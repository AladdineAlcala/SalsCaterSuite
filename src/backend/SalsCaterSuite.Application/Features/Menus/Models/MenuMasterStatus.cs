namespace SalsCaterSuite.Application.Features.Menus.Models;

/// <summary>
/// The status filter of the Menu &amp; Food Costing Master (spec §4.4, §6.1).
/// <para>
/// An unrecognised value is <b>ignored</b>, never rejected: the query normalises the raw query
/// string to this enumeration and falls back to <see cref="All"/>.
/// </para>
/// </summary>
public enum MenuMasterStatus
{
    /// <summary>No status filter — every dish of the filtered set.</summary>
    All = 0,

    /// <summary>The dishes whose food cost is known and above zero.</summary>
    Costed = 1,

    /// <summary>The dishes whose food cost is missing or zero.</summary>
    Uncosted = 2,

    /// <summary>The dishes whose food cost percentage exceeds the configured target.</summary>
    MarginAlert = 3
}
