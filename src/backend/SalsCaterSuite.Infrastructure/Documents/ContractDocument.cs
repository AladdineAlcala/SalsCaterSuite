using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;

namespace SalsCaterSuite.Infrastructure.Documents;

/// <summary>
/// One row of the contract's service-details table.
/// <para>
/// The table carries the <b>additive</b> line items only — the package and, when the booking
/// carries them, the out-of-town surcharge and the frozen add-on total. A zero charge is
/// <b>not</b> a line item: an absent line is not a "0.00" row (BR-20's null/zero discipline).
/// </para>
/// </summary>
/// <param name="Description">What the row is.</param>
/// <param name="Pax">The headcount the row is priced for, when it has one.</param>
/// <param name="RatePerPax">The per-guest rate, when the row has one.</param>
/// <param name="Total">The row's amount in pesos.</param>
public sealed record ContractServiceLine(string Description, int? Pax, decimal? RatePerPax, decimal Total);

/// <summary>
/// One line of the contract's totals block.
/// </summary>
/// <param name="Label">What the line is.</param>
/// <param name="Amount">The amount in pesos.</param>
/// <param name="IsEmphasis">Whether the line is emphasised (the contract amount, the balance).</param>
public sealed record ContractTotalLine(string Label, decimal Amount, bool IsEmphasis = false);

/// <summary>
/// One line of the contract's event-particulars block.
/// </summary>
/// <param name="Text">The rendered line.</param>
/// <param name="IsBold">Whether the line is emphasised.</param>
public sealed record ContractParticularsLine(string Text, bool IsBold = false);

public class ContractDocument : IDocument
{
    /// <summary>
    /// The repeating page header — the <b>document's identity</b> (the company, the document
    /// type). It never carries a booking's particulars: BR-34's dispatch schedule belongs in the
    /// information block and is printed <b>once</b>, next to the programme times it is ordered
    /// against.
    /// </summary>
    public static readonly IReadOnlyList<string> HeaderLines = new[]
    {
        "SAL'S CATERING SERVICES & EVENT SUITE",
        "CATERING CONTRACT & SERVICE AGREEMENT"
    };

    public BookingResponse Booking { get; }

    public ContractDocument(BookingResponse booking)
    {
        Booking = booking;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    /// <summary>
    /// The event's particulars — the block that carries the Event Date, the Occasion, the Venue,
    /// the programme's serving time, the guaranteed pax and (when both times are stated) the
    /// dispatch schedule.
    /// <para>
    /// The wording of a line that already exists on the documents the business signs is
    /// <b>preserved exactly</b> — in particular <c>Guaranteed Pax: {N} Persons</c>, the wording
    /// the kitchen's event order has always printed. This cycle <b>adds</b> lines to the contract
    /// (the serving time and the dispatch schedule); it never renames one. The service-details
    /// table's <b>Pax</b> column is the itemisation of the headcount, so the particulars block
    /// carries it once, as the summary.
    /// </para>
    /// <para>
    /// The dispatch schedule is <b>printed once, here</b>, next to the programme times it is
    /// ordered against (BR-34). It is omitted entirely when either time is unstated — an
    /// unstated time is never printed as a zero (BR-20).
    /// </para>
    /// </summary>
    public IReadOnlyList<ContractParticularsLine> BuildParticularsLines()
    {
        var lines = new List<ContractParticularsLine>
        {
            // The pre-cycle emphasis on the Event Date is preserved, and so is its rendering:
            // the same format string and the same ambient culture the documents have always
            // used — this cycle adds lines, it does not rewrite them.
            new($"Event Date: {Booking.EventDate:MMMM dd, yyyy}", IsBold: true),
            new($"Occasion: {Booking.Occasion}"),
            new($"Venue: {Booking.Venue}"),
            new($"Serving Time: {FormatTime(Booking.EventStartTime)} - {FormatTime(Booking.EventEndTime)}"),
            new($"Guaranteed Pax: {Booking.NumberOfHeads} Persons", IsBold: true)
        };

        var dispatchSchedule = BuildDispatchScheduleText();

        if (!string.IsNullOrWhiteSpace(dispatchSchedule))
        {
            lines.Add(new ContractParticularsLine(dispatchSchedule, IsBold: true));
        }

        return lines;
    }

    /// <summary>
    /// The service-details rows. The package is always there; the out-of-town surcharge is
    /// there <b>only</b> when the booking carries one (BR-26, DEC-116, BR-20).
    /// </summary>
    public IReadOnlyList<ContractServiceLine> BuildServiceLines()
    {
        var lines = new List<ContractServiceLine>
        {
            new(
                Description: Booking.PackageName ?? "Catering Buffet Package",
                Pax: Booking.NumberOfHeads,
                RatePerPax: Booking.NumberOfHeads > 0
                    ? Booking.PackageAmount / Booking.NumberOfHeads
                    : null,
                Total: Booking.PackageAmount)
        };

        // The out-of-town surcharge: its per-guest rate and the headcount, so the reader can
        // see how the amount was derived (the municipality's rate × pax, BR-17, BR-26).
        if (Booking.ExtendedAreaAmount > 0m)
        {
            lines.Add(new ContractServiceLine(
                Description: "Out-of-town surcharge",
                Pax: Booking.NumberOfHeads,
                RatePerPax: Booking.ExtendedAreaRatePerGuest,
                Total: Booking.ExtendedAreaAmount));
        }

        // The frozen add-on total (BR-44, DEC-135, DEC-151). It is an ADDITIONAL row and only
        // exists when the booking carries one — a zero total is not a "0.00" row (BR-20).
        if (Booking.AddonsTotal > 0m)
        {
            lines.Add(new ContractServiceLine(
                Description: "Add-ons",
                Pax: null,
                RatePerPax: null,
                Total: Booking.AddonsTotal));
        }

        return lines;
    }

    /// <summary>
    /// The totals block. Every figure is the <b>shared calculator's</b> (DEC-113) — the
    /// document never derives a second money path. The discount line is there <b>only</b> when a
    /// discount was applied, and it carries the frozen code so the client can see what was taken
    /// off (DEC-106, BR-02, BR-20).
    /// </summary>
    public IReadOnlyList<ContractTotalLine> BuildTotalLines()
    {
        var lines = new List<ContractTotalLine>
        {
            new("Total Charges", Booking.Subtotal)
        };

        if (Booking.DiscountAmount > 0m)
        {
            var label = string.IsNullOrWhiteSpace(Booking.DiscountCode)
                ? "Discount"
                : $"Discount ({Booking.DiscountCode})";

            lines.Add(new ContractTotalLine(label, Booking.DiscountAmount));
        }

        lines.Add(new ContractTotalLine("Contract Amount", Booking.AmountDue, IsEmphasis: true));
        lines.Add(new ContractTotalLine("Total Payments Received", Booking.AmountPaid));
        lines.Add(new ContractTotalLine("Remaining Balance", Booking.Balance, IsEmphasis: true));

        return lines;
    }

    /// <summary>
    /// The dispatch schedule (BR-34, DEC-106) — printed <b>once</b>, in the event's particulars
    /// block, next to the programme times it is ordered against. It is published <b>only</b> when
    /// both times are stated; an unstated time omits the line entirely rather than printing a
    /// zero.
    /// </summary>
    public string? BuildDispatchScheduleText() =>
        Booking.CallTime.HasValue && Booking.PackoutTime.HasValue
            ? $"Dispatch Schedule: Call {FormatTime(Booking.CallTime.Value)}  ·  Pack-out {FormatTime(Booking.PackoutTime.Value)}"
            : null;

    /// <summary>A time of day as <c>HH:mm</c>.</summary>
    private static string FormatTime(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}";

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Column(col =>
            {
                col.Item().Text(HeaderLines[0])
                    .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                col.Item().Text(HeaderLines[1])
                    .Bold().FontSize(12).FontColor(Colors.Grey.Darken2);
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Contract Ref: {ResolveContractReference()}").Bold();
                        c.Item().Text($"Client Name: {Booking.CustomerName}");
                        c.Item().Text($"Contact No: {Booking.CustomerContact ?? "N/A"}");
                    });

                    // The event's particulars. The dispatch schedule is part of them and is
                    // printed ONCE, here, next to the programme times it is ordered against
                    // (BR-34) — the repeating page header carries the document's identity,
                    // not one booking's schedule.
                    row.RelativeItem().Column(c =>
                    {
                        foreach (var particular in BuildParticularsLines())
                        {
                            var text = c.Item().Text(particular.Text);

                            if (particular.IsBold)
                            {
                                text.Bold();
                            }
                        }
                    });
                });

                col.Item().PaddingTop(15).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Service Details").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Pax").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Rate/Pax").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Total (PHP)").Bold();
                    });

                    foreach (var line in BuildServiceLines())
                    {
                        table.Cell().Padding(5).Text(line.Description);
                        table.Cell().Padding(5).Text(line.Pax.HasValue ? $"{line.Pax}" : string.Empty);
                        table.Cell().Padding(5).Text(line.RatePerPax.HasValue ? $"PHP {line.RatePerPax.Value:N2}" : string.Empty);
                        table.Cell().Padding(5).Text($"PHP {line.Total:N2}");
                    }
                });

                col.Item().PaddingTop(20).AlignRight().Column(c =>
                {
                    // DEC-113: the money block is the SHARED calculator's output. The DTO this
                    // document receives is produced by the one booking-details path, so
                    // AmountDue is the calculator's NetAmountDue, AmountPaid is the
                    // authoritative payment sum and Balance is the calculator's
                    // RemainingBalance. The denormalized Booking.AmountPaid cache and the
                    // domain's Booking.Balance are never used.
                    foreach (var line in BuildTotalLines())
                    {
                        var text = c.Item().Text($"{line.Label}: PHP {line.Amount:N2}");

                        if (line.IsEmphasis)
                        {
                            text.Bold();
                        }
                    }
                });

                col.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Client Signature over Printed Name:");
                        c.Item().PaddingTop(35).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text(Booking.CustomerName ?? "Client").SemiBold();
                    });
                    row.ConstantItem(40);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Sal's Catering Authorized Representative:");
                        c.Item().PaddingTop(35).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text("Authorized Caterer Representative").SemiBold();
                    });
                });
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

    /// <summary>
    /// Reference printed on the agreement. The human-readable <see cref="BookingResponse.BookingNumber"/>
    /// is authoritative (DEC-016 / DEC-023); the free-text reference and finally the legacy
    /// <c>TRN-{id}</c> form are used only when the booking number is blank.
    /// </summary>
    private string ResolveContractReference()
    {
        if (!string.IsNullOrWhiteSpace(Booking.BookingNumber))
        {
            return Booking.BookingNumber;
        }

        return string.IsNullOrWhiteSpace(Booking.Reference)
            ? $"TRN-{Booking.TransactionId:D5}"
            : Booking.Reference;
    }
}
