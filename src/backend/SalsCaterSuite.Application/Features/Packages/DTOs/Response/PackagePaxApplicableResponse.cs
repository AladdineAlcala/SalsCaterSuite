namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>
/// One applicable-pax classification option (REQ-PKG-04, DEC-173).
/// <para>
/// The label is stored and published verbatim. It is <b>not</b> parsed as a numeric interval,
/// and option 5 is deliberately not coupled to <c>PackageType.Pm</c>.
/// </para>
/// </summary>
public class PackagePaxApplicableResponse
{
    /// <summary>The fixed identifier of the classification.</summary>
    public int NumPaxId { get; set; }

    /// <summary>The display label of the classification.</summary>
    public string PackageApplicablePax { get; set; } = string.Empty;
}
