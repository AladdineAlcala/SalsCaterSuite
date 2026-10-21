using ClosedXML.Excel;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace SalsCaterSuite.Infrastructure.Exports;

/// <summary>
/// The Customer Master directory export (spec §5.5).
/// <para>
/// The export is the <b>current filtered result set</b> — not the current page — and every
/// figure in it is the one the directory endpoint returned; nothing is recomputed here.
/// </para>
/// </summary>
public class CustomerDirectoryExport
{
    public byte[] Generate(IEnumerable<CustomerDirectoryItemDto> customers)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Customer Directory");

        worksheet.Cell(1, 1).Value = "SAL'S CATERING SERVICES";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = "Customer Master & Accounts Ledger";
        worksheet.Cell(2, 1).Style.Font.Bold = true;

        int row = 4;
        string[] headers =
        {
            "Customer ID", "Full Name", "Company", "Contact 1", "Contact 2", "Address",
            "Bookings", "Total Contracted Value", "Total Paid", "Outstanding Balance", "Ledger State"
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
        foreach (var customer in customers)
        {
            worksheet.Cell(row, 1).Value = customer.CustomerId;
            worksheet.Cell(row, 2).Value = customer.FullName;
            worksheet.Cell(row, 3).Value = customer.Company ?? string.Empty;
            worksheet.Cell(row, 4).Value = customer.Contact1 ?? string.Empty;
            worksheet.Cell(row, 5).Value = customer.Contact2 ?? string.Empty;
            worksheet.Cell(row, 6).Value = customer.Address ?? string.Empty;
            worksheet.Cell(row, 7).Value = customer.BookingCounts?.Total ?? 0;
            worksheet.Cell(row, 8).Value = customer.TotalContractedValue;
            worksheet.Cell(row, 9).Value = customer.TotalPaid;
            worksheet.Cell(row, 10).Value = customer.OutstandingBalance;
            // The server-provided display label of the aging state (DEC-052) — the client
            // never derives a label of its own.
            worksheet.Cell(row, 11).Value = customer.LedgerStateLabel;

            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

            row++;
        }

        // The totals are only written when there is at least one row — an empty result set
        // must not produce an invalid SUM range.
        if (row > 5)
        {
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
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
