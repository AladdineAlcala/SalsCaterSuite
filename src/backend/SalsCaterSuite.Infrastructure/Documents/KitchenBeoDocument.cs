using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Infrastructure.Documents;

public class KitchenBeoDocument : IDocument
{
    public BookingDto Booking { get; }
    public List<MenuDto> MenuItems { get; }

    public KitchenBeoDocument(BookingDto booking, List<MenuDto> menuItems)
    {
        Booking = booking;
        MenuItems = menuItems;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

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
                        c.Item().Text($"Event Ref: TRN-{Booking.TransactionId:D5}").Bold();
                        c.Item().Text($"Occasion: {Booking.Occasion}");
                        c.Item().Text($"Client: {Booking.CustomerName}");
                    });
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Event Date: {Booking.EventDate:MMMM dd, yyyy}").Bold();
                        c.Item().Text($"Serving Time: {Booking.EventStartTime:hh:mm tt} - {Booking.EventEndTime:hh:mm tt}");
                        c.Item().Text($"Guaranteed Pax: {Booking.NumberOfHeads} Persons").Bold().FontColor(Colors.Blue.Darken2);
                    });
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Venue: {Booking.Venue}").Bold();
                        c.Item().Text($"Service Type: Buffet Service");
                    });
                });

                col.Item().PaddingTop(15).Text("MENU COURSE DISHES & KITCHEN PREP").Bold().FontSize(12);
                col.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(40);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(3);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Category").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Dish Name").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Special Notes").Bold();
                    });

                    int i = 1;
                    foreach (var item in MenuItems)
                    {
                        table.Cell().Padding(5).Text($"{i++}");
                        table.Cell().Padding(5).Text(item.CourseName ?? "General");
                        table.Cell().Padding(5).Text(item.MenuName).Bold();
                        table.Cell().Padding(5).Text(item.Note ?? "Standard Prep");
                    }
                });

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
}

