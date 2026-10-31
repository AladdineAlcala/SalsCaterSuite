namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>The menu-change window's open and closed counts (KPI-41, DEC-128).</summary>
public class BookingRegisterMenuWindowsResponse
{
    /// <summary>The bookings whose window is still open.</summary>
    public int OpenSelectionCount { get; set; }

    /// <summary>The bookings whose window has closed.</summary>
    public int LockedSelectionCount { get; set; }
}
