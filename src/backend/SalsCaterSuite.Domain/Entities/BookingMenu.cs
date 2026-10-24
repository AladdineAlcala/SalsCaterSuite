using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// One dish chosen for <b>one booking</b> (REQ-BKG-06, DEC-127, BR-35).
/// <para>
/// The row is a <b>catalogue reference</b>: it stores no dish name, no price and no food cost,
/// because the catalogue owns those. The <see cref="CourseId"/> is <b>frozen</b> at selection
/// time, so the selection stays self-describing if the catalogue later re-assigns a dish
/// (RSK-063). The <see cref="PrepInstruction"/> is the chef's <b>per-event</b> instruction for
/// this dish and lives on the selection row, never on the catalogue (DEC-146).
/// </para>
/// <para>
/// The selection carries <b>no money</b> and never moves a booking's totals (BR-37, DEC-129).
/// </para>
/// </summary>
public class BookingMenu : BaseEntity
{
    public int BookingMenuId { get; set; }

    /// <summary>The booking the selection belongs to (<c>trn_Id</c>).</summary>
    public int TransactionId { get; set; }

    public Booking? Booking { get; set; }

    /// <summary>The chosen dish's catalogue key (the catalogue's key is a string).</summary>
    public string MenuId { get; set; } = string.Empty;

    public Menu? Menu { get; set; }

    /// <summary>The course the dish was chosen for — frozen at selection time (RSK-063).</summary>
    public int CourseId { get; set; }

    public CourseCategory? CourseCategory { get; set; }

    /// <summary>
    /// The chef's per-event prep instruction for this dish (DEC-146).
    /// <c>null</c> = not stated.
    /// </summary>
    public string? PrepInstruction { get; set; }
}
