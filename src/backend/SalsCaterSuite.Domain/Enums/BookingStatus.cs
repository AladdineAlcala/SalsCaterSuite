namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// Lifecycle status only (DEC-177). Payment progress is <see cref="PaymentState"/>.
/// Ordinals are the post-migration values. Legacy ordinals are remapped by the migration:
/// 0→0, 1/2/3→1, 4→2, 5→3, 6→4, 7→5, 8→6, 9→7.
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5,
    Rescheduled = 6,
    Expired = 7
}
