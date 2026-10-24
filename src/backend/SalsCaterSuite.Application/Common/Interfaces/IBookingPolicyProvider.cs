namespace SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// The deployment-configured booking policy (DEC-101, DEC-102, DEC-128, DEC-143).
/// <para>
/// Four figures are <b>configuration</b>, not business rules: the required deposit percentage,
/// the commissary capacity, the menu-change window's lead time and the VAT-inclusive rate.
/// Every one is <b>absent by default</b>, and an absent key means <b>unknown</b>, never zero —
/// the rail reads "not stated" and the client renders an em dash. The reference mockups'
/// <c>30</c>, <c>400</c>, <c>7</c> and <c>12</c> are never hardcoded.
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

    /// <summary>
    /// The number of days before the event after which the dish selection and the add-on lines
    /// are frozen (<c>Bookings:MenuSelection:LockDaysBeforeEvent</c>, DEC-128).
    /// <para>
    /// <c>null</c> when the deployment has not stated one. The <b>days</b> figure is the one that
    /// is "not stated"; the <b>lock date</b> is always published and is the event date in that
    /// case (BR-36, DEC-128).
    /// </para>
    /// </summary>
    int? MenuSelectionLockDaysBeforeEvent { get; }

    /// <summary>
    /// The VAT-inclusive rate (<c>Bookings:Tax:VatPercentage</c>, DEC-143).
    /// <para>
    /// <c>null</c> when the deployment has not stated one, and the disclosure is then
    /// <c>null</c>. The rate never changes the net amount due: it is a <b>memo</b> figure
    /// disclosed <b>within</b> it (BR-42).
    /// </para>
    /// </summary>
    decimal? VatPercentage { get; }
}

/// <summary>
/// The default policy provider used when no configuration provider is supplied: every figure
/// is <b>not stated</b>.
/// </summary>
public sealed class DefaultBookingPolicyProvider : IBookingPolicyProvider
{
    /// <summary>A shared instance; the provider is stateless.</summary>
    public static DefaultBookingPolicyProvider Instance { get; } = new();

    public decimal? RequiredDepositPercentage => null;

    public int? MaximumCapacityPax => null;

    public int? MenuSelectionLockDaysBeforeEvent => null;

    public decimal? VatPercentage => null;
}
