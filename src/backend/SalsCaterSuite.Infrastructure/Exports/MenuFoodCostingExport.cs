using ClosedXML.Excel;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

namespace SalsCaterSuite.Infrastructure.Exports;

/// <summary>
/// The Menu &amp; Food Costing Master export (spec §6.3).
/// <para>
/// The export is the <b>current filtered result set</b> — not the current page — and every figure
/// in it is the one the master endpoint returned; nothing is recomputed here. The cost figures
/// therefore travel with the same rounding the screen shows.
/// </para>
/// </summary>
public class MenuFoodCostingExport
{
    /// <summary>The worksheet title of the workbook.</summary>
    private const string WorksheetTitle = "Menu Food Costing";

    /// <summary>
    /// Renders the dishes as an XLSX workbook. The columns are the ones the approved API
    /// contract lists (spec §6.3): Dish Code, Dish Name, Course, Selling Price, Food Cost,
    /// Gross Profit, Food Cost %, Gross Margin %, Target %, Within Target, Notes.
    /// </summary>
    public byte[] Generate(IEnumerable<MenuMasterItemResponse> items)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetTitle);

        worksheet.Cell(1, 1).Value = "SAL'S CATERING SERVICES";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = "Menu & Food Costing Master";
        worksheet.Cell(2, 1).Style.Font.Bold = true;

        int row = 4;
        string[] headers =
        {
            "Dish Code", "Dish Name", "Course", "Selling Price", "Food Cost", "Gross Profit",
            "Food Cost %", "Gross Margin %", "Target %", "Within Target", "Notes"
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
        foreach (var item in items)
        {
            worksheet.Cell(row, 1).Value = item.MenuId;
            worksheet.Cell(row, 2).Value = item.MenuName;
            worksheet.Cell(row, 3).Value = item.CourseName ?? string.Empty;
            worksheet.Cell(row, 4).Value = item.SellingPrice;
            worksheet.Cell(row, 5).Value = item.FoodCost;
            worksheet.Cell(row, 6).Value = item.GrossProfit;
            worksheet.Cell(row, 7).Value = item.FoodCostPercentage;
            worksheet.Cell(row, 8).Value = item.GrossMarginPercentage;
            worksheet.Cell(row, 9).Value = item.TargetFoodCostPercentage;
            // The server-provided verdict — the workbook never re-derives it.
            worksheet.Cell(row, 10).Value = item.IsWithinTarget ? "Yes" : "No";
            worksheet.Cell(row, 11).Value = item.Note ?? string.Empty;

            worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";

            row++;
        }

        // The totals are only written when there is at least one row — an empty result set must
        // not produce an invalid SUM range.
        if (row > 5)
        {
            worksheet.Cell(row, 3).Value = "TOTALS:";
            worksheet.Cell(row, 3).Style.Font.Bold = true;
            worksheet.Cell(row, 4).FormulaA1 = $"SUM(D5:D{row - 1})";
            worksheet.Cell(row, 5).FormulaA1 = $"SUM(E5:E{row - 1})";
            worksheet.Cell(row, 6).FormulaA1 = $"SUM(F5:F{row - 1})";
            worksheet.Cell(row, 4).Style.Font.Bold = true;
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
