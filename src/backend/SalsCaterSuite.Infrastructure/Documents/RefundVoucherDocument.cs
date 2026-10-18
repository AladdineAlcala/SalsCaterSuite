using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SalsCaterSuite.Infrastructure.Documents;

public class RefundVoucherDocument : IDocument
{
    public long RefundId { get; }
    public int TransactionId { get; }
    public string CustomerName { get; }
    public decimal RefundAmount { get; }
    public decimal Deduction { get; }
    public decimal NetRefund { get; }
    public string Reason { get; }
    public DateTime Date { get; }

    public RefundVoucherDocument(
        long refundId,
        int transactionId,
        string customerName,
        decimal refundAmount,
        decimal deduction,
        decimal netRefund,
        string reason,
        DateTime date)
    {
        RefundId = refundId;
        TransactionId = transactionId;
        CustomerName = customerName;
        RefundAmount = refundAmount;
        Deduction = deduction;
        NetRefund = netRefund;
        Reason = reason;
        Date = date;
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

            page.Header().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("SAL'S CATERING SERVICES").Bold().FontSize(14).FontColor(Colors.Red.Darken3);
                    c.Item().Text("CANCELLATION & REFUND VOUCHER").Bold().FontSize(10);
                });
                r.ConstantItem(150).Column(c =>
                {
                    c.Item().Text($"Voucher No: RF-{RefundId:D5}").Bold();
                    c.Item().Text($"Date: {Date:yyyy-MM-dd}");
                });
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                col.Item().PaddingTop(8).Text($"Payee / Client: {CustomerName}").Bold();
                col.Item().Text($"Booking Reference: TRN-{TransactionId:D5}");
                col.Item().Text($"Cancellation Reason: {Reason}");

                col.Item().PaddingTop(12).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                    });

                    t.Cell().Padding(4).Text("Total Deposits Eligible for Refund:");
                    t.Cell().Padding(4).AlignRight().Text($"PHP {RefundAmount:N2}");

                    t.Cell().Padding(4).Text("Less: Cancellation Charges / Deductions:");
                    t.Cell().Padding(4).AlignRight().Text($"- PHP {Deduction:N2}");

                    t.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("NET REFUND PAYABLE:").Bold();
                    t.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"PHP {NetRefund:N2}").Bold().FontColor(Colors.Green.Darken3);
                });

                col.Item().PaddingTop(25).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Prepared By / Disbursing Officer:");
                        c.Item().PaddingTop(20).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text("Finance Accounting Officer");
                    });
                    r.ConstantItem(40);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Received By Client:");
                        c.Item().PaddingTop(20).LineHorizontal(1);
                        c.Item().PaddingTop(2).Text(CustomerName);
                    });
                });
            });

            page.Footer().AlignCenter().Text("Refund Voucher | Sal's CaterSuite");
        });
    }
}

