using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// The Bookings &amp; Events Register and the Reservation Workbench against the real API and a
/// disposable LocalDB database (REQ-BKG-05, REQ-BKG-01, Scenarios 61-72, DEC-019).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BookingsRegisterEndpointTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public BookingsRegisterEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    // ─────────────────────────── the catalogue fixtures ───────────────────────────

    /// <summary>
    /// Seeds a tier the operator can book. The <paramref name="label"/> distinguishes the
    /// fixtures from one another; the <b>type</b> is always <c>Regular</c>, because BR-40
    /// (DEC-141) limits the out-of-town surcharge to the <c>Regular</c> and <c>Premier</c>
    /// types verbatim and the pinning test lives in the unit suite.
    /// </summary>
    private async Task<int> SeedTierAsync(string label, decimal rate, bool active = true)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var package = new Package
        {
            // The code is unique among the live tiers; these fixtures bypass the create command,
            // so they assign one themselves (the 2026-10-24 code column is NOT NULL + unique).
            PackageCode = ("PKG-REG-" + Guid.NewGuid().ToString("N"))[..30],
            PackageType = "Regular",
            Description = $"{label} Tier",
            AmountPax = rate,
            IsActive = active,
            CreatedDate = DateTime.UtcNow
        };

        context.Packages.Add(package);
        await context.SaveChangesAsync();

        return package.PackageId;
    }

    private async Task<int> SeedMunicipalityAsync(string name, decimal rate)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var area = new Area
        {
            AreaDetails = name,
            AdditionalRate = rate,
            CreatedDate = DateTime.UtcNow
        };

        context.Areas.Add(area);
        await context.SaveChangesAsync();

        return area.AreaId;
    }

    private async Task<int> SeedDiscountAsync(string code, decimal? percentage, decimal? fixedAmount)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var discount = new Discount
        {
            DiscountCode = code,
            Description = $"{code} discount",
            Percentage = percentage ?? 0m,
            FixedAmount = fixedAmount,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        context.Discounts.Add(discount);
        await context.SaveChangesAsync();

        return discount.DiscountId;
    }

    private static BookingPayload BookingFor(
        int customerId,
        int? packageId,
        string venue,
        DateOnly eventDate,
        int noOfPerson = 120,
        decimal? pricePerHead = null,
        bool confirm = true,
        int? extendedAreaId = null,
        bool applyExtendedAmount = false,
        string? discountCode = null,
        decimal? depositAmount = null,
        decimal? initialPayment = null,
        TimeSpan? callTime = null,
        TimeSpan? packoutTime = null) =>
        new BookingPayload(
            customerId,
            noOfPerson,
            pricePerHead ?? 820m,
            Occasion: "Integration Event",
            Venue: venue,
            PackageId: packageId)
        {
            StartDate = eventDate.ToDateTime(new TimeOnly(17, 0)),
            EndDate = eventDate.ToDateTime(new TimeOnly(22, 0)),
            Confirm = confirm,
            ExtendedAreaId = extendedAreaId,
            ApplyExtendedAmount = applyExtendedAmount,
            DiscountCode = discountCode,
            DepositAmount = depositAmount,
            CallTime = callTime,
            PackoutTime = packoutTime,
            InitialPayment = initialPayment.HasValue
                ? new InitialPaymentPayload(
                    AmountPaid: initialPayment.Value,
                    PaymentMeans: "Check",
                    CheckNo: "CK-2026-1",
                    Particular: "Downpayment")
                : null
        };

    // ─────────────────────────── the tests ───────────────────────────

    [Fact]
    public async Task The_unfiltered_register_reconciles_with_the_operations_dashboard()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Reconcile");

        var venue = $"Register Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(45);

        await client.PostBookingAsync(BookingFor(customerId, null, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));
        await client.PostBookingAsync(BookingFor(customerId, null, venue, eventDate.AddDays(1), noOfPerson: 10, pricePerHead: 100m));

        var register = await client.GetJsonAsync($"/api/bookings?search={Uri.EscapeDataString(venue)}");
        var summary = await client.GetJsonAsync($"/api/bookings/summary?search={Uri.EscapeDataString(venue)}");
        var dashboard = await client.GetJsonAsync("/api/dashboard");

        var registerOutstanding = register.GetProperty("summary").GetProperty("outstandingAmount").GetDecimal();
        var summaryOutstanding = summary.GetProperty("receivables").GetProperty("outstandingAmount").GetDecimal();
        var dashboardOutstanding = dashboard.GetProperty("receivables").GetProperty("outstandingAmount").GetDecimal();

        registerOutstanding.Should().Be(2_000m, "the register's outstanding is the sum of the two bookings");
        summaryOutstanding.Should().Be(registerOutstanding, "the summary describes the same population");

        // The dashboard's receivables cover every non-cancelled booking, so the register's
        // figure can never exceed it — and the two reconcile when the register's population
        // is the whole book.
        dashboardOutstanding.Should().BeGreaterThanOrEqualTo(registerOutstanding);
    }

    [Fact]
    public async Task The_register_is_filtered_paged_and_faceted()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Facets");

        var venue = $"Facet Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);

        await client.PostBookingAsync(BookingFor(customerId, null, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));
        await client.PostBookingAsync(BookingFor(customerId, null, venue, eventDate.AddDays(2), noOfPerson: 10, pricePerHead: 100m));

        var register = await client.GetJsonAsync(
            $"/api/bookings?search={Uri.EscapeDataString(venue)}&page=1&pageSize=10");

        register.GetProperty("totalCount").GetInt32().Should().Be(2);
        register.GetProperty("page").GetInt32().Should().Be(1);
        register.GetProperty("pageSize").GetInt32().Should().Be(10);
        register.GetProperty("totalPages").GetInt32().Should().Be(1);
        register.GetProperty("items").GetArrayLength().Should().Be(2);
        register.GetProperty("summary").GetProperty("bookingCount").GetInt32().Should().Be(2);
        register.GetProperty("quickViewCounts").GetProperty("venues").GetArrayLength().Should().BeGreaterThan(0);

        // An unknown status name is a 400 — the vocabulary is never silently widened.
        var rejected = await client.GetAsync($"/api/bookings?status=NotAStatus");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.ReadErrorsAsync()).Should().Contain(
            "One or more of the booking statuses is not a known booking status.");
    }

    [Fact]
    public async Task The_quote_prices_the_booking_and_persists_nothing()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Quote");
        var packageId = await SeedTierAsync("Quote Buffet", 820m);
        var areaId = await SeedMunicipalityAsync("Quote Municipality", 50m);

        var before = await _fixture.QueryAsync(context => context.Bookings.CountAsync());

        var response = await client.PostAsJsonAsync("/api/bookings/quote", new
        {
            customerId,
            packageId,
            noOfPerson = 120,
            occasion = "Quote Event",
            venue = "Quote Hall",
            eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
            startTime = "17:00:00",
            endTime = "22:00:00",
            extendedAreaId = areaId,
            applyExtendedAmount = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        quote.GetProperty("baseAmount").GetDecimal().Should().Be(820m * 120);
        quote.GetProperty("extendedAreaAmount").GetDecimal().Should().Be(50m * 120);
        quote.GetProperty("grossAmount").GetDecimal().Should().Be(870m * 120);

        // BR-38: 120 heads falls in the 100-999 band, so the quote nets the automatic 20.00
        // per head and publishes the band it applied.
        quote.GetProperty("discountSource").GetString().Should().Be("HeadcountBand");
        quote.GetProperty("discountBandLabel").GetString().Should().Be("100 to 999 heads");
        quote.GetProperty("discountAmount").GetDecimal().Should().Be(2_400m);
        quote.GetProperty("netAmountDue").GetDecimal().Should().Be(870m * 120 - 2_400m);

        // The deposit and the capacity are configuration and absent by default.
        quote.GetProperty("requiredDepositPercentage").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        quote.GetProperty("requiredDepositAmount").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        quote.GetProperty("capacity").GetProperty("capacityPax").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        quote.GetProperty("capacity").GetProperty("isConfigured").GetBoolean().Should().BeFalse();

        // The tier-active check is the only blocking one.
        var checks = quote.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().HaveCount(5);
        checks.Should().ContainSingle(check => check.GetProperty("isBlocking").GetBoolean());

        var after = await _fixture.QueryAsync(context => context.Bookings.CountAsync());
        after.Should().Be(before, "the quote persists nothing");
    }

    [Fact]
    public async Task A_booking_is_created_with_a_surcharge_and_a_discount_and_its_frozen_columns_are_stored()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Surcharge");
        var packageId = await SeedTierAsync("Surcharge Buffet", 820m);
        var areaId = await SeedMunicipalityAsync("Surcharge Municipality", 50m);
        await SeedDiscountAsync("TENPERCENT", 10m, null);

        var venue = $"Surcharge Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(75);

        var response = await client.PostBookingAsync(BookingFor(
            customerId,
            packageId,
            venue,
            eventDate,
            extendedAreaId: areaId,
            applyExtendedAmount: true,
            discountCode: "TENPERCENT",
            depositAmount: 20_000m));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();
        body.GetProperty("bookingNumber").GetString().Should().MatchRegex("^BK-\\d{8}-\\d{5}$");
        body.GetProperty("status").GetString().Should().Be("Confirmed");

        // The frozen columns.
        var stored = await _fixture.QueryAsync(context => context.Bookings
            .AsNoTracking()
            .FirstAsync(booking => booking.TransactionId == transactionId));

        stored.ExtendedAreaAmount.Should().Be(50m * 120, "the surcharge is stored, not inferred");
        stored.DiscountCode.Should().Be("TENPERCENT");
        // The gross is 120 × (820 + 50) = 104,400; the percentage is charged on the gross.
        stored.DiscountAmount.Should().Be(10_440m, "the percentage is charged on the gross amount");
        stored.AmountDue.Should().Be(93_960m);
        stored.DepositAmount.Should().Be(20_000m);
        stored.CallTime.Should().BeNull();
        stored.PackoutTime.Should().BeNull();

        // The ledger publishes the same net amount due.
        var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
        ledger.GetProperty("contractAmount").GetDecimal().Should().Be(stored.AmountDue);
    }

    [Fact]
    public async Task A_draft_and_a_confirmed_booking_are_two_states_of_one_create()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Draft");
        var packageId = await SeedTierAsync("Draft Buffet", 500m);

        var venue = $"Draft Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90);

        var draft = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, confirm: false));

        draft.StatusCode.Should().Be(HttpStatusCode.Created, await draft.DescribeAsync());

        var draftBody = await draft.Content.ReadFromJsonAsync<JsonElement>();
        draftBody.GetProperty("status").GetString().Should().Be("Pending");

        var draftId = draftBody.GetProperty("transactionId").GetInt32();
        var draftPayments = await _fixture.QueryAsync(context =>
            context.Payments.CountAsync(payment => payment.TransactionId == draftId));
        draftPayments.Should().Be(0, "a draft never carries a payment");

        // A confirmed booking with an initial payment: the payment is committed with it.
        var confirmed = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate.AddDays(1), initialPayment: 10_000m));

        confirmed.StatusCode.Should().Be(HttpStatusCode.Created, await confirmed.DescribeAsync());

        var confirmedBody = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        confirmedBody.GetProperty("status").GetString().Should().Be("Confirmed");

        var confirmedId = confirmedBody.GetProperty("transactionId").GetInt32();

        var payments = await _fixture.QueryAsync(context => context.Payments
            .AsNoTracking()
            .Where(payment => payment.TransactionId == confirmedId)
            .ToListAsync());

        payments.Should().HaveCount(1, "the payment row and the booking are committed together");
        payments[0].AmountPaid.Should().Be(10_000m);
        payments[0].PaymentNo.Should().MatchRegex("^OR-\\d{8}-\\d{4}$");

        var ledger = await client.GetJsonAsync($"/api/bookings/{confirmedId}/ledger");
        ledger.GetProperty("totalPaid").GetDecimal().Should().Be(10_000m);
        ledger.GetProperty("payments").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task An_overpayment_is_refused_and_nothing_is_written()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Overpayment");
        var packageId = await SeedTierAsync("Overpay Buffet", 500m);

        var venue = $"Overpay Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(105);

        var before = await _fixture.QueryAsync(context => context.Bookings.CountAsync());
        var paymentsBefore = await _fixture.QueryAsync(context => context.Payments.CountAsync());

        var response = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m, initialPayment: 1_000.01m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain(
            "The initial payment cannot exceed the net amount due of the booking.");

        var after = await _fixture.QueryAsync(context => context.Bookings.CountAsync());
        after.Should().Be(before, "no booking row is created");

        var paymentsAfter = await _fixture.QueryAsync(context => context.Payments.CountAsync());
        paymentsAfter.Should().Be(paymentsBefore, "no payment row is created");
    }

    [Fact]
    public async Task A_payment_above_the_net_amount_due_is_refused_on_the_payment_path()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "OverpayPath");
        var packageId = await SeedTierAsync("OverpayPath Buffet", 500m);

        var venue = $"Overpay Path Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(110);

        var created = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();

        var response = await client.RecordPaymentAsync(transactionId, 1_000.01m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadErrorsAsync()).Should().Contain(
            "The payment would take the total collected above the net amount due of the booking.");

        (await _fixture.QueryAsync(context =>
            context.Payments.CountAsync(payment => payment.TransactionId == transactionId)))
            .Should().Be(0, "no payment row is written");
    }

    [Fact]
    public async Task The_contract_pdf_and_the_register_export_are_served()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Documents");
        var packageId = await SeedTierAsync("Document Buffet", 500m);

        var venue = $"Document Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(120);

        var created = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();

        var contract = await client.GetAsync($"/api/bookings/{transactionId}/contract");
        contract.StatusCode.Should().Be(HttpStatusCode.OK, await contract.DescribeAsync());
        contract.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await contract.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);

        var export = await client.GetAsync($"/api/bookings/export?search={Uri.EscapeDataString(venue)}");
        export.StatusCode.Should().Be(HttpStatusCode.OK, await export.DescribeAsync());
        export.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await export.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task The_migration_adds_the_four_booking_columns()
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        applied.Should().Contain(migration => migration.EndsWith("AddBookingsEventsRegister", StringComparison.Ordinal));

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        pending.Should().BeEmpty("the schema matches the model");
    }

    /// <summary>
    /// The overdue filter is a <b>tri-state</b> (BR-32): absent applies no filter,
    /// <c>true</c> selects the overdue bookings and <c>false</c> selects the bookings that are
    /// <b>not</b> overdue. An explicit <c>false</c> narrows the population — it is never
    /// silently treated as absent.
    /// </summary>
    [Fact]
    public async Task The_overdue_filter_is_a_tri_state()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Overdue");
        var packageId = await SeedTierAsync("Overdue Buffet", 100m);

        var venue = $"Overdue Hall {Guid.NewGuid():N}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, today.AddDays(-5), noOfPerson: 10, pricePerHead: 100m));
        await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, today.AddDays(20), noOfPerson: 10, pricePerHead: 100m));

        var search = Uri.EscapeDataString(venue);

        var all = await client.GetJsonAsync($"/api/bookings?search={search}");
        all.GetProperty("totalCount").GetInt32().Should().Be(2, "an absent overdue filter keeps both");

        var overdue = await client.GetJsonAsync($"/api/bookings?search={search}&overdue=true");
        overdue.GetProperty("totalCount").GetInt32().Should().Be(1);
        overdue.GetProperty("items").EnumerateArray().Single()
            .GetProperty("isOverdue").GetBoolean().Should().BeTrue();

        var notOverdue = await client.GetJsonAsync($"/api/bookings?search={search}&overdue=false");
        notOverdue.GetProperty("totalCount").GetInt32()
            .Should().Be(1, "an explicit false narrows the population");
        notOverdue.GetProperty("items").EnumerateArray().Single()
            .GetProperty("isOverdue").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// BR-34 (ratified): the dispatch schedule is <b>ordered</b> against the programme. The
    /// violation is a <c>400</c> with the server's own message, and nothing is written.
    /// </summary>
    [Fact]
    public async Task The_dispatch_schedule_must_be_ordered_against_the_programme()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Dispatch");
        var packageId = await SeedTierAsync("Dispatch Buffet", 500m);

        var venue = $"Dispatch Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(150);

        var before = await _fixture.QueryAsync(context => context.Bookings.CountAsync());

        // The programme runs 17:00 → 22:00.
        var lateCall = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m,
            callTime: new TimeSpan(18, 0, 0)));

        lateCall.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await lateCall.ReadErrorsAsync()).Should().Contain(
            "The call time must not be later than the programme's start time.");

        var earlyPackout = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m,
            packoutTime: new TimeSpan(21, 0, 0)));

        earlyPackout.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await earlyPackout.ReadErrorsAsync()).Should().Contain(
            "The pack-out time must not be earlier than the programme's end time.");

        var after = await _fixture.QueryAsync(context => context.Bookings.CountAsync());
        after.Should().Be(before, "nothing is written");

        // The ordered schedule is accepted.
        var accepted = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m,
            callTime: new TimeSpan(9, 0, 0), packoutTime: new TimeSpan(23, 0, 0)));

        accepted.StatusCode.Should().Be(HttpStatusCode.Created, await accepted.DescribeAsync());
    }

    /// <summary>
    /// The details endpoint must publish the dispatch schedule, the frozen surcharge and the
    /// frozen discount code. A declared-but-never-populated field is a silent wrong answer — a
    /// consumer would read <c>null</c> for a booking that carries the value.
    /// </summary>
    [Fact]
    public async Task The_details_endpoint_publishes_the_dispatch_schedule_the_surcharge_and_the_discount_code()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Details");
        var packageId = await SeedTierAsync("Details Buffet", 820m);
        var areaId = await SeedMunicipalityAsync("Details Municipality", 50m);

        // The discount code must be unique in the shared database — disc_code carries no unique
        // index, so a second active discount with the same code is an ambiguous code (BR-02).
        var discountCode = $"DETAILS{Guid.NewGuid():N}"[..20];
        await SeedDiscountAsync(discountCode, 10m, null);

        var venue = $"Details Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(160);

        var response = await client.PostBookingAsync(BookingFor(
            customerId,
            packageId,
            venue,
            eventDate,
            extendedAreaId: areaId,
            applyExtendedAmount: true,
            discountCode: discountCode,
            depositAmount: 20_000m,
            callTime: new TimeSpan(14, 0, 0),
            packoutTime: new TimeSpan(23, 0, 0)));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();

        var details = await client.GetBookingDetailsAsync(transactionId);

        details.GetProperty("callTime").GetString().Should().Be("14:00:00");
        details.GetProperty("packoutTime").GetString().Should().Be("23:00:00");
        details.GetProperty("discountCode").GetString().Should().Be(discountCode);
        details.GetProperty("extendedAreaAmount").GetDecimal()
            .Should().Be(50m * 120, "the frozen surcharge, not the municipality's current rate");
        details.GetProperty("depositAmount").GetDecimal().Should().Be(20_000m);
    }

    /// <summary>
    /// The details endpoint and the register describe <b>the same booking the same way</b>: the
    /// net amount due, the collected amount, the outstanding balance, the surcharge, the deposit
    /// and the status are identical on both.
    /// </summary>
    [Fact]
    public async Task The_details_endpoint_and_the_register_publish_identical_figures()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Parity");
        var packageId = await SeedTierAsync("Parity Buffet", 820m);
        var areaId = await SeedMunicipalityAsync("Parity Municipality", 50m);

        var venue = $"Parity Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(170);

        var created = await client.PostBookingAsync(BookingFor(
            customerId,
            packageId,
            venue,
            eventDate,
            extendedAreaId: areaId,
            applyExtendedAmount: true,
            depositAmount: 20_000m));

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();

        var payment = await client.RecordPaymentAsync(transactionId, 30_000m);
        payment.StatusCode.Should().Be(HttpStatusCode.OK, await payment.DescribeAsync());

        var details = await client.GetBookingDetailsAsync(transactionId);
        var register = await client.GetJsonAsync($"/api/bookings?search={Uri.EscapeDataString(venue)}");
        var item = register.GetProperty("items").EnumerateArray().Single();

        details.GetProperty("amountDue").GetDecimal()
            .Should().Be(item.GetProperty("netAmountDue").GetDecimal(), "the same net amount due on both");
        details.GetProperty("amountPaid").GetDecimal()
            .Should().Be(item.GetProperty("amountPaid").GetDecimal(), "the same collected amount on both");
        details.GetProperty("balance").GetDecimal()
            .Should().Be(item.GetProperty("outstandingBalance").GetDecimal(), "the same outstanding balance on both");
        details.GetProperty("extendedAreaAmount").GetDecimal()
            .Should().Be(item.GetProperty("extendedAreaAmount").GetDecimal(), "the same surcharge on both");
        details.GetProperty("depositAmount").GetDecimal()
            .Should().Be(item.GetProperty("depositAmount").GetDecimal(), "the same deposit on both");
        details.GetProperty("status").GetString()
            .Should().Be(item.GetProperty("status").GetString(), "the same lifecycle status on both");
    }

    /// <summary>
    /// The unified soft-deleted-payment exclusion (DEC-111, BR-27, AC-BKG-47). A soft-deleted
    /// payment is excluded from <b>every</b> money surface and from the listed ledger entries —
    /// it is not money the business holds and not a receipt the customer may see.
    /// </summary>
    [Fact]
    public async Task A_soft_deleted_payment_is_excluded_from_every_money_surface()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "SoftDelete");
        var packageId = await SeedTierAsync("SoftDelete Buffet", 100m);

        var venue = $"Soft Delete Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(140);

        var created = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = body.GetProperty("transactionId").GetInt32();

        var payment = await client.RecordPaymentAsync(transactionId, 400m);
        payment.StatusCode.Should().Be(HttpStatusCode.OK, await payment.DescribeAsync());

        // The payment is soft-deleted behind the API's back.
        using (var scope = _fixture.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
            var stored = await context.Payments.FirstAsync(p => p.TransactionId == transactionId);
            stored.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        // The booking details.
        var details = await client.GetBookingDetailsAsync(transactionId);
        details.GetProperty("amountPaid").GetDecimal().Should().Be(0m, "a soft-deleted payment is not money the business holds");
        details.GetProperty("balance").GetDecimal().Should().Be(1_000m);

        // The ledger — the sum AND the listed entries.
        var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
        ledger.GetProperty("totalPaid").GetDecimal().Should().Be(0m);
        ledger.GetProperty("balanceDue").GetDecimal().Should().Be(1_000m);
        ledger.GetProperty("payments").GetArrayLength().Should().Be(0, "a soft-deleted payment is not listed either");

        // The register.
        var register = await client.GetJsonAsync($"/api/bookings?search={Uri.EscapeDataString(venue)}");
        var item = register.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("amountPaid").GetDecimal().Should().Be(0m);
        item.GetProperty("outstandingBalance").GetDecimal().Should().Be(1_000m);

        // The customer surfaces.
        var customer = await client.GetJsonAsync($"/api/customers/{customerId}");
        customer.GetProperty("totalPaid").GetDecimal().Should().Be(0m);
        customer.GetProperty("outstandingBalance").GetDecimal().Should().Be(1_000m);
    }

    /// <summary>
    /// The authorization is two-level (BR-12, contract §14.4): a draft needs only an
    /// authenticated user, but a booking that carries an initial payment additionally requires
    /// the <c>Payments.Record</c> policy. The Accountant role holds no payment policy.
    /// </summary>
    [Fact]
    public async Task A_booking_that_carries_an_initial_payment_should_require_the_payments_record_policy()
    {
        var client = await CreateAccountantClientAsync();
        var customerId = await client.CreateCustomerAsync("Register", "Authorization");
        var packageId = await SeedTierAsync("Authorization Buffet", 500m);

        var venue = $"Authorization Hall {Guid.NewGuid():N}";
        var eventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(130);

        // A confirmed booking WITHOUT an initial payment needs only an authenticated user.
        var allowed = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate, noOfPerson: 10, pricePerHead: 100m));

        allowed.StatusCode.Should().Be(HttpStatusCode.Created, await allowed.DescribeAsync());

        // A draft needs only an authenticated user too.
        var draft = await client.PostBookingAsync(BookingFor(
            customerId, packageId, venue, eventDate.AddDays(1), noOfPerson: 10, pricePerHead: 100m, confirm: false));

        draft.StatusCode.Should().Be(HttpStatusCode.Created, await draft.DescribeAsync());

        // A booking that carries an initial payment needs the Payments.Record policy.
        var refused = await client.PostBookingAsync(BookingFor(
            customerId,
            packageId,
            venue,
            eventDate.AddDays(2),
            noOfPerson: 10,
            pricePerHead: 100m,
            initialPayment: 500m));

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.ReadErrorsAsync()).Should().Contain("You are not authorized to perform this action.");
    }

    /// <summary>
    /// Provisions an Accountant — a role that holds no payment policy — and returns an
    /// authenticated client for it.
    /// </summary>
    private async Task<HttpClient> CreateAccountantClientAsync()
    {
        const string userName = "accountant.integration@salscatersuite.test";
        const string password = "Accountant@12345";

        using (var scope = _fixture.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByNameAsync(userName);

            if (user is null)
            {
                var created = await userManager.CreateAsync(new ApplicationUser
                {
                    UserName = userName,
                    Email = userName,
                    EmailConfirmed = true,
                    IsActive = true,
                    Firstname = "Integration",
                    Lastname = "Accountant",
                    DateCreated = DateTime.UtcNow
                }, password);

                created.Succeeded.Should().BeTrue(
                    $"the accountant account must be provisioned: {string.Join("; ", created.Errors.Select(e => e.Description))}");

                user = await userManager.FindByNameAsync(userName);
            }

            user.Should().NotBeNull("the accountant account exists after the provisioning");

            var added = await userManager.AddToRoleAsync(user!, "Accountant");
            added.Succeeded.Should().BeTrue();
        }

        return await _fixture.CreateAuthenticatedClientAsync(userName, password);
    }
}
