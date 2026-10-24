namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The two tier types the out-of-town surcharge is limited to (BR-40, DEC-141, DEC-156).
/// <para>
/// The test is <b>pinned</b>: the tier's <c>PackageType</c> is <b>trimmed</b> and compared
/// <b>case-insensitively</b> for <b>equality</b> with <c>Regular</c> or <c>Premier</c>
/// <b>verbatim</b>. A type such as "Premier Buffet" is <b>not</b> a surcharge tier. The
/// comparison is on the <b>type column only</b>, never on the description: the development
/// catalogue's own tier is typed <c>Regular</c> while its description reads
/// "2026 Premier Package B", so a description match would give the opposite answer.
/// </para>
/// </summary>
public static class PackageSurchargeEligibility
{
    /// <summary>The eligible type names, in the order the 400 names them.</summary>
    public static readonly IReadOnlyList<string> EligibleTypes = new[] { "Regular", "Premier" };

    /// <summary>Whether the tier's stored type is one of the two eligible types.</summary>
    public static bool IsEligible(string? packageType)
    {
        if (string.IsNullOrWhiteSpace(packageType))
        {
            return false;
        }

        var trimmed = packageType.Trim();

        return string.Equals(trimmed, "Regular", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "Premier", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The <c>400</c> message for an ineligible surcharge, naming both eligible types so the
    /// operator is not left guessing (BR-40).
    /// </summary>
    public static string RejectionMessage(string? packageType) =>
        $"The out-of-town surcharge applies only to the Regular and Premier tier types; " +
        $"the selected tier is typed '{packageType ?? "not stated"}'.";
}
