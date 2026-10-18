using ClosedXML.Excel;
using SalsCaterSuite.Application.Features.Payments.DTOs;

namespace SalsCaterSuite.Infrastructure.Exports;

public class CollectionSummaryExport
{
    public byte[] Generate(DateTime startDate, DateTime endDate, IEnumerable<PaymentEntryDto> payments)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Collection Summary");

        worksheet.Cell(1, 1).Value = "SAL'S CATERING SERVICES";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = $"Cash & Collection Summary: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}";
        worksheet.Cell(2, 1).Style.Font.Bold = true;

        int row = 4;
        string[] headers = { "Receipt #", "TRN #", "Date", "Particular", "Means", "Check #", "Amount (PHP)", "Cashier" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
            cell.Style.Font.FontColor = XLColor.White;
        }

        row++;
        foreach (var p in payments)
        {
            worksheet.Cell(row, 1).Value = p.PaymentNo;
            worksheet.Cell(row, 2).Value = p.TransactionId;
            worksheet.Cell(row, 3).Value = p.DateOfPayment?.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 4).Value = p.Particular;
            worksheet.Cell(row, 5).Value = p.PaymentMeans;
            worksheet.Cell(row, 6).Value = p.CheckNo ?? "-";
            worksheet.Cell(row, 7).Value = p.AmountPaid;
            worksheet.Cell(row, 8).Value = p.CreatedByUser ?? "N/A";

            worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        worksheet.Cell(row, 6).Value = "TOTAL COLLECTION:";
        worksheet.Cell(row, 6).Style.Font.Bold = true;
        worksheet.Cell(row, 7).FormulaA1 = $"SUM(G5:G{row - 1})";
        worksheet.Cell(row, 7).Style.Font.Bold = true;
        worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

