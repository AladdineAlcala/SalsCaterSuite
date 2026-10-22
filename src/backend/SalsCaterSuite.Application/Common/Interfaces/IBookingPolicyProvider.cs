namespace SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// The deployment-configured booking policy (DEC-101, DEC-102).
/// <para>
/// Two figures are <b>configuration</b>, not business rules: the required deposit percentage
/// and the commissary capacity. Both are <b>absent by default</b>, and an absent key means
/// <b>unknown</b>, never zero — the rail reads "not stated" and the client renders an em dash.
/// The reference mockups' <c>30</c> and <c>400</c> are never hardcoded.
/// </para>
/// <para>
/// The Application layer must not depend on <c>Microsoft.Extensions.Configuration</c>, so the
/// configuration binding lives in Infrastructure and is exposed through this neutral contract —
/// the same pattern the food-cost benchmark uses (DEC-040).
/// </para>
/// </summary>
public interface IBookingPolicyProvider
{
    /// <summary>
    /// The required deposit percentage (<c>Bookings:Deposit:RequiredPercentage</c>).
    /// <c>null</c> when the deployment has not stated one — which means <b>no</b> percentage is
    /// assumed or implied (DEC-043), not zero.
    /// </summary>
    decimal? RequiredDepositPercentage { get; }

    /// <summary>
    /// The commissary's maximum daily pax (<c>Bookings:Capacity:MaximumPax</c>).
    /// <c>null</c> when the deployment has not stated one.
    /// </summary>
    int? MaximumCapacityPax { get; }
}

/// <summary>
/// The default policy provider used when no configuration provider is supplied: both figures
/// are <b>not stated</b>.
/// </summary>
public sealed class DefaultBookingPolicyProvider : IBookingPolicyProvider
{
    /// <summary>A shared instance; the provider is stateless.</summary>
    public static DefaultBookingPolicyProvider Instance { get; } = new();

    public decimal? RequiredDepositPercentage => null;

    public int? MaximumCapacityPax => null;
}
