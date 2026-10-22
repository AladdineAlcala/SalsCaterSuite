namespace SalsCaterSuite.Application.Features.Payments.Services;

using System.Globalization;
using SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// The <b>one</b> receipt-number generator of the product (BR-05, DEC-112).
/// <para>
/// The payment ledger and the booking create path both issue a receipt, and the number has one
/// format — <c>OR-YYYYMMDD-XXXX</c> — so the create path <b>inherits</b> the ledger's existing
/// behaviour instead of creating a second convention.
/// </para>
/// <para>
/// The <i>race</i> the sequence can still lose (two concurrent receipts on the same day) is
/// <b>not</b> fixed by this cycle — RSK-013 stays open. Extracting the generator keeps the
/// blast radius from growing; it does not close the risk.
/// </para>
/// </summary>
public static class ReceiptNumberGenerator
{
    /// <summary>
    /// Issues the next receipt number of the day: it counts the receipts already created and
    /// formats the next number in the sequence. The count and the format live in one place so
    /// the ledger and the create path cannot diverge.
    /// </summary>
    public static async Task<string> GenerateAsync(
        IPaymentRepository payments,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var countToday = await payments.CountCreatedOnDateAsync(now, cancellationToken);
        return Generate(now, countToday);
    }

    /// <summary>
    /// The one format of a receipt number: <c>OR-{yyyyMMdd}-{count + 1:D4}</c>.
    /// </summary>
    public static string Generate(DateTime now, int countCreatedToday) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"OR-{now:yyyyMMdd}-{countCreatedToday + 1:D4}");
}
