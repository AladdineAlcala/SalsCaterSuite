using ClosedXML.Excel;
using SalsCaterSuite.Application.Features.Bookings.DTOs;

namespace SalsCaterSuite.Infrastructure.Exports;

/// <summary>
/// The Bookings &amp; Events Register export (contract §14.6, spec §4.6).
/// <para>
/// The export is the <b>whole filtered population</b>, never the current page — it carries the
/// very same filters as the register endpoint. Every figure in it is the one the register
/// published; nothing is recomputed here.
/// </para>
/// </summary>
public class BookingRegisterExport
{
    public byte[] Generate(IEnumerable<BookingRegisterItemDto> bookings, BookingRegisterTotalsDto totals)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Bookings & Events");

        worksheet.Cell(1, 1).Value = "SAL'S CATERING SERVICES";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = "Bookings & Events Register";
        worksheet.Cell(2, 1).Style.Font.Bold = true;

        int row = 4;
        string[] headers =
        {
            "Booking Number", "Event Date", "Start", "End", "Call", "Pack-out",
            "Status", "Payment State", "Overdue", "Customer", "Company", "Contact",
            "Occasion", "Venue", "Tier", "Tier Type", "Pax", "Rate/Pax", "Package Amount",
            "Net Amount Due", "Amount Paid", "Outstanding", "Surcharge", "Deposit", "Discount Code", "Discount"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            cell.Style.Font.FontColor = XLColor.White;
        }

        row++;
        foreach (var booking in bookings)
        {
            worksheet.Cell(row, 1).Value = booking.BookingNumber;
            worksheet.Cell(row, 2).Value = booking.EventDate.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(row, 3).Value = booking.EventStartTime;
            worksheet.Cell(row, 4).Value = booking.EventEndTime;
            worksheet.Cell(row, 5).Value = booking.CallTime.HasValue
                ? booking.CallTime.Value
                : (TimeSpan?)null;
            worksheet.Cell(row, 6).Value = booking.PackoutTime.HasValue
                ? booking.PackoutTime.Value
                : (TimeSpan?)null;
            worksheet.Cell(row, 7).Value = booking.Status.ToString();
            worksheet.Cell(row, 8).Value = booking.PaymentState;
            worksheet.Cell(row, 9).Value = booking.IsOverdue ? "Yes" : "No";
            worksheet.Cell(row, 10).Value = booking.CustomerName;
            worksheet.Cell(row, 11).Value = booking.CustomerCompany ?? string.Empty;
            worksheet.Cell(row, 12).Value = booking.CustomerContact ?? string.Empty;
            worksheet.Cell(row, 13).Value = booking.Occasion ?? string.Empty;
            worksheet.Cell(row, 14).Value = booking.Venue ?? string.Empty;
            worksheet.Cell(row, 15).Value = booking.PackageName ?? string.Empty;
            worksheet.Cell(row, 16).Value = booking.PackageType ?? string.Empty;
            worksheet.Cell(row, 17).Value = booking.NumberOfHeads;
            worksheet.Cell(row, 18).Value = booking.PricePerHead;
            worksheet.Cell(row, 19).Value = booking.PackageAmount;
            worksheet.Cell(row, 20).Value = booking.NetAmountDue;
            worksheet.Cell(row, 21).Value = booking.AmountPaid;
            worksheet.Cell(row, 22).Value = booking.OutstandingBalance;
            worksheet.Cell(row, 23).Value = booking.ExtendedAreaAmount;
            worksheet.Cell(row, 24).Value = booking.DepositAmount;
            worksheet.Cell(row, 25).Value = booking.DiscountCode ?? string.Empty;
            worksheet.Cell(row, 26).Value = booking.DiscountAmount;

            for (int column = 18; column <= 26; column++)
            {
                worksheet.Cell(row, column).Style.NumberFormat.Format = "#,##0.00";
            }

            row++;
        }

        // The totals row is written only when there is at least one data row — an empty
        // result set must not produce an invalid SUM range.
        if (row > 5)
        {
            worksheet.Cell(row, 1).Value = "TOTALS:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;

            worksheet.Cell(row, 17).Value = totals.TotalPax;
            worksheet.Cell(row, 20).Value = totals.NetContractedValue;
            worksheet.Cell(row, 21).Value = totals.CollectedAmount;
            worksheet.Cell(row, 22).Value = totals.OutstandingAmount;

            worksheet.Cell(row, 17).Style.Font.Bold = true;
            worksheet.Cell(row, 20).Style.Font.Bold = true;
            worksheet.Cell(row, 21).Style.Font.Bold = true;
            worksheet.Cell(row, 22).Style.Font.Bold = true;

            for (int column = 18; column <= 26; column++)
            {
                worksheet.Cell(row, column).Style.NumberFormat.Format = "#,##0.00";
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
