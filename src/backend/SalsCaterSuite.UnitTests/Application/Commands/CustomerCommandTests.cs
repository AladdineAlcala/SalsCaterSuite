namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Customers.Commands;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;
using SalsCaterSuite.Application.Features.Customers.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// DEC-037: the customer write use cases are repository-backed. These tests drive the
/// hand-written <see cref="FakeCustomerRepository"/> and assert the same observable
/// behaviour the EF Core commands previously produced.
/// </summary>
public class CustomerCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeCustomerRepository _customers;
    private readonly CreateCustomerCommand _create;
    private readonly UpdateCustomerCommand _update;

    public CustomerCommandTests()
    {
        _customers = new FakeCustomerRepository(_store);
        _create = new CreateCustomerCommand(_customers, new CreateCustomerValidator());
        // spec §7.3 / BR-11: the update command now runs the same validator the create
        // command runs, so its constructor takes one too.
        _update = new UpdateCustomerCommand(_customers, new CreateCustomerValidator());
    }

    private static CreateCustomerRequest ValidRequest() =>
        new("Santos", "Maria", null, "12 Rizal St, Manila", "09171234567", null, null);

    [Fact]
    public async Task Create_WhenValid_ShouldPersistCustomerAndReturnAssignedId()
    {
        // Act
        var result = await _create.ExecuteAsync(ValidRequest());

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
        _store.Customers.Should().ContainSingle();
        _store.Customers[0].LastName.Should().Be("Santos");
        _store.Customers[0].FirstName.Should().Be("Maria");
        _store.Customers[0].DateRegistered.Should().NotBeNull();
        _customers.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_WhenLastNameMissing_ShouldFailValidationAndPersistNothing()
    {
        // Arrange
        var request = ValidRequest() with { LastName = " " };

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Last name is required.");
        _store.Customers.Should().BeEmpty();
        _customers.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenCustomerExists_ShouldApplyChangesAndSave()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 7, LastName = "Old", FirstName = "Name" });

        // Act
        var result = await _update.ExecuteAsync(7, ValidRequest() with { Company = "Acme Catering" });

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = _store.Customers.Single(c => c.CustomerId == 7);
        saved.LastName.Should().Be("Santos");
        saved.Company.Should().Be("Acme Catering");
        _customers.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_WhenCustomerDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Act
        var result = await _update.ExecuteAsync(999, ValidRequest());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Customer not found.");
        _customers.SaveCount.Should().Be(0);
    }

    // ── spec §7.3 / BR-11 — the update validates its input ────────────────────────────

    [Fact]
    public async Task Update_WhenLastNameIsBlank_ShouldFailValidationAndChangeNothing()
    {
        // Arrange — B7: a blank last name is rejected and nothing is written.
        _store.Customers.Add(new Customer { CustomerId = 7, LastName = "Santos", FirstName = "Maria" });

        // Act
        var result = await _update.ExecuteAsync(7, ValidRequest() with { LastName = " " });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Last name is required.");
        _store.Customers.Single(c => c.CustomerId == 7).LastName.Should().Be("Santos");
        _customers.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenLastNameIsTooLong_ShouldFailValidationAndChangeNothing()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 7, LastName = "Santos", FirstName = "Maria" });

        // Act
        var result = await _update.ExecuteAsync(7, ValidRequest() with { LastName = new string('x', 101) });

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Last name must not exceed 100 characters.");
        _store.Customers.Single(c => c.CustomerId == 7).LastName.Should().Be("Santos");
        _customers.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_WhenTheCustomerIsMissingAndThePayloadIsValid_ShouldReturnNotFound()
    {
        // Act
        var result = await _update.ExecuteAsync(999, ValidRequest());

        // Assert — the 404 and the 400 are distinguished by the result type, not by the text.
        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<SalsCaterSuite.Application.Features.Customers.CustomerNotFoundResult>();
        _customers.SaveCount.Should().Be(0);
    }
}
