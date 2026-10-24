using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The inputs of the one booking-pricing computation (BR-25, BR-26, BR-02, BR-30).
/// </summary>
public sealed record BookingPricingInput
{
    /// <summary>The tier's frozen rate per guest.</summary>
    public decimal PricePerHead { get; init; }

    /// <summary>The headcount the booking is priced for.</summary>
    public int NumberOfHeads { get; init; }

    /// <summary>
    /// The municipality's per-pax out-of-town rate (BR-17). <c>null</c> when the municipality
    /// has stated none — an absent rate contributes <b>zero</b> to the money chain while the
    /// published rate stays <c>null</c> (DEC-086).
    /// </summary>
    public decimal? ExtendedAreaRate { get; init; }

    /// <summary>
    /// The tier's own per-guest extended-location amount (the owner's 2026-10-24 request). It is
    /// added to the municipality's rate; <c>null</c> contributes zero. Only a Regular/Premier
    /// tier may carry one.
    /// </summary>
    public decimal? PackageExtendedLocationRate { get; init; }

    /// <summary>
    /// The operator's creation-time switch. When it is off the surcharge is zero, whatever the
    /// municipality's rate is (BR-26).
    /// </summary>
    public bool ApplyExtendedAmount { get; init; }

    /// <summary>
    /// The resolved discount, or <c>null</c> when no discount applies. The resolution itself is
    /// the caller's business — an unknown or an ambiguous code is a <c>400</c> (BR-02).
    /// </summary>
    public Discount? Discount { get; init; }

    /// <summary>The code that resolved the discount; stored frozen on the booking (DEC-106).</summary>
    public string? DiscountCode { get; init; }

    /// <summary>
    /// The frozen add-on line total — BR-01's third term, which joins the gross (DEC-135,
    /// DEC-151). Zero when the booking carries no add-ons.
    /// </summary>
    public decimal AddonsTotal { get; init; }

    /// <summary>The deposit the operator stated. Advisory — never a second payment (BR-30).</summary>
    public decimal? StatedDepositAmount { get; init; }
}

/// <summary>
/// The result of the one booking-pricing computation. Every figure is already rounded to two
/// decimals, away from the midpoint (DEC-115).
/// </summary>
public sealed record BookingPricingResult
{
    /// <summary>The base amount — the rate per guest times the headcount.</summary>
    public decimal BaseAmount { get; init; }

    /// <summary>The municipality's rate as published; <c>null</c> when it was not stated.</summary>
    public decimal? ExtendedAreaRate { get; init; }

    /// <summary>
    /// The tier's own per-guest extended-location amount as published; <c>null</c> when it is not
    /// stated (the owner's 2026-10-24 request).
    /// </summary>
    public decimal? PackageExtendedLocationRate { get; init; }

    /// <summary>The frozen out-of-town surcharge in pesos.</summary>
    public decimal ExtendedAreaAmount { get; init; }

    /// <summary>The gross amount — the base plus the surcharge plus the add-ons.</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>The frozen add-on line total that joined the gross (DEC-135, DEC-151).</summary>
    public decimal AddonsTotal { get; init; }

    /// <summary>The frozen discount code.</summary>
    public string? DiscountCode { get; init; }

    /// <summary>The applied discount type.</summary>
    public DiscountType DiscountType { get; init; }

    /// <summary>The applied discount percentage.</summary>
    public decimal DiscountPercentage { get; init; }

    /// <summary>The applied discount amount, already rounded.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>The <b>derived</b> source of the discount (BR-44, DEC-154).</summary>
    public DiscountSource DiscountSource { get; init; }

    /// <summary>The applied headcount band's label, or <c>null</c> when no band applied.</summary>
    public string? DiscountBandLabel { get; init; }

    /// <summary>The applied headcount band's rate per head, or <c>null</c>.</summary>
    public decimal? DiscountBandRatePerHead { get; init; }

    /// <summary>The net amount due — the gross less the discount.</summary>
    public decimal NetAmountDue { get; init; }

    /// <summary>The configured VAT-inclusive rate, or <c>null</c> when none is stated.</summary>
    public decimal? VatPercentage { get; init; }

    /// <summary>
    /// The VAT-inclusive disclosure — a memo figure <b>within</b> the net amount due (BR-42,
    /// DEC-143). <c>null</c> when the rate is absent or the net is zero.
    /// </summary>
    public decimal? IncludedVatAmount { get; init; }

    /// <summary>The configured required deposit percentage, or <c>null</c> when none is stated.</summary>
    public decimal? RequiredDepositPercentage { get; init; }

    /// <summary>The required deposit amount, or <c>null</c> when no percentage is configured.</summary>
    public decimal? RequiredDepositAmount { get; init; }

    /// <summary>The deposit the operator stated.</summary>
    public decimal DepositAmount { get; init; }

    /// <summary>
    /// The deposit that actually applies: the required amount when one is configured, otherwise
    /// the stated amount, otherwise zero (BR-30).
    /// </summary>
    public decimal EffectiveDeposit { get; init; }
}

/// <summary>
/// The <b>single</b> pricing computation of a booking (BR-25, BR-26, BR-02, BR-30, DEC-115).
/// <para>
/// The quote (<c>POST /api/bookings/quote</c>) and the create path (<c>POST /api/bookings</c>)
/// both call it, so the figure a workbench quotes and the figure a confirmed booking freezes
/// are produced by the same code — BR-31's "the quote's figures are computed by the same code
/// path the create path uses" is therefore structural, not aspirational.
/// </para>
/// <para>
/// <b>One rounding.</b> The discount and the net are each rounded <b>once</b>, two decimals,
/// away from the midpoint (DEC-115), and the create path stores exactly those rounded values —
/// so <c>Booking.AmountDue</c> equals the calculator's <c>NetAmountDue</c> to the cent.
/// </para>
/// </summary>
public class BookingPricingService
{
    private readonly IBookingPolicyProvider _policy;

    public BookingPricingService(IBookingPolicyProvider policy)
    {
        _policy = policy;
    }

    /// <summary>
    /// Prices a proposed or created booking. The tax <b>charge</b> is zero — the tax policy is
    /// UNRESOLVED (DEC-008) — while the configured VAT rate is disclosed <b>within</b> the net
    /// amount due (BR-42, DEC-143). The stored <c>Booking.AmountDue</c> and the shared
    /// calculator's <c>NetAmountDue</c> therefore agree by construction (DEC-099).
    /// </summary>
    public BookingPricingResult Price(BookingPricingInput input)
    {
        var baseAmount = BookingAmountDueCalculator.Round2(input.PricePerHead * input.NumberOfHeads);

        // An unstated out-of-town rate contributes nothing to the money chain, while the
        // published rate stays null — the measurement is unknown, the addition is nothing
        // (DEC-086). The tier's own per-guest amount joins the municipality's rate (the owner's
        // 2026-10-24 request).
        var effectiveRate = (input.ExtendedAreaRate ?? 0m)
            + (input.PackageExtendedLocationRate ?? 0m);
        var extendedAreaAmount = input.ApplyExtendedAmount
            ? BookingAmountDueCalculator.Round2(effectiveRate * input.NumberOfHeads)
            : 0m;

        var gross = BookingAmountDueCalculator.Round2(
            baseAmount + extendedAreaAmount + input.AddonsTotal);

        // BR-02, BR-38, DEC-137: an explicit code REPLACES the automatic band — it never stacks.
        // When no code resolves, the automatic headcount-band rule is the fallback and is the
        // only source of a fixed discount that carries no code.
        var (discountType, discountPercentage, fixedAmount) = ResolveDiscount(input.Discount);

        var band = (HeadcountBand?)null;

        if (discountType == DiscountType.None)
        {
            band = HeadcountBandDiscount.Resolve(input.NumberOfHeads);

            if (band is not null)
            {
                discountType = DiscountType.FixedAmount;
                fixedAmount = band.RatePerHead * input.NumberOfHeads;
            }
        }

        var rawDiscount = 0m;
        if (fixedAmount.HasValue)
        {
            rawDiscount = fixedAmount.Value;
        }
        else if (discountPercentage > 0m)
        {
            rawDiscount = (gross * discountPercentage) / 100m;
        }

        var discountAmount = BookingAmountDueCalculator.Round2(Math.Min(rawDiscount, gross));
        var netAmountDue = BookingAmountDueCalculator.Round2(Math.Max(0m, gross - discountAmount));

        var discountSource = DiscountSourceResolver.Resolve(
            input.DiscountCode, input.NumberOfHeads, discountAmount);

        // BR-30: the required deposit is configuration and absent by default. When it is absent
        // no percentage is assumed or implied (DEC-043) and the rail reads "not stated".
        var requiredPercentage = _policy.RequiredDepositPercentage;
        decimal? requiredAmount = null;
        if (requiredPercentage.HasValue)
        {
            requiredAmount = BookingAmountDueCalculator.Round2(netAmountDue * requiredPercentage.Value / 100m);
        }

        var statedDeposit = input.StatedDepositAmount ?? 0m;

        // BR-42, DEC-143: the VAT is a memo figure disclosed WITHIN the net amount due and
        // never added to it, so it cannot move the amount due.
        var vatPercentage = _policy.VatPercentage;

        return new BookingPricingResult
        {
            BaseAmount = baseAmount,
            ExtendedAreaRate = input.ExtendedAreaRate,
            PackageExtendedLocationRate = input.PackageExtendedLocationRate,
            ExtendedAreaAmount = extendedAreaAmount,
            GrossAmount = gross,
            AddonsTotal = BookingAmountDueCalculator.Round2(input.AddonsTotal),
            DiscountCode = input.DiscountCode,
            DiscountType = discountType,
            DiscountPercentage = discountPercentage,
            DiscountAmount = discountAmount,
            DiscountSource = discountSource,
            DiscountBandLabel = band?.Label,
            DiscountBandRatePerHead = band?.RatePerHead,
            NetAmountDue = netAmountDue,
            VatPercentage = vatPercentage,
            IncludedVatAmount = IncludedVat.Calculate(netAmountDue, vatPercentage),
            RequiredDepositPercentage = requiredPercentage,
            RequiredDepositAmount = requiredAmount,
            DepositAmount = statedDeposit,
            EffectiveDeposit = requiredAmount ?? statedDeposit
        };
    }

    /// <summary>
    /// The discount's own resolution: a fixed amount takes precedence over a percentage
    /// (BR-02). The returned percentage is the catalogue's own, so the frozen booking still
    /// records which rule the catalogue described — the calculator gives the fixed amount
    /// precedence anyway (BR-25).
    /// </summary>
    private static (DiscountType Type, decimal Percentage, decimal? FixedAmount) ResolveDiscount(Discount? discount)
    {
        if (discount is null)
        {
            return (DiscountType.None, 0m, null);
        }

        if (discount.FixedAmount.HasValue && discount.FixedAmount.Value > 0m)
        {
            return (DiscountType.FixedAmount, discount.Percentage, discount.FixedAmount.Value);
        }

        if (discount.Percentage > 0m)
        {
            return (DiscountType.Percentage, discount.Percentage, null);
        }

        return (DiscountType.None, 0m, null);
    }
}
