namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// Application use-case tests (DEC-037): persistence is exercised through hand-written
/// repository fakes, never EF Core.
/// </summary>
public class CreateBookingCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store;
    private readonly FakeBookingRepository _bookings;
    private readonly FakeCustomerRepository _customers;
    private readonly FakePackageRepository _packages;
    private readonly FakeRefundRepository _refunds;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly Mock<IValidator<CreateBookingRequestDto>> _validatorMock;
    private readonly CreateBookingCommand _command;

    public CreateBookingCommandTests()
    {
        _store = new FakeSalsCaterSuiteDataStore();
        _bookings = new FakeBookingRepository(_store);
        _customers = new FakeCustomerRepository(_store);
        _packages = new FakePackageRepository(_store);
        _refunds = new FakeRefundRepository(_store);

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(u => u.UserName).Returns("event_planner");

        // By default the real validator is exercised end-to-end.
        _command = new CreateBookingCommand(_customers, _packages, _bookings, _currentUserMock.Object, new CreateBookingValidator());

        // A permissive validator is used for the domain-exception path, where the
        // payload must reach Booking.Create() (which enforces the invariant).
        _validatorMock = new Mock<IValidator<CreateBookingRequestDto>>();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateBookingRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private static CreateBookingRequestDto ValidRequest(int customerId = 1, int? packageId = null)
        => new()
        {
            CustomerId = customerId,
            NoOfPerson = 100,
            Occasion = "Grand Wedding Reception",
            Venue = "Grand Ballroom A",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(30).AddHours(4),
            PackageId = packageId,
            EventColor = "#10b981"
        };

    private void SeedCustomer(int customerId = 1)
    {
        if (!_store.Customers.Any(c => c.CustomerId == customerId))
        {
            _store.Customers.Add(new Customer { CustomerId = customerId, FirstName = "John", LastName = "Doe" });
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomerDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var request = ValidRequest(customerId: 999, packageId: 1);

        // Act
        var result = await _command.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Customer not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPackageDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        SeedCustomer(1);
        var request = ValidRequest(customerId: 1, packageId: 999);

        // Act
        var result = await _command.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package with ID 999 not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestIsInvalid_ShouldReturnValidationFailureAndPersistNothing()
    {
        // Arrange — Occasion is required by CreateBookingValidator.
        var request = ValidRequest(customerId: 1);
        request.Occasion = string.Empty;

        // Act
        var result = await _command.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDomainRuleViolated_ShouldReturnFailureInsteadOfThrowing()
    {
        // Arrange — validation is bypassed so the payload reaches the domain factory,
        // which rejects NoOfPerson <= 0 with a DomainException.
        var command = new CreateBookingCommand(_customers, _packages, _bookings, _currentUserMock.Object, _validatorMock.Object);
        SeedCustomer(1);
        var request = ValidRequest(customerId: 1);
        request.NoOfPerson = 0;

        // Act
        var result = await command.ExecuteAsync(request);

        // Assert — mapped to a failed result, not an unhandled exception.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Number of persons must be greater than zero.");
        _bookings.Bookings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomerAndPackageExist_ShouldCreateBookingSuccessfully()
    {
        // Arrange
        SeedCustomer(2);
        _store.Packages.Add(new Package { PackageId = 5, PackageType = "Buffet Premium", AmountPax = 650m, NumPaxId = 4 });

        var request = ValidRequest(customerId: 2, packageId: 5);
        request.NoOfPerson = 120;
        request.Occasion = "Corporate Gala Dinner";
        request.Venue = "Emerald Ballroom";

        // Act
        var result = await _command.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);

        var savedBooking = _bookings.Bookings.FirstOrDefault(b => b.CustomerId == 2);
        savedBooking.Should().NotBeNull();
        savedBooking!.Occasion.Should().Be("Corporate Gala Dinner");
        savedBooking.CreatedByUser.Should().Be("event_planner");
        savedBooking.PackageAmount.Should().Be(120 * 650m); // 78,000
        savedBooking.Status.Should().NotBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAssignFormattedBookingNumber()
    {
        // Arrange
        SeedCustomer(3);
        var request = ValidRequest(customerId: 3);

        // Act
        var result = await _command.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = _bookings.Bookings.Single(b => b.TransactionId == result.Data);
        saved.BookingNumber.Should().NotBeNullOrWhiteSpace();
        saved.BookingNumber.Should().MatchRegex(@"^BK-\d{8}-\d{5}$");
        saved.BookingNumber.Should().EndWith($"-{saved.TransactionId:D5}");
    }

    [Fact]
    public async Task ExecuteAsync_TwoBookings_ShouldReceiveDistinctBookingNumbers()
    {
        // Arrange
        SeedCustomer(4);

        // Act
        var first = await _command.ExecuteAsync(ValidRequest(customerId: 4));
        var second = await _command.ExecuteAsync(ValidRequest(customerId: 4));

        // Assert
        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeTrue();

        var numbers = _bookings.Bookings.Select(b => b.BookingNumber).ToList();
        numbers.Should().HaveCount(2);
        numbers.Should().OnlyContain(n => !string.IsNullOrWhiteSpace(n));
        numbers.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ThenCancel_ShouldStillCancelTheNumberedBooking()
    {
        // Arrange — the number assignment must not disturb the cancel flow.
        SeedCustomer(5);
        var createResult = await _command.ExecuteAsync(ValidRequest(customerId: 5));
        createResult.Succeeded.Should().BeTrue();

        var cancelCommand = new CancelBookingCommand(_bookings, _refunds);

        // Act
        var cancelResult = await cancelCommand.ExecuteAsync(createResult.Data, "Client request", null);

        // Assert
        cancelResult.Succeeded.Should().BeTrue();
        var cancelled = _bookings.Bookings.Single(b => b.TransactionId == createResult.Data);
        cancelled.Status.Should().Be(BookingStatus.Cancelled);
        cancelled.BookingNumber.Should().MatchRegex(@"^BK-\d{8}-\d{5}$");
    }

    /// <summary>
    /// DEC-016: a booking must never be left without a booking number. When the
    /// post-INSERT numbering step fails, the create operation fails and the
    /// just-inserted row is deleted (compensating delete).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenBookingNumberAssignmentFails_ShouldReturnFailureAndRemoveTheInsertedRow()
    {
        // Arrange — the store never assigns the IDENTITY id, so AssignBookingNumber()
        // fails exactly as it would if the persisted row could not be re-read.
        SeedCustomer(6);
        _store.SuppressIdentityAssignment = true;

        // Act
        var result = await _command.ExecuteAsync(ValidRequest(customerId: 6));

        // Assert — the failure is reported and the orphaned insert is rolled back.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainMatch("Failed to assign a booking number:*");
        _bookings.Bookings.Should().BeEmpty();
    }
}
