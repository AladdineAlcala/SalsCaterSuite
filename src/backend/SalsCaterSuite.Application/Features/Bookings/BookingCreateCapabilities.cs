using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings;

/// <summary>
/// The collaborators the <b>extended</b> create path needs (REQ-BKG-05, DEC-114): the
/// municipality catalogue, the discount catalogue, the payment ledger, the receipt-number
/// generator, the deployment's booking policy and the pricing service.
/// <para>
/// They are gathered in one object so <see cref="Commands.CreateBookingCommand"/> keeps a
/// single, stable constructor: the five collaborators it has always had stay exactly where they
/// were, and the extended capabilities arrive as one additional argument.
/// </para>
/// </summary>
public sealed class BookingCreateCapabilities
{
    /// <summary>The municipality catalogue — the out-of-town surcharge's rate (BR-17).</summary>
    public IAreaRepository? Areas { get; init; }

    /// <summary>The discount catalogue — resolved by code (BR-02).</summary>
    public IDiscountRepository? Discounts { get; init; }

    /// <summary>The payment ledger — the initial payment's row.</summary>
    public IPaymentRepository? Payments { get; init; }

    /// <summary>
    /// The deployment's booking policy — the required deposit percentage and the capacity
    /// (DEC-101, DEC-102).
    /// </summary>
    public IBookingPolicyProvider Policy { get; init; } = DefaultBookingPolicyProvider.Instance;

    /// <summary>The one pricing computation (BR-25, BR-31).</summary>
    public BookingPricingService Pricing { get; init; } = new(DefaultBookingPolicyProvider.Instance);
}
