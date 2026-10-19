using ClosedXML.Excel;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Infrastructure.Exports;

public class CateringMonthlyReportExport
{
    public byte[] Generate(int year, int month, IEnumerable<BookingDto> bookings)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Monthly Catering Report");

        // Header
        worksheet.Cell(1, 1).Value = "SAL'S CATERING SERVICES";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = $"Monthly Catering Sales & Operational Report - {new DateTime(year, month, 1):MMMM yyyy}";
        worksheet.Cell(2, 1).Style.Font.Bold = true;

        // Table headers
        int row = 4;
        string[] headers = { "TRN #", "Date", "Customer", "Occasion", "Venue", "Package", "Pax", "Contract Amount", "Total Paid", "Balance", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            cell.Style.Font.FontColor = XLColor.White;
        }

        row++;
        foreach (var b in bookings)
        {
            worksheet.Cell(row, 1).Value = b.TransactionId;
            worksheet.Cell(row, 2).Value = b.EventDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 3).Value = b.CustomerName;
            worksheet.Cell(row, 4).Value = b.Occasion;
            worksheet.Cell(row, 5).Value = b.Venue;
            worksheet.Cell(row, 6).Value = b.PackageName;
            worksheet.Cell(row, 7).Value = b.NumberOfHeads;
            worksheet.Cell(row, 8).Value = b.PackageAmount;
            worksheet.Cell(row, 9).Value = b.AmountDue;
            worksheet.Cell(row, 10).Value = b.Balance;
            worksheet.Cell(row, 11).Value = b.Status == BookingStatus.Cancelled ? "Cancelled" : (b.Status == BookingStatus.Completed ? "Completed" : "Confirmed");

            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

            row++;
        }

        // Totals
        worksheet.Cell(row, 7).Value = "TOTALS:";
        worksheet.Cell(row, 7).Style.Font.Bold = true;
        worksheet.Cell(row, 8).FormulaA1 = $"SUM(H5:H{row - 1})";
        worksheet.Cell(row, 9).FormulaA1 = $"SUM(I5:I{row - 1})";
        worksheet.Cell(row, 10).FormulaA1 = $"SUM(J5:J{row - 1})";
        worksheet.Cell(row, 8).Style.Font.Bold = true;
        worksheet.Cell(row, 9).Style.Font.Bold = true;
        worksheet.Cell(row, 10).Style.Font.Bold = true;
        worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
        worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

