namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The Customer Master directory use case (spec §5.1, REQ-CUS-01).
/// <para>
/// The Application-layer tests drive the hand-written fake, which mirrors the EF Core
/// implementation's rules exactly; the authoritative projection is asserted against EF Core
/// in <c>CustomerRepositoryTests</c> and end to end on real SQL Server by the integration
/// <c>CustomerMasterEndpointTests</c>.
/// </para>
/// </summary>
public class CustomerDirectoryQueryTests
{
    /// <summary>The fixed "today" the aging assertions are anchored on (DEC-041).</summary>
    private static readonly DateOnly Today = new(2026, 10, 19);

    /// <summary>
    /// The fixed instant the aggregate is stamped with — the same clock the aging service
    /// reads, so the whole payload is deterministic.
    /// </summary>
    private static readonly DateTime GeneratedAtInstant =
        new(2026, 10, 19, 9, 15, 0, DateTimeKind.Utc);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeCustomerRepository _customers;
    private readonly GetCustomerDirectoryQuery _directory;

    public CustomerDirectoryQueryTests()
    {
        var clock = new FakeDateTime(GeneratedAtInstant);

        _customers = new FakeCustomerRepository(
            _store,
            new BookingAmountDueCalculator(),
            new CustomerAgingService(clock),
            clock);

        _directory = new GetCustomerDirectoryQuery(_customers);
    }

    private Booking AddBooking(int customerId, decimal pricePerHead, DateOnly eventDate)
    {
        var start = eventDate.ToDateTime(new TimeOnly(10, 0));
        var booking = Booking.Create(
            customerId: customerId,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(4),
            pricePerHead: pricePerHead);

        _store.Bookings.Add(booking);
        _store.AssignTransactionId(booking);
        return booking;
    }

    private static Payment PaymentFor(Booking booking, string paymentNo, decimal amount) => new()
    {
        PaymentNo = paymentNo,
        TransactionId = booking.TransactionId,
        AmountPaid = amount,
        Particular = "Payment",
        DateOfPayment = DateTime.UtcNow,
        PaymentMeans = PaymentMeans.Cash
    };

    private static Customer AddCustomer(int id, string lastName, string firstName, string? company = null)
    {
        var customer = new Customer
        {
            CustomerId = id,
            LastName = lastName,
            FirstName = firstName,
            Company = company,
            Contact1 = $"0917000{id:D4}"
        };
        return customer;
    }

    [Fact]
    public async Task The_directory_should_return_the_paged_payload()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        result.Succeeded.Should().BeTrue();
        var directory = result.Data!;
        directory.Page.Should().Be(1);
        directory.PageSize.Should().Be(10);
        directory.TotalCount.Should().Be(1);
        directory.TotalPages.Should().Be(1);
        directory.Items.Should().ContainSingle();
        directory.Summary.TotalClients.Should().Be(1);
    }

    [Fact]
    public async Task The_summary_should_describe_the_whole_filtered_set_and_not_the_page()
    {
        // Arrange — 5 customers, each owing 10,000.
        for (var i = 1; i <= 5; i++)
        {
            _store.Customers.Add(AddCustomer(i, $"Last{i}", $"First{i}"));
            var booking = AddBooking(i, 100m, Today.AddDays(10));   // due 10,000
            _store.Payments.Add(PaymentFor(booking, $"OR-{i}", 0m));
        }

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 2);

        // Assert
        var directory = result.Data!;
        directory.Items.Should().HaveCount(2, "the page holds at most pageSize rows");
        directory.TotalCount.Should().Be(5);
        directory.TotalPages.Should().Be(3, "ceil(5 ÷ 2)");
        directory.Summary.TotalClients.Should().Be(5, "the summary covers the whole filtered set");
        directory.Summary.ActiveArAmount.Should().Be(50000m);
    }

    [Fact]
    public async Task The_page_should_be_clamped_and_the_page_size_should_be_bounded()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));

        // Act — page 0 and pageSize 0 are clamped, pageSize 1000 is capped.
        var low = await _directory.ExecuteAsync(null, null, null, 0, 0);
        var high = await _directory.ExecuteAsync(null, null, null, 1, 1000);

        // Assert
        low.Data!.Page.Should().Be(1);
        low.Data.PageSize.Should().Be(1);
        high.Data!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task The_search_should_match_the_name_the_company_and_the_contact_number()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria", "Acme Foods"));
        _store.Customers.Add(AddCustomer(2, "Cruz", "Jose"));
        _store.Customers.Add(AddCustomer(3, "Reyes", "Ana"));

        // Act — a case-insensitive substring.
        var byName = await _directory.ExecuteAsync("sant", null, null, 1, 10);
        var byCompany = await _directory.ExecuteAsync("ACME", null, null, 1, 10);
        var byContact = await _directory.ExecuteAsync("09170000003", null, null, 1, 10);

        // Assert
        byName.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        byCompany.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        byContact.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(3);
    }

    [Fact]
    public async Task The_customer_type_should_be_derived_and_never_persisted()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria", "Acme Foods"));
        _store.Customers.Add(AddCustomer(2, "Cruz", "Jose"));
        _store.Customers.Add(AddCustomer(3, "Reyes", "Ana", "   "));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var types = result.Data!.Items.OrderBy(i => i.CustomerId).Select(i => i.CustomerType).ToList();
        types.Should().Equal("Corporate", "Private", "Private");
        _store.Customers.Single(c => c.CustomerId == 1).Company.Should().Be("Acme Foods",
            "the classification is a presentation — the record is never changed");
    }

    [Fact]
    public async Task The_customer_type_filter_should_narrow_the_result_set()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria", "Acme Foods"));
        _store.Customers.Add(AddCustomer(2, "Cruz", "Jose"));

        // Act
        var corporate = await _directory.ExecuteAsync(null, "Corporate", null, 1, 10);
        var privateAccounts = await _directory.ExecuteAsync(null, "Private", null, 1, 10);

        // Assert
        corporate.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        privateAccounts.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(2);
    }

    [Fact]
    public async Task An_unrecognised_customer_type_should_be_ignored()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria", "Acme Foods"));
        _store.Customers.Add(AddCustomer(2, "Cruz", "Jose"));

        // Act
        var result = await _directory.ExecuteAsync(null, "banana", null, 1, 10);

        // Assert — the filter is ignored, never a 400.
        result.Succeeded.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task The_booking_counts_should_split_the_past_from_the_upcoming()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));
        AddBooking(1, 100m, Today.AddDays(-10));      // past
        AddBooking(1, 100m, Today);                   // today — upcoming
        AddBooking(1, 100m, Today.AddDays(5));        // upcoming
        var cancelled = AddBooking(1, 100m, Today.AddDays(-2));
        cancelled.Cancel();

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var counts = result.Data!.Items.Single().BookingCounts;
        counts.Total.Should().Be(3, "the cancelled booking is not part of the active total");
        counts.Past.Should().Be(1);
        counts.Upcoming.Should().Be(2);
        counts.Cancelled.Should().Be(1);
        counts.Completed.Should().Be(0);
    }

    [Fact]
    public async Task The_money_should_come_from_the_shared_calculator_and_never_go_negative()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));
        var booking = AddBooking(1, 500m, Today.AddDays(5));   // due 50,000
        _store.Payments.Add(PaymentFor(booking, "OR-1", 60000m));  // overpaid

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var item = result.Data!.Items.Single();
        item.TotalContractedValue.Should().Be(50000m);
        item.TotalPaid.Should().Be(60000m);
        item.OutstandingBalance.Should().Be(0m, "an overpaid booking clamps to zero (BR-03)");
    }

    [Fact]
    public async Task A_cancelled_booking_should_contribute_nothing_at_all()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));
        var cancelled = AddBooking(1, 500m, Today.AddDays(5));  // due 50,000
        _store.Payments.Add(PaymentFor(cancelled, "OR-1", 10000m));
        cancelled.Cancel();

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var item = result.Data!.Items.Single();
        item.TotalContractedValue.Should().Be(0m);
        item.TotalPaid.Should().Be(0m);
        item.OutstandingBalance.Should().Be(0m);
        item.LedgerState.Should().Be("Clean");
    }

    [Fact]
    public async Task The_ledger_state_should_be_the_worst_bucket_of_the_customers_balances()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));
        var recent = AddBooking(1, 100m, Today.AddDays(-5));     // Days1To30
        var old = AddBooking(1, 100m, Today.AddDays(-100));      // Over90Days
        _store.Payments.Add(PaymentFor(recent, "OR-1", 0m));
        _store.Payments.Add(PaymentFor(old, "OR-2", 0m));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var item = result.Data!.Items.Single();
        item.LedgerState.Should().Be("Over90Days");
        item.LedgerStateLabel.Should().Be("Overdue 90+ days");
    }

    [Fact]
    public async Task The_ar_state_filter_should_narrow_the_result_set()
    {
        // Arrange
        // 1 — clean (fully paid)
        _store.Customers.Add(AddCustomer(1, "Aaa", "Clean"));
        var settled = AddBooking(1, 100m, Today.AddDays(-100));
        _store.Payments.Add(PaymentFor(settled, "OR-1", 10000m));

        // 2 — open, not yet due
        _store.Customers.Add(AddCustomer(2, "Bbb", "Open"));
        AddBooking(2, 100m, Today.AddDays(10));

        // 3 — overdue by 45 days
        _store.Customers.Add(AddCustomer(3, "Ccc", "Overdue30"));
        AddBooking(3, 100m, Today.AddDays(-45));

        // 4 — overdue by 70 days
        _store.Customers.Add(AddCustomer(4, "Ddd", "Overdue60"));
        AddBooking(4, 100m, Today.AddDays(-70));

        // Act
        var clean = await _directory.ExecuteAsync(null, null, "Clean", 1, 10);
        var open = await _directory.ExecuteAsync(null, null, "Open", 1, 10);
        var overdue30 = await _directory.ExecuteAsync(null, null, "Overdue30", 1, 10);
        var overdue60 = await _directory.ExecuteAsync(null, null, "Overdue60", 1, 10);
        var ignored = await _directory.ExecuteAsync(null, null, "banana", 1, 10);

        // Assert
        clean.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        open.Data!.Items.Select(i => i.CustomerId).Should().BeEquivalentTo(new[] { 2, 3, 4 });
        overdue30.Data!.Items.Select(i => i.CustomerId).Should().BeEquivalentTo(new[] { 3, 4 });
        overdue60.Data!.Items.Select(i => i.CustomerId).Should().BeEquivalentTo(new[] { 4 });
        ignored.Data!.Items.Should().HaveCount(4, "an unrecognised AR state is ignored");
    }

    [Fact]
    public async Task The_summary_should_report_the_overdue_clients()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Aaa", "Overdue"));
        AddBooking(1, 100m, Today.AddDays(-45));   // 10,000, 45 days overdue
        _store.Customers.Add(AddCustomer(2, "Bbb", "Current"));
        AddBooking(2, 100m, Today.AddDays(10));    // 10,000, not yet due

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var summary = result.Data!.Summary;
        summary.TotalClients.Should().Be(2);
        summary.ActiveArAmount.Should().Be(20000m);
        summary.ClientsWithBalance.Should().Be(2);
        summary.TotalContractedValue.Should().Be(20000m);
        summary.AverageValuePerClient.Should().Be(10000m);
        summary.OverdueAmount.Should().Be(10000m, "only the client that is more than 30 days overdue");
        summary.OverdueClientCount.Should().Be(1);
    }

    [Fact]
    public async Task The_average_value_per_client_should_be_null_when_there_are_no_clients()
    {
        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert — a null means "undefined", never zero (contract §9.2 rule 6).
        var summary = result.Data!.Summary;
        summary.TotalClients.Should().Be(0);
        summary.AverageValuePerClient.Should().BeNull();
    }

    [Fact]
    public async Task The_soft_deleted_customers_should_be_excluded()
    {
        // Arrange
        var deleted = AddCustomer(1, "Santos", "Maria");
        deleted.IsDeleted = true;
        _store.Customers.Add(deleted);
        _store.Customers.Add(AddCustomer(2, "Cruz", "Jose"));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(2);
        result.Data.Summary.TotalClients.Should().Be(1);
    }

    [Fact]
    public async Task The_rows_should_be_ordered_by_last_name_then_first_name()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Zara"));
        _store.Customers.Add(AddCustomer(2, "Santos", "Ana"));
        _store.Customers.Add(AddCustomer(3, "Cruz", "Jose"));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        result.Data!.Items.Select(i => i.CustomerId).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task The_unpaged_variant_should_return_the_whole_filtered_set()
    {
        // Arrange
        for (var i = 1; i <= 3; i++)
        {
            _store.Customers.Add(AddCustomer(i, $"Last{i}", $"First{i}"));
        }

        // Act
        var result = await _directory.ExecuteUnpagedAsync(null, null, null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(3);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // The quick-view counts and the aggregate's timestamp (spec §4.3 / §4.10)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A population of four customers:
    /// 1 — corporate, settled; 2 — corporate, open balance (not yet due);
    /// 3 — private, open balance and 45 days overdue; 4 — private, settled.
    /// </summary>
    private void SeedQuickViewPopulation()
    {
        _store.Customers.Add(AddCustomer(1, "Aaa", "Settled", "Acme Foods"));
        _store.Customers.Add(AddCustomer(2, "Bbb", "Open", "Acme Foods"));
        _store.Customers.Add(AddCustomer(3, "Ccc", "Overdue"));
        _store.Customers.Add(AddCustomer(4, "Ddd", "Private"));

        var settled = AddBooking(1, 100m, Today.AddDays(-10));
        _store.Payments.Add(PaymentFor(settled, "OR-1", 10000m));

        AddBooking(2, 100m, Today.AddDays(10));                       // open, not yet due

        var overdue = AddBooking(3, 100m, Today.AddDays(-45));        // open, 45 days overdue
        _store.Payments.Add(PaymentFor(overdue, "OR-3", 0m));

        var clean = AddBooking(4, 100m, Today.AddDays(-10));
        _store.Payments.Add(PaymentFor(clean, "OR-4", 10000m));
    }

    [Fact]
    public async Task The_quick_view_counts_should_describe_the_search_filtered_population()
    {
        // Arrange
        SeedQuickViewPopulation();

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert
        var counts = result.Data!.QuickViewCounts;
        counts.All.Should().Be(4);
        counts.Corporate.Should().Be(2);
        counts.Private.Should().Be(2);
        counts.WithOpenBalance.Should().Be(2);
        counts.Overdue30.Should().Be(1);
    }

    [Fact]
    public async Task The_quick_view_counts_should_add_up_to_the_whole_population()
    {
        // Arrange
        SeedQuickViewPopulation();

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert — the classification is exhaustive, so corporate + private = all.
        var counts = result.Data!.QuickViewCounts;
        (counts.Corporate + counts.Private).Should().Be(counts.All);
    }

    [Fact]
    public async Task A_customer_type_filter_should_not_change_the_quick_view_counts()
    {
        // Arrange
        SeedQuickViewPopulation();

        // Act
        var unfiltered = await _directory.ExecuteAsync(null, null, null, 1, 10);
        var corporate = await _directory.ExecuteAsync(null, "Corporate", null, 1, 10);
        var privateAccounts = await _directory.ExecuteAsync(null, "Private", null, 1, 10);

        // Assert — the summary follows the filter, the pills keep describing the population.
        corporate.Data!.Summary.TotalClients.Should().Be(2);
        privateAccounts.Data!.Summary.TotalClients.Should().Be(2);

        corporate.Data!.QuickViewCounts.Should().BeEquivalentTo(unfiltered.Data!.QuickViewCounts);
        privateAccounts.Data!.QuickViewCounts.Should().BeEquivalentTo(unfiltered.Data!.QuickViewCounts);
    }

    [Fact]
    public async Task An_ar_state_filter_should_not_change_the_quick_view_counts()
    {
        // Arrange
        SeedQuickViewPopulation();

        // Act
        var unfiltered = await _directory.ExecuteAsync(null, null, null, 1, 10);
        var overdue30 = await _directory.ExecuteAsync(null, null, "Overdue30", 1, 10);
        var open = await _directory.ExecuteAsync(null, null, "Open", 1, 10);

        // Assert
        overdue30.Data!.Summary.TotalClients.Should().Be(1);
        open.Data!.Summary.TotalClients.Should().Be(2);

        overdue30.Data!.QuickViewCounts.Should().BeEquivalentTo(unfiltered.Data!.QuickViewCounts);
        open.Data!.QuickViewCounts.Should().BeEquivalentTo(unfiltered.Data!.QuickViewCounts);
    }

    [Fact]
    public async Task The_search_filter_should_narrow_the_quick_view_counts()
    {
        // Arrange
        SeedQuickViewPopulation();

        // Act
        var result = await _directory.ExecuteAsync("Aaa", null, null, 1, 10);

        // Assert — the search is part of the population the pills describe.
        var counts = result.Data!.QuickViewCounts;
        counts.All.Should().Be(1);
        counts.Corporate.Should().Be(1);
        counts.Private.Should().Be(0);
        counts.WithOpenBalance.Should().Be(0);
        counts.Overdue30.Should().Be(0);
    }

    [Fact]
    public async Task The_directory_should_carry_the_server_generated_timestamp()
    {
        // Arrange
        _store.Customers.Add(AddCustomer(1, "Santos", "Maria"));

        // Act
        var result = await _directory.ExecuteAsync(null, null, null, 1, 10);

        // Assert — the server's UTC instant, never a client clock (spec §4.10).
        var generatedAt = result.Data!.GeneratedAt;
        generatedAt.Should().Be(GeneratedAtInstant);
        generatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
