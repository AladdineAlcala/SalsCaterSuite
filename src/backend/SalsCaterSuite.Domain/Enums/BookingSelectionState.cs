namespace SalsCaterSuite.Domain.Enums;

/// <summary>
/// A booking's menu-selection state (BR-28, M2). Exactly <b>one</b> of the three values is ever
/// published — it is never two flags.
/// </summary>
public enum BookingSelectionState
{
    /// <summary>The booking carries no live selection row and the window is open.</summary>
    None = 0,

    /// <summary>The booking carries at least one live selection row and the window is open.</summary>
    Selected = 1,

    /// <summary>The menu-change window has passed; the selection and the add-ons are frozen.</summary>
    Locked = 2
}
