namespace SalsCaterSuite.UnitTests.Application.Dashboard;

using System.Reflection;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;
using SalsCaterSuite.Application.Features.Dashboard.Models;
using SalsCaterSuite.Application.Features.Dashboard.Queries;
using SalsCaterSuite.Application.Features.Dashboard.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;

/// <summary>
/// A hand-built dashboard fixture (DEC-037). No EF Core is involved: every booking, payment
/// and menu is a real domain entity in the shared in-memory store, and the repositories are
/// the hand-written fakes. Every expected value in the dashboard tests is therefore derived
/// from plain arithmetic over the fixture, not from production code.
/// </summary>
internal sealed class DashboardHarness
{
    /// <summary>The reference "now" used by the dashboard tests: 19 October 2026, 08:00 UTC.</summary>
    public static readonly DateTime ReferenceNow = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

    private static readonly PropertyInfo StatusProperty =
        typeof(Booking).GetProperty(nameof(Booking.Status))!;

    private static readonly PropertyInfo TransactionIdProperty =
        typeof(Booking).GetProperty(nameof(Booking.TransactionId))!;

    private readonly FakeBookingRepository _bookings;
    private readonly FakePaymentRepository _payments;
    private readonly FakeMenuRepository _menus;

    private int _nextTransactionId = 1;
    private int _nextReceiptNumber = 1;
    private int _nextMenuNumber = 1;

    public DashboardHarness(DateTime? utcNow = null, IFoodCostBenchmarkProvider? benchmarks = null)
    {
        Clock = new FakeDateTime(utcNow ?? ReferenceNow);
        Store = new FakeSalsCaterSuiteDataStore();
        _bookings = new FakeBookingRepository(Store);
        _payments = new FakePaymentRepository(Store);
        _menus = new FakeMenuRepository(Store);
        FakeBenchmarks = new FakeFoodCostBenchmarkProvider();
        Benchmarks = benchmarks ?? FakeBenchmarks;

        Service = new DashboardService(
            _bookings,
            _payments,
            _menus,
            new BookingAmountDueCalculator(),
            Benchmarks,
            Clock);

        Query = new GetDashboardQuery(Service, Clock);

        Store.Customers.Add(new Customer
        {
            CustomerId = 1,
            LastName = "Santos",
            FirstName = "Maria"
        });
    }

    public FakeSalsCaterSuiteDataStore Store { get; }

    public FakeDateTime Clock { get; }

    public FakeFoodCostBenchmarkProvider FakeBenchmarks { get; }

    public IFoodCostBenchmarkProvider Benchmarks { get; }

    public DashboardService Service { get; }

    public GetDashboardQuery Query { get; }

    /// <summary>The UTC date the dashboard treats as "today".</summary>
    public DateOnly Today => DateOnly.FromDateTime(Clock.UtcNow);

    /// <summary>The first day of the reference period (October 2026).</summary>
    public DateOnly PeriodStart => new(2026, 10, 1);

    /// <summary>The last day of the reference period (October 2026).</summary>
    public DateOnly PeriodEnd => new(2026, 10, 31);

    public Booking AddBooking(
        DateOnly eventDate,
        int heads,
        decimal pricePerHead,
        BookingStatus status = BookingStatus.Pending,
        int? transactionId = null,
        string occasion = "Test Event",
        string? packageName = "Premier Package",
        bool isDeleted = false,
        decimal discountPercentage = 0m,
        decimal discountAmount = 0m)
    {
        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: eventDate.ToDateTime(new TimeOnly(17, 0)),
            endDate: eventDate.ToDateTime(new TimeOnly(22, 0)),
            occasion: occasion,
            venue: "Test Venue",
            packageName: packageName,
            pricePerHead: pricePerHead);

        var id = transactionId ?? _nextTransactionId++;
        if (transactionId.HasValue && transactionId.Value >= _nextTransactionId)
        {
            _nextTransactionId = transactionId.Value + 1;
        }

        TransactionIdProperty.SetValue(booking, id);
        StatusProperty.SetValue(booking, status);

        if (discountPercentage != 0m)
        {
            booking.DiscountPercentage = discountPercentage;
        }

        if (discountAmount != 0m)
        {
            booking.RecalculateFinancials(0m, 0m, discountAmount, 0m);
        }

        booking.AssignBookingNumber();
        booking.IsDeleted = isDeleted;

        Store.Bookings.Add(booking);
        return booking;
    }

    public Payment AddPayment(
        int transactionId,
        decimal amount,
        DateTime dateOfPayment,
        bool isDeleted = false)
    {
        var payment = new Payment
        {
            PaymentNo = $"OR-TEST-{_nextReceiptNumber++:D4}",
            TransactionId = transactionId,
            DateOfPayment = dateOfPayment,
            Particular = "Test payment",
            PaymentMeans = PaymentMeans.Cash,
            AmountPaid = amount,
            IsDeleted = isDeleted
        };

        Store.Payments.Add(payment);
        return payment;
    }

    public Menu AddMenu(
        int? courseId,
        decimal? price,
        decimal? foodCost,
        string menuName = "Test Dish",
        bool isDeleted = false)
    {
        var menu = new Menu
        {
            MenuId = $"M-{_nextMenuNumber++:D3}",
            CourseId = courseId,
            MenuName = menuName,
            Price = price,
            FoodCost = foodCost,
            IsDeleted = isDeleted
        };

        Store.Menus.Add(menu);
        return menu;
    }

    public void AddCourse(int courseId, string courseName)
    {
        Store.CourseCategories.Add(new CourseCategory { CourseId = courseId, CourseName = courseName });
    }

    public Task<Result<DashboardResponse>> GetAsync(string? period = null) =>
        Query.ExecuteAsync(period);
}
