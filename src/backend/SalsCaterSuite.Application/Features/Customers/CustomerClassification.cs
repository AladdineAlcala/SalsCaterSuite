namespace SalsCaterSuite.Application.Features.Customers;

/// <summary>
/// The derived account classification (spec §4.4, DEC-051).
/// <para>
/// <c>Corporate</c> when the company name is not blank, <c>Private</c> otherwise. The
/// classification is a <b>presentation</b> of the existing <c>Customer.Company</c> attribute:
/// it is never persisted and it never changes a record.
/// </para>
/// </summary>
public static class CustomerClassification
{
    /// <summary>The customer has a non-blank company name.</summary>
    public const string Corporate = "Corporate";

    /// <summary>The customer has no company name.</summary>
    public const string Private = "Private";

    /// <summary>The derived classification of a company name.</summary>
    public static string Derive(string? company) =>
        string.IsNullOrWhiteSpace(company) ? Private : Corporate;
}
