namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using Xunit;

public class BookingRepositoryTests : IDisposable
{
    private readonly SalsCaterSuiteDbContext _context;
    private readonly BookingRepository _repository;

    public BookingRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new BookingRepository(_context);
    }

    /// <summary>Bookings can only be created through the domain factory.</summary>
    private static Booking CreateTestBooking(int customerId = 1, string? occasion = null, int daysFromNow = 30, int noOfPerson = 50)
    {
        var eventDateTime = DateTime.UtcNow.AddDays(daysFromNow);
        return Booking.Create(
            customerId: customerId,
            noOfPerson: noOfPerson,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDateTime),
            startDate: eventDateTime,
            endDate: eventDateTime.AddHours(4),
            occasion: occasion);
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookingExists_ShouldReturnBooking()
    {
        // Arrange
        var booking = CreateTestBooking(occasion: "Anniversary");
        booking.Venue = "Sunset Garden";
        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
        var transactionId = booking.TransactionId;

        // Act
        var result = await _repository.GetByIdAsync(transactionId);

        // Assert
        result.Should().NotBeNull();
        result!.TransactionId.Should().Be(transactionId);
        result.Occasion.Should().Be("Anniversary");
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookingDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(9999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetWithDetailsAsync_ShouldIncludeRelatedEntities()
    {
        // Arrange
        var customer = new Customer { CustomerId = 10, FirstName = "Sarah", LastName = "Connor" };
        var package = new Package { PackageId = 20, PackageType = "Silver Buffet" };
        var booking = CreateTestBooking(customerId: 10, occasion: "Family Reunion");
        booking.Customer = customer;
        booking.Package = package;

        await _context.Customers.AddAsync(customer);
        await _context.Packages.AddAsync(package);
        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();

        var payment = new Payment
        {
            PaymentNo = "OR-200-01",
            TransactionId = booking.TransactionId,
            AmountPaid = 5000m
        };
        await _context.Payments.AddAsync(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetWithDetailsAsync(booking.TransactionId);

        // Assert
        result.Should().NotBeNull();
        result!.Customer.Should().NotBeNull();
        result.Customer!.FullName.Should().Be("Connor, Sarah");
        result.Package.Should().NotBeNull();
        result.Package!.PackageType.Should().Be("Silver Buffet");
        result.Payments.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByDateRangeAsync_ShouldFilterCorrectly()
    {
        // Arrange
        var baseDate = new DateTime(2026, 12, 1);
        var b1 = CreateSpecificDateTestBooking(DateOnly.FromDateTime(baseDate.AddDays(5)));
        var b2 = CreateSpecificDateTestBooking(DateOnly.FromDateTime(baseDate.AddDays(15)));
        var b3 = CreateSpecificDateTestBooking(DateOnly.FromDateTime(baseDate.AddDays(25)));

        await _context.Bookings.AddRangeAsync(b1, b2, b3);
        await _context.SaveChangesAsync();

        // Act (Filter between Dec 10 and Dec 20)
        var result = await _repository.GetByDateRangeAsync(
            DateOnly.FromDateTime(baseDate.AddDays(10)),
            DateOnly.FromDateTime(baseDate.AddDays(20)));

        // Assert
        result.Should().HaveCount(1);
        result[0].TransactionId.Should().Be(b2.TransactionId);
    }

    /// <summary>Creates a booking with an explicit event date (factory requires start &lt;= end).</summary>
    private static Booking CreateSpecificDateTestBooking(DateOnly eventDate)
    {
        var start = eventDate.ToDateTime(TimeOnly.FromTimeSpan(new TimeSpan(10, 0, 0)));
        return Booking.Create(
            customerId: 1,
            noOfPerson: 50,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(4));
    }

    [Fact]
    public async Task AddAsync_And_SaveChangesAsync_ShouldPersistEntity()
    {
        // Arrange
        var newBooking = CreateTestBooking(occasion: "Graduation Bash", noOfPerson: 80);

        // Act
        await _repository.AddAsync(newBooking);
        await _repository.SaveChangesAsync();

        // Assert
        var persisted = await _context.Bookings.FindAsync(newBooking.TransactionId);
        persisted.Should().NotBeNull();
        persisted!.Occasion.Should().Be("Graduation Bash");
        persisted.NumberOfHeads.Should().Be(80);
    }

    [Fact]
    public async Task Update_ShouldModifyEntityStateAndPersistChanges()
    {
        // Arrange
        var booking = CreateTestBooking(occasion: "Original Title");
        booking.Venue = "Hall 1";
        await _context.Bookings.AddAsync(booking);
        await _context.SaveChangesAsync();
        var transactionId = booking.TransactionId;

        // Detach to simulate a fresh load.
        _context.ChangeTracker.Clear();
        var loaded = await _context.Bookings.FindAsync(transactionId);
        loaded.Should().NotBeNull();

        // Act
        loaded!.Occasion = "Updated Occasion Title";
        _repository.Update(loaded);
        await _repository.SaveChangesAsync();

        // Assert
        var updated = await _context.Bookings.FindAsync(transactionId);
        updated!.Occasion.Should().Be("Updated Occasion Title");
    }

    public void Dispose()
    {
        TestDbContextFactory.Destroy(_context);
    }
}
