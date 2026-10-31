namespace SalsCaterSuite.UnitTests.Infrastructure.Documents;

using FluentAssertions;
using QuestPDF.Fluent;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Documents;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The printable catering contract (contract §14.5, DEC-113, BR-26, BR-34).
/// <para>
/// The document is composed from the DTO the <b>one</b> booking-details path produces, so its
/// figures are the shared calculator's. The itemisation is <b>additive</b> — a line exists only
/// when the booking carries the charge, and a zero charge is <b>not</b> a "0.00" row (BR-20's
/// null/zero discipline).
/// </para>
/// <para>
/// The assertions are made against the document's <b>content model</b> — the service lines, the
/// totals and the dispatch schedule — which <c>Compose</c> renders verbatim; QuestPDF writes the
/// glyphs of its subset font, so the rendered bytes are not text-searchable without a
/// ToUnicode CMap parser, which this repository deliberately does not carry. A render smoke
/// test proves the PDF is produced.
/// </para>
/// </summary>
public class ContractDocumentTests
{
    private static readonly DateOnly Today = new(2026, 10, 22);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly GetBookingDetailsQuery _details;

    public ContractDocumentTests()
    {
        _bookings = new FakeBookingRepository(_store);
        _details = new GetBookingDetailsQuery(_bookings, new BookingAmountDueCalculator());
    }

    /// <summary>
    /// Builds a booking and projects it through the <b>real</b> booking-details path, so the
    /// document is composed from the same DTO the API serves.
    /// </summary>
    private async Task<(Booking Booking, BookingResponse Dto)> SeedAsync(
        decimal pricePerHead = 500m,
        int heads = 100,
        decimal extendedAreaAmount = 0m,
        string? discountCode = null,
        decimal discountAmount = 0m,
        TimeSpan? callTime = null,
        TimeSpan? packoutTime = null)
    {
        _store.Customers.Add(new Customer { CustomerId = 1, FirstName = "Maria", LastName = "Santos" });

        var eventDate = Today.AddDays(30);

        var booking = Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: eventDate.ToDateTime(new TimeOnly(17, 0)),
            endDate: eventDate.ToDateTime(new TimeOnly(22, 0)),
            occasion: "Silver Wedding Anniversary",
            venue: "Grand Ballroom",
            packageName: "Premier Buffet Tier",
            pricePerHead: pricePerHead,
            callTime: callTime,
            packoutTime: packoutTime,
            discountCode: discountCode,
            extendedAreaAmount: extendedAreaAmount);

        if (discountAmount > 0m)
        {
            booking.DiscountType = DiscountType.Percentage;
            booking.DiscountPercentage = 10m;
        }

        booking.RecalculateFinancials(extendedAreaAmount, 0m, discountAmount, 0m);

        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();
        booking.AssignBookingNumber();

        var dto = (await _details.ExecuteAsync(booking.TransactionId)).Data!;

        return (booking, dto);
    }

    [Fact]
    public async Task The_surcharge_line_should_appear_for_a_booking_that_has_one()
    {
        var (_, dto) = await SeedAsync(extendedAreaAmount: 5_000m, callTime: new TimeSpan(14, 0, 0), packoutTime: new TimeSpan(23, 0, 0));

        var document = new ContractDocument(dto);
        var lines = document.BuildServiceLines();

        var surcharge = lines.Should().ContainSingle(line => line.Description == "Out-of-town surcharge").Subject;
        surcharge.Total.Should().Be(5_000m);
        surcharge.Pax.Should().Be(100, "the headcount is shown so the derivation is visible");
        surcharge.RatePerPax.Should().Be(50m, "the per-guest rate the frozen amount was derived from");

        // The package row is still there, and the surcharge is an ADDITIONAL row.
        lines.Should().HaveCount(2);
        lines[0].Description.Should().Be("Premier Buffet Tier");
    }

    [Fact]
    public async Task The_surcharge_line_should_be_absent_for_a_booking_that_has_none()
    {
        var (_, dto) = await SeedAsync();

        var document = new ContractDocument(dto);
        var lines = document.BuildServiceLines();

        lines.Should().ContainSingle();
        lines.Should().NotContain(line => line.Description == "Out-of-town surcharge");
        lines[0].Description.Should().Be("Premier Buffet Tier");
    }

    [Fact]
    public async Task The_discount_line_should_carry_the_code_and_be_absent_when_no_discount_was_applied()
    {
        var (withDiscount, withDiscountDto) = await SeedAsync(
            extendedAreaAmount: 5_000m,
            discountCode: "TENPERCENT",
            discountAmount: 5_500m);

        var discounted = new ContractDocument(withDiscountDto).BuildTotalLines();

        var discount = discounted.Should().ContainSingle(line => line.Label.StartsWith("Discount", StringComparison.Ordinal)).Subject;
        discount.Label.Should().Be("Discount (TENPERCENT)", "the frozen code is what makes the booking auditable");
        discount.Amount.Should().Be(5_500m);

        var (_, withoutDiscountDto) = await SeedAsync(pricePerHead: 700m);
        var undiscounted = new ContractDocument(withoutDiscountDto).BuildTotalLines();

        undiscounted.Should().NotContain(line => line.Label.StartsWith("Discount", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_contract_total_should_equal_the_shared_calculators_net_amount_due()
    {
        var (booking, dto) = await SeedAsync(
            extendedAreaAmount: 5_000m,
            discountCode: "TENPERCENT",
            discountAmount: 5_500m);

        var document = new ContractDocument(dto);
        var totals = document.BuildTotalLines();

        var expected = new BookingAmountDueCalculator()
            .Calculate(booking, BookingPaymentPopulation.Collected(booking))
            .NetAmountDue;

        totals.Should().ContainSingle(line => line.Label == "Contract Amount")
            .Which.Amount.Should().Be(expected, "the printed total is the shared calculator's figure (DEC-113)");
        totals.Should().ContainSingle(line => line.Label == "Contract Amount")
            .Which.Amount.Should().Be(dto.AmountDue);

        // The charge, the discount and the net tie out.
        var totalCharges = totals.Should().ContainSingle(line => line.Label == "Total Charges").Which.Amount;
        var discount = totals.Should().ContainSingle(line => line.Label.StartsWith("Discount", StringComparison.Ordinal)).Which.Amount;

        (totalCharges - discount).Should().Be(expected);
    }

    [Fact]
    public async Task The_dispatch_schedule_should_be_published_only_when_both_times_are_stated()
    {
        var (_, both) = await SeedAsync(callTime: new TimeSpan(14, 0, 0), packoutTime: new TimeSpan(23, 0, 0));
        var document = new ContractDocument(both);

        var schedule = document.BuildDispatchScheduleText();
        schedule.Should().NotBeNullOrWhiteSpace();
        schedule.Should().Contain("14:00");
        schedule.Should().Contain("23:00");

        var (_, callOnly) = await SeedAsync(pricePerHead: 600m, callTime: new TimeSpan(14, 0, 0));
        new ContractDocument(callOnly).BuildDispatchScheduleText().Should().BeNull();

        var (_, neither) = await SeedAsync(pricePerHead: 700m);
        new ContractDocument(neither).BuildDispatchScheduleText().Should().BeNull();
    }

    /// <summary>
    /// The dispatch schedule belongs to the <b>event's particulars</b> and is printed <b>once</b>,
    /// there — the repeating page header carries the document's identity, not one booking's
    /// schedule (BR-34).
    /// </summary>
    [Fact]
    public async Task The_dispatch_schedule_should_belong_to_the_particulars_and_not_to_the_page_header()
    {
        var (_, dto) = await SeedAsync(callTime: new TimeSpan(14, 0, 0), packoutTime: new TimeSpan(23, 0, 0));
        var document = new ContractDocument(dto);

        var particulars = document.BuildParticularsLines();

        particulars.Should().Contain(line => line.Text.Contains("Dispatch Schedule", StringComparison.Ordinal));
        particulars.Should().Contain(line => line.Text.Contains("Serving Time", StringComparison.Ordinal));
        particulars.Should().Contain(line => line.Text.Contains("Guaranteed Pax", StringComparison.Ordinal));

        // The repeating page header is the document's identity — never a booking's schedule.
        ContractDocument.HeaderLines.Should().HaveCount(2);
        ContractDocument.HeaderLines.Should().NotContain(
            line => line.Contains("Dispatch", StringComparison.OrdinalIgnoreCase));

        // An unstated time omits the line entirely.
        var (_, without) = await SeedAsync(pricePerHead: 700m);
        new ContractDocument(without).BuildParticularsLines()
            .Should().NotContain(line => line.Text.Contains("Dispatch Schedule", StringComparison.Ordinal));
    }

    /// <summary>
    /// The wording of a line that already exists on the documents the business signs is
    /// <b>preserved exactly</b>: <c>Guaranteed Pax: {N} Persons</c> — the wording the kitchen's
    /// event order has always printed, unit included. The wording itself is the contract, so
    /// this test fails if someone renames it.
    /// </summary>
    [Fact]
    public async Task The_particulars_should_carry_the_guaranteed_pax_wording_exactly()
    {
        var (_, dto) = await SeedAsync(heads: 150);

        var particulars = new ContractDocument(dto).BuildParticularsLines();

        particulars.Should().ContainSingle(line => line.Text.Contains("Guaranteed Pax", StringComparison.Ordinal))
            .Which.Text.Should().Be("Guaranteed Pax: 150 Persons");

        // The headcount is carried ONCE — the service-details table's Pax column is the
        // itemisation; the particulars block is the summary.
        particulars.Should().ContainSingle(line =>
            line.Text.Contains("Pax", StringComparison.Ordinal) || line.Text.Contains("Headcount", StringComparison.Ordinal));
        particulars.Should().NotContain(line => line.Text.StartsWith("Headcount:", StringComparison.Ordinal));
    }

    /// <summary>
    /// "This cycle <b>adds</b>, it does not <b>rewrite</b>." For a booking with no surcharge, no
    /// discount and no dispatch schedule, the particulars block is <b>textually identical</b> to
    /// the block the documents already carried — the same five lines, in the same order, with the
    /// same wording.
    /// </summary>
    [Fact]
    public async Task The_particulars_should_be_unchanged_for_a_booking_with_no_charge_and_no_schedule()
    {
        var (booking, dto) = await SeedAsync(heads: 150);

        var particulars = new ContractDocument(dto).BuildParticularsLines();

        // The Event Date is rendered with the same format string and the same ambient culture
        // the documents have always used — the wording is what is pinned, not the date format.
        particulars.Select(line => line.Text).Should().Equal(
            $"Event Date: {booking.EventDate:MMMM dd, yyyy}",
            "Occasion: Silver Wedding Anniversary",
            "Venue: Grand Ballroom",
            "Serving Time: 17:00 - 22:00",
            "Guaranteed Pax: 150 Persons");

        // And nothing else is added to that block.
        particulars.Should().HaveCount(5);
    }

    /// <summary>
    /// The recovered per-guest rate must reconstruct the frozen amount exactly. When the
    /// division does not, the rate is <c>null</c> and the document prints the amount and the
    /// headcount <b>without</b> a rate — never a rate that does not tie out (a signed document
    /// must not carry an arithmetic error).
    /// </summary>
    [Fact]
    public async Task A_surcharge_that_does_not_divide_evenly_should_publish_no_rate()
    {
        // 100.00 over 3 guests: 100 / 3 does not reconstruct 100.00 at two decimals.
        var (_, dto) = await SeedAsync(pricePerHead: 100m, heads: 3, extendedAreaAmount: 100m);

        dto.ExtendedAreaRatePerGuest.Should().BeNull("a rate that does not tie out is never published");

        var document = new ContractDocument(dto);
        var surcharge = document.BuildServiceLines()
            .Should().ContainSingle(line => line.Description == "Out-of-town surcharge").Subject;

        surcharge.Total.Should().Be(100m, "the amount is still published");
        surcharge.Pax.Should().Be(3, "the headcount is still published");
        surcharge.RatePerPax.Should().BeNull("the rate is omitted rather than shown wrong");
    }

    [Fact]
    public async Task A_surcharge_that_divides_evenly_should_publish_the_rate()
    {
        var (_, dto) = await SeedAsync(pricePerHead: 100m, heads: 120, extendedAreaAmount: 6_000m);

        dto.ExtendedAreaRatePerGuest.Should().Be(50m);

        new ContractDocument(dto).BuildServiceLines()
            .Should().ContainSingle(line => line.Description == "Out-of-town surcharge")
            .Which.RatePerPax.Should().Be(50m);
    }

    [Fact]
    public async Task The_document_should_render_a_pdf()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var (_, dto) = await SeedAsync(extendedAreaAmount: 5_000m, discountCode: "TENPERCENT", discountAmount: 5_500m);

        var bytes = new ContractDocument(dto).GeneratePdf();

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.Latin1.GetString(bytes, 0, 5).Should().Be("%PDF-", "the document renders");
    }
}
