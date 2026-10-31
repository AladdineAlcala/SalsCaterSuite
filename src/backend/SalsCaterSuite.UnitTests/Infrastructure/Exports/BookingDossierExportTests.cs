namespace SalsCaterSuite.UnitTests.Infrastructure.Exports;

using System.IO;
using ClosedXML.Excel;
using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Exports;
using Xunit;

/// <summary>
/// The Bookings &amp; Events Register export (contract §14.6). The export carries the whole
/// filtered population and every figure in it is the one the register published.
/// </summary>
public class BookingDossierExportTests
{
    private readonly BookingRegisterExport _export = new();

    private static BookingRegisterItemResponse SampleItem() => new()
    {
        TransactionId = 101,
        BookingNumber = "BK-20261120-00101",
        EventDate = new DateOnly(2026, 11, 20),
        EventStartTime = new TimeSpan(17, 0, 0),
        EventEndTime = new TimeSpan(22, 0, 0),
        CallTime = new TimeSpan(9, 0, 0),
        PackoutTime = new TimeSpan(23, 0, 0),
        Status = BookingStatus.Confirmed,
        PaymentState = "PartiallyPaid",
        IsOverdue = false,
        CustomerName = "Santos, Maria",
        CustomerCompany = "Sal's Corporation",
        CustomerContact = "09171234567",
        Occasion = "Silver Wedding Anniversary",
        Venue = "Grand Ballroom",
        PackageName = "Premier Buffet Tier",
        PackageType = "Premier Buffet",
        NumberOfHeads = 120,
        PricePerHead = 820m,
        PackageAmount = 98_400m,
        NetAmountDue = 104_400m,
        AmountPaid = 30_000m,
        OutstandingBalance = 74_400m,
        ExtendedAreaAmount = 6_000m,
        DepositAmount = 30_000m,
        DiscountCode = "TEN",
        DiscountAmount = 0m
    };

    private static BookingRegisterTotalsResponse SampleTotals() => new()
    {
        BookingCount = 1,
        NetContractedValue = 104_400m,
        CollectedAmount = 30_000m,
        OutstandingAmount = 74_400m,
        TotalPax = 120
    };

    [Fact]
    public void The_export_should_carry_the_header_and_the_row()
    {
        var bytes = _export.Generate(new[] { SampleItem() }, SampleTotals());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        worksheet.Cell(4, 1).GetString().Should().Be("Booking Number");
        worksheet.Cell(4, 20).GetString().Should().Be("Net Amount Due");

        worksheet.Cell(5, 1).GetString().Should().Be("BK-20261120-00101");
        worksheet.Cell(5, 7).GetString().Should().Be(nameof(BookingStatus.Confirmed));
        worksheet.Cell(5, 8).GetString().Should().Be("PartiallyPaid");
        worksheet.Cell(5, 10).GetString().Should().Be("Santos, Maria");
        worksheet.Cell(5, 17).GetValue<int>().Should().Be(120);
        worksheet.Cell(5, 20).GetValue<decimal>().Should().Be(104_400m);
        worksheet.Cell(5, 21).GetValue<decimal>().Should().Be(30_000m);
        worksheet.Cell(5, 22).GetValue<decimal>().Should().Be(74_400m);
    }

    [Fact]
    public void The_export_should_carry_the_totals_row()
    {
        var bytes = _export.Generate(new[] { SampleItem() }, SampleTotals());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        worksheet.Cell(6, 1).GetString().Should().Be("TOTALS:");
        worksheet.Cell(6, 17).GetValue<int>().Should().Be(120);
        worksheet.Cell(6, 20).GetValue<decimal>().Should().Be(104_400m);
        worksheet.Cell(6, 21).GetValue<decimal>().Should().Be(30_000m);
        worksheet.Cell(6, 22).GetValue<decimal>().Should().Be(74_400m);
    }

    [Fact]
    public void The_export_of_an_empty_population_should_still_carry_the_header()
    {
        var bytes = _export.Generate(Array.Empty<BookingRegisterItemResponse>(), new BookingRegisterTotalsResponse());

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        worksheet.Cell(4, 1).GetString().Should().Be("Booking Number");
        worksheet.Cell(5, 1).IsEmpty().Should().BeTrue("an empty population writes no row");
    }
}
