using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Infrastructure.Documents;

/// <summary>
/// The printable specification sheet of a buffet tier (REQ-PKG-02, contract §13.8).
/// <para>
/// It is rendered from the very same two payloads the workbench screens read — the course ruleset
/// and the coverage matrix — so the printed document and the screen can never describe two
/// different tiers. Every figure on it is a <b>server</b> figure; nothing is recomputed here
/// (DEC-002).
/// </para>
/// <para>
/// A figure the server published as <c>null</c> is printed as an em dash, never as a zero
/// (contract §9.2 rule 6).
/// </para>
/// </summary>
public class PackageSpecSheetDocument : IDocument
{
    private readonly PackageSpecSheetModel _model;

    public PackageSpecSheetDocument(PackageSpecSheetModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Package Specification Sheet - {_model.Ruleset.PackageName}",
        Author = "Sal's CaterSuite",
        Subject = "Package configuration specification"
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        var ruleset = _model.Ruleset;
        var coverage = _model.Coverage;

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            page.Header().Column(col =>
            {
                col.Item().Text("SAL'S CATERING SERVICES & EVENT SUITE")
                    .Bold().FontSize(15).FontColor(Colors.Blue.Darken3);
                col.Item().Text("PACKAGE CONFIGURATION SPECIFICATION SHEET")
                    .Bold().FontSize(11).FontColor(Colors.Grey.Darken2);
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                // ── the identity and the commercial terms ──
                col.Item().PaddingBottom(5).Text("PACKAGE IDENTITY & COMMERCIAL TERMS").Bold().FontSize(10);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Cell().Padding(4).Text("Tier").SemiBold();
                    table.Cell().Padding(4).Text(ruleset.PackageName);
                    table.Cell().Padding(4).Text("Type").SemiBold();
                    table.Cell().Padding(4).Text(ruleset.PackageType);

                    table.Cell().Padding(4).Text("Rate per guest").SemiBold();
                    table.Cell().Padding(4).Text(Money(ruleset.AmountPax));
                    table.Cell().Padding(4).Text("Minimum pax").SemiBold();
                    table.Cell().Padding(4).Text(ruleset.MinPax.HasValue ? ruleset.MinPax.Value.ToString() : EmDash);

                    table.Cell().Padding(4).Text("Minimum contract value").SemiBold();
                    table.Cell().Padding(4).Text(Money(ruleset.MinimumContractValue));
                    table.Cell().Padding(4).Text("Status").SemiBold();
                    table.Cell().Padding(4).Text(ruleset.IsActive ? "Active" : "Inactive");

                    table.Cell().Padding(4).Text("Cost ceiling per pax").SemiBold();
                    table.Cell().Padding(4).Text(Money(ruleset.CostCeilingPerPax));
                    table.Cell().Padding(4).Text("Target food cost").SemiBold();
                    table.Cell().Padding(4).Text($"{ruleset.TargetFoodCostPercentage:0.##}%");

                    table.Cell().Padding(4).Text("Covered municipalities").SemiBold();
                    table.Cell().Padding(4).Text(coverage.CoverageCount.ToString());
                    table.Cell().Padding(4).Text("Active coverage").SemiBold();
                    table.Cell().Padding(4).Text(coverage.ActiveCoverageCount.ToString());
                });

                // ── the planning forecast ──
                col.Item().PaddingTop(12).PaddingBottom(5)
                    .Text("PLANNING FORECAST (STANDARD RUN)").Bold().FontSize(10);

                col.Item().Text(
                    "The standard run is a planning estimate derived from the qualified dish pools. " +
                    "It is not a commitment.");

                col.Item().PaddingTop(3).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                    });

                    table.Cell().Padding(4).Text("Standard-run food cost / pax").SemiBold();
                    table.Cell().Padding(4).Text(Money(ruleset.StandardRunFoodCostPerPax));
                    table.Cell().Padding(4).Text("Standard-run food cost %").SemiBold();
                    table.Cell().Padding(4).Text(Percentage(ruleset.StandardRunFoodCostPercentage));

                    table.Cell().Padding(4).Text("Base gross profit %").SemiBold();
                    table.Cell().Padding(4).Text(Percentage(ruleset.BaseGrossProfitPercentage));
                    table.Cell().Padding(4).Text("Target margin").SemiBold();
                    table.Cell().Padding(4).Text($"{ruleset.TargetMarginPercentage:0.##}%");
                });

                // ── the course rules and their qualified pools ──
                col.Item().PaddingTop(12).PaddingBottom(5)
                    .Text("COURSE COVERAGE RULES").Bold().FontSize(10);

                if (ruleset.Rules.Count == 0)
                {
                    col.Item().Text("No course rule is configured for this tier.");
                }
                else
                {
                    foreach (var rule in ruleset.Rules)
                    {
                        col.Item().PaddingTop(4).Text($"{rule.CourseName} - required {rule.RequiredQuota}").Bold();
                        col.Item().Text($"Rule: {rule.RuleDescription}");
                        col.Item().Text(
                            $"Portion: {(rule.PortionGrams.HasValue ? $"{rule.PortionGrams.Value} g" : EmDash)}   " +
                            $"Inclusion: {rule.InclusionTag ?? EmDash}   " +
                            $"Upgrade: {rule.UpgradeAddonDescription ?? EmDash}");
                        col.Item().Text(
                            $"Qualified pool: {rule.PoolSize} dish(es), {rule.PoolCostedCount} costed   " +
                            $"Pool average food cost: {Money(rule.PoolAverageFoodCost)}");

                        if (rule.Pool.Count > 0)
                        {
                            col.Item().Text(string.Join(", ", rule.Pool.Select(dish =>
                                $"{dish.MenuName} ({Money(dish.FoodCost)})")));
                        }
                    }
                }

                // ── the covered municipalities ──
                col.Item().PaddingTop(12).PaddingBottom(5)
                    .Text("MUNICIPALITY COVERAGE").Bold().FontSize(10);

                if (coverage.Areas.Count == 0)
                {
                    col.Item().Text("No municipality is covered by this tier.");
                }
                else
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Municipality").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Rate / pax").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Distance").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Dispatch note").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Logistics").Bold();
                        });

                        foreach (var area in coverage.Areas)
                        {
                            table.Cell().Padding(4).Text(area.AreaName);
                            table.Cell().Padding(4).Text(Money(area.AdditionalRate));
                            table.Cell().Padding(4).Text(area.DistanceKm.HasValue ? $"{area.DistanceKm.Value} km" : EmDash);
                            table.Cell().Padding(4).Text(area.LogisticsNote ?? EmDash);
                            table.Cell().Padding(4).Text(Money(area.LogisticsCost));
                        }
                    });
                }
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Generated by Sal's CaterSuite | Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }

    /// <summary>The em dash a <c>null</c> figure is printed as — never a zero.</summary>
    private const string EmDash = "—";

    /// <summary>Prints a money figure, or the em dash when the server published it as unknown.</summary>
    private static string Money(decimal? amount) =>
        amount.HasValue ? $"PHP {amount.Value:N2}" : EmDash;

    /// <summary>Prints a percentage, or the em dash when the server published it as unknown.</summary>
    private static string Percentage(decimal? percentage) =>
        percentage.HasValue ? $"{percentage.Value:N2}%" : EmDash;
}
