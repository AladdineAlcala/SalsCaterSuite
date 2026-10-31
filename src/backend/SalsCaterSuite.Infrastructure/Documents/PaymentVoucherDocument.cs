using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Features.Payments.DTOs.Request;
using SalsCaterSuite.Application.Features.Payments.DTOs.Response;

namespace SalsCaterSuite.Infrastructure.Documents;

public class PaymentVoucherDocument : IDocument
{
    public PaymentEntryResponse Payment { get; }
    public string CustomerName { get; }

    public PaymentVoucherDocument(PaymentEntryResponse payment, string customerName)
    {
        Payment = payment;
        CustomerName = customerName;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("SAL'S CATERING SERVICES").Bold().FontSize(14).FontColor(Colors.Blue.Darken3);
                    c.Item().Text("OFFICIAL PAYMENT ACKNOWLEDGEMENT RECEIPT").Bold().FontSize(10);
                });
                row.ConstantItem(150).Column(c =>
                {
                    c.Item().Text($"Voucher No: {Payment.PaymentNo}").Bold().FontColor(Colors.Red.Darken2);
                    c.Item().Text($"Date: {Payment.DateOfPayment:yyyy-MM-dd HH:mm}");
                });
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                col.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().Text($"Received From: {CustomerName}").Bold();
                    r.RelativeItem().Text($"Booking Ref: TRN-{Payment.TransactionId:D5}");
                });
                col.Item().PaddingTop(5).Row(r =>
                {
                    r.RelativeItem().Text($"Payment Means: {Payment.PaymentMeans}");
                    r.RelativeItem().Text($"Check/Ref No: {Payment.CheckNo ?? "N/A"}");
                });
                col.Item().PaddingTop(5).Text($"Particulars: {Payment.Particular}");

                col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(10).Row(r =>
                {
                    r.RelativeItem().Text("AMOUNT RECEIVED:").Bold().FontSize(12);
                    r.ConstantItem(150).AlignRight().Text($"PHP {Payment.AmountPaid:N2}").Bold().FontSize(14).FontColor(Colors.Green.Darken3);
                });

                if (!string.IsNullOrWhiteSpace(Payment.Notes))
                {
                    col.Item().PaddingTop(5).Text($"Notes: {Payment.Notes}").Italic();
                }

                col.Item().PaddingTop(25).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Received by Cashier:");
                        c.Item().PaddingTop(20).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text(Payment.CreatedByUser ?? "Authorized Cashier");
                    });
                    r.ConstantItem(40);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Customer Conforme:");
                        c.Item().PaddingTop(20).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text(CustomerName);
                    });
                });
            });

            page.Footer().AlignCenter().Text("Thank you for choosing Sal's Catering Services!");
        });
    }
}

