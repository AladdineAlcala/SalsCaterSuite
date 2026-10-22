using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>one</b> rule that decides which payment rows are money the business holds (BR-27,
/// DEC-111).
/// <para>
/// A <b>soft-deleted</b> payment is excluded from every money surface and from the <b>list</b> of
/// ledger entries as well as from the sum: it is not money the business holds, and it is not a
/// receipt the customer may see. The exclusion is applied by the query that reads the payments
/// and by this helper — no global query filter is introduced (DEC-038, DEC-045).
/// </para>
/// <para>
/// The rule lives in one class so the ledger, the booking details, the customer surfaces, the
/// calendar feed and the register cannot drift apart, and so the register and the operations
/// dashboard reconcile by construction (AC-BKG-05, AC-BKG-47).
/// </para>
/// </summary>
public static class BookingPaymentPopulation
{
    /// <summary>The live (not soft-deleted) payment rows of a booking.</summary>
    public static IEnumerable<Payment> Live(Booking booking) =>
        (booking.Payments ?? Enumerable.Empty<Payment>()).Where(p => !p.IsDeleted);

    /// <summary>
    /// The authoritative collected amount of a booking: the sum of its live payment rows
    /// (BR-10, DEC-012). The denormalized <c>Booking.AmountPaid</c> cache is never used.
    /// </summary>
    public static decimal Collected(Booking booking) =>
        Live(booking).Sum(p => p.AmountPaid);
}
