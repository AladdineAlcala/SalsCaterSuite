using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Infrastructure.Documents;

/// <summary>One chosen dish as the kitchen sheet prints it (contract §15.5).</summary>
/// <param name="CourseName">The course the dish was chosen for.</param>
/// <param name="DishName">The dish's catalogue name, or its key when the catalogue row is gone.</param>
/// <param name="PrepInstruction">
/// The <b>chef's per-event</b> instruction (DEC-146). Absent, the dish's catalogue note — the
/// ingredient note — is printed instead, and only then is "Standard prep" the fallback.
/// </param>
public sealed record BeoDishRow(string CourseName, string DishName, string PrepInstruction);

/// <summary>
/// One of the tier's course rules, printed when the booking carries <b>no</b> selection
/// (DEC-159): the sheet states the standard run and names the rules, never an invented dish
/// list.
/// </summary>
public sealed record BeoCourseRuleRow(
    string CourseName,
    int RequiredQuota,
    int? PortionGrams,
    decimal? BatchMassKg,
    string? RuleDescription);

/// <summary>One frozen add-on line the kitchen may have to staff for (contract §16.3).</summary>
public sealed record BeoAddonRow(
    string Description,
    string? Unit,
    AddonPricingBasis PricingBasis,
    decimal Amount);

/// <summary>
/// The Kitchen Banquet Event Order (BEO) / kitchen prep sheet (contract §15.5, DEC-148,
/// DEC-159).
/// <para>
/// It is rendered from the booking's <b>selection</b> — the "menu items" REQ-REP-02 has always
/// asked for — and it carries the kitchen's own words: the <b>per-event prep instruction</b>,
/// which lives on the selection row and never on the catalogue (DEC-146), and the booking's
/// <b>add-on lines</b>, which the kitchen may have to staff for.
/// </para>
/// <para>
/// A booking with <b>no</b> selection prints the tier's <b>course rules</b> and states plainly
/// that no dish selection has been recorded and that the event runs the tier's standard run. It
/// prints <b>no dish list</b>: BR-19 defines the standard run as a <b>cost figure</b>, the model
/// holds no canonical standard dish list, and printing the cheapest dish in each pool would be
/// an invented rule (DEC-159).
/// </para>
/// </summary>
public class KitchenBeoDocument : IDocument
{
    /// <summary>
    /// The sentence a selection-less sheet prints instead of a dish list (DEC-159). It is a
    /// constant so the document and its tests can never drift apart.
    /// </summary>
    public const string NoSelectionStatement =
        "No dish selection has been recorded for this event. The kitchen prepares the tier's standard run.";

    public BookingDto Booking { get; }

    /// <summary>The booking's stored selection and the tier's live ruleset (contract §15.1).</summary>
    public BookingMenuSelectionDto Selection { get; }

    /// <summary>The booking's frozen add-on lines (contract §16.3).</summary>
    public BookingAddonSetDto Addons { get; }

    public KitchenBeoDocument(
        BookingDto booking,
        BookingMenuSelectionDto selection,
        BookingAddonSetDto addons)
    {
        Booking = booking;
        Selection = selection;
        Addons = addons;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    /// <summary>Whether the booking carries a dish selection (contract §15.5).</summary>
    public bool HasSelection => Selection.Selections.Count > 0;

    /// <summary>
    /// The chosen dishes, in the order the selection endpoint publishes them. Empty when the
    /// booking carries no selection — the sheet then prints the course rules instead.
    /// </summary>
    public IReadOnlyList<BeoDishRow> BuildDishRows() =>
        Selection.Selections
            .Select(item => new BeoDishRow(
                CourseName: string.IsNullOrWhiteSpace(item.CourseName)
                    ? $"Course {item.CourseId}"
                    : item.CourseName!,
                DishName: string.IsNullOrWhiteSpace(item.MenuName) ? item.MenuId : item.MenuName!,
                // DEC-146: the chef's per-event word wins. The catalogue note is the fallback,
                // never the other way round.
                PrepInstruction: string.IsNullOrWhiteSpace(item.PrepInstruction)
                    ? string.IsNullOrWhiteSpace(item.Note) ? "Standard prep" : item.Note!
                    : item.PrepInstruction!))
            .ToList();

    /// <summary>
    /// The tier's course rules, printed <b>only</b> when the booking carries no selection
    /// (DEC-159). A tier with no ruleset yields an empty list.
    /// </summary>
    public IReadOnlyList<BeoCourseRuleRow> BuildStandardRunRules() =>
        HasSelection
            ? Array.Empty<BeoCourseRuleRow>()
            : Selection.Courses
                .Select(course => new BeoCourseRuleRow(
                    CourseName: string.IsNullOrWhiteSpace(course.CourseName)
                        ? $"Course {course.CourseId}"
                        : course.CourseName!,
                    RequiredQuota: course.RequiredQuota,
                    PortionGrams: course.PortionGrams,
                    BatchMassKg: course.BatchMassKg,
                    RuleDescription: course.RuleDescription))
                .ToList();

    /// <summary>The frozen add-on lines, in the order the add-on endpoint publishes them.</summary>
    public IReadOnlyList<BeoAddonRow> BuildAddonRows() =>
        Addons.Addons
            .Select(line => new BeoAddonRow(
                Description: line.Description,
                Unit: line.Unit,
                PricingBasis: line.PricingBasis,
                Amount: line.Amount))
            .ToList();

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Column(col =>
            {
                col.Item().Text("SAL'S CATERING SERVICES").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                col.Item().Text("BANQUET EVENT ORDER (BEO) / KITCHEN PREP SHEET").Bold().FontSize(12);
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Event Ref: {Booking.BookingNumber}").Bold();
                        c.Item().Text($"Occasion: {Booking.Occasion}");
                        c.Item().Text($"Client: {Booking.CustomerName}");
                    });
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Event Date: {Booking.EventDate:MMMM dd, yyyy}").Bold();
                        c.Item().Text($"Serving Time: {FormatTime(Booking.EventStartTime)} - {FormatTime(Booking.EventEndTime)}");
                        c.Item().Text($"Guaranteed Pax: {Booking.NumberOfHeads} Persons").Bold().FontColor(Colors.Blue.Darken2);
                    });
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Venue: {Booking.Venue}").Bold();
                        c.Item().Text($"Package: {Booking.PackageName ?? "Bespoke"}");
                        c.Item().Text($"Service Type: Buffet Service");
                    });
                });

                ComposeMenuSection(col);
                ComposeAddonSection(col);

                col.Item().PaddingTop(25).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Head Chef / Kitchen Supervisor:");
                        c.Item().PaddingTop(25).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text("Kitchen Production Lead");
                    });
                    r.ConstantItem(40);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Banquet Operations Captain:");
                        c.Item().PaddingTop(25).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text("Service Operations Lead");
                    });
                });
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("BEO Prep Sheet | Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
    }

    /// <summary>
    /// The dish list, or — when nothing is selected — the tier's rules and the plain statement
    /// that the event runs the standard run (DEC-159).
    /// </summary>
    private void ComposeMenuSection(ColumnDescriptor col)
    {
        col.Item().PaddingTop(15).Text("MENU COURSE DISHES & KITCHEN PREP").Bold().FontSize(12);

        if (!HasSelection)
        {
            col.Item().PaddingTop(5).Text(NoSelectionStatement).Italic().FontColor(Colors.Grey.Darken2);

            var rules = BuildStandardRunRules();

            if (rules.Count > 0)
            {
                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(55);
                        cols.ConstantColumn(65);
                        cols.ConstantColumn(65);
                        cols.RelativeColumn(4);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Course").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Quota").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Portion (g)").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Batch (kg)").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Rule").Bold();
                    });

                    foreach (var rule in rules)
                    {
                        table.Cell().Padding(5).Text(rule.CourseName);
                        table.Cell().Padding(5).Text($"{rule.RequiredQuota}");
                        table.Cell().Padding(5).Text(rule.PortionGrams.HasValue ? $"{rule.PortionGrams.Value}" : string.Empty);
                        table.Cell().Padding(5).Text(rule.BatchMassKg.HasValue ? $"{rule.BatchMassKg.Value:N2}" : string.Empty);
                        table.Cell().Padding(5).Text(rule.RuleDescription ?? string.Empty);
                    }
                });
            }

            return;
        }

        col.Item().PaddingTop(5).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(30);
                cols.RelativeColumn(2);
                cols.RelativeColumn(3);
                cols.RelativeColumn(4);
            });

            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Course").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Dish Name").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Kitchen Prep Instruction").Bold();
            });

            var index = 1;

            foreach (var row in BuildDishRows())
            {
                table.Cell().Padding(5).Text($"{index++}");
                table.Cell().Padding(5).Text(row.CourseName);
                table.Cell().Padding(5).Text(row.DishName).Bold();
                table.Cell().Padding(5).Text(row.PrepInstruction);
            }
        });
    }

    /// <summary>
    /// The booking's add-on lines — the kitchen may have to staff for them (contract §15.5).
    /// The section is omitted entirely when the booking carries none.
    /// </summary>
    private void ComposeAddonSection(ColumnDescriptor col)
    {
        var rows = BuildAddonRows();

        if (rows.Count == 0)
        {
            return;
        }

        col.Item().PaddingTop(15).Text("ADD-ON LINES").Bold().FontSize(12);

        col.Item().PaddingTop(5).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(30);
                cols.RelativeColumn(4);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
            });

            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Add-on").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Unit").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Basis").Bold();
                h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Line Total (PHP)").Bold();
            });

            var index = 1;

            foreach (var row in rows)
            {
                table.Cell().Padding(5).Text($"{index++}");
                table.Cell().Padding(5).Text(row.Description).Bold();
                table.Cell().Padding(5).Text(row.Unit ?? string.Empty);
                table.Cell().Padding(5).Text(BasisLabel(row.PricingBasis));
                table.Cell().Padding(5).Text($"PHP {row.Amount:N2}");
            }

            table.Cell().ColumnSpan(4).Padding(5).AlignRight().Text("Total add-ons").Bold();
            table.Cell().Padding(5).Text($"PHP {Addons.AddonsTotal:N2}").Bold();
        });
    }

    private static string BasisLabel(AddonPricingBasis basis) =>
        basis == AddonPricingBasis.PerGuest ? "Per guest" : "Flat";

    /// <summary>A time of day as <c>HH:mm</c> — the rendering the kitchen sheets have always used.</summary>
    private static string FormatTime(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}";
}
