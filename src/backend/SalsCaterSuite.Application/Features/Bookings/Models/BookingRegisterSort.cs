namespace SalsCaterSuite.Application.Features.Bookings.Models;

/// <summary>
/// The sort vocabulary of the Bookings &amp; Events Register (contract §14.1).
/// <para>
/// The values are the published names. An unrecognised value is rejected with <c>400</c> —
/// the vocabulary is never silently widened (BR-32).
/// </para>
/// </summary>
public static class BookingRegisterSort
{
    /// <summary>Event date, earliest first — the default.</summary>
    public const string EventDateAsc = "EventDateAsc";

    /// <summary>Event date, latest first.</summary>
    public const string EventDateDesc = "EventDateDesc";

    /// <summary>Net contracted value, highest first.</summary>
    public const string ContractValueDesc = "ContractValueDesc";

    /// <summary>Net contracted value, lowest first.</summary>
    public const string ContractValueAsc = "ContractValueAsc";

    /// <summary>Booking number, A to Z.</summary>
    public const string BookingNumberAsc = "BookingNumberAsc";

    /// <summary>The published sort values.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        EventDateAsc, EventDateDesc, ContractValueDesc, ContractValueAsc, BookingNumberAsc
    };

    /// <summary>The default sort.</summary>
    public const string Default = EventDateAsc;
}
