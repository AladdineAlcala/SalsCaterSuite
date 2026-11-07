using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

/// <summary>
/// <c>PUT /api/bookings/{id}/addons</c> — <b>replace</b> the booking's add-on lines and
/// <b>re-freeze</b> the money (contract §16.4, DEC-134, DEC-152).
/// <para>
/// The lines are replaced, never patched: the lines no longer carried are soft-deleted and a
/// re-added add-on resurrects its row (the filtered unique index on (<c>trn_Id</c>,
/// <c>addonId</c>) is the database-level backstop).
/// </para>
/// <para>
/// The write <b>re-freezes</b> the money: the add-on total is recomputed, <c>Subtotal</c>,
/// <c>DiscountAmount</c> and <c>AmountDue</c> are re-frozen, the frozen discount is
/// <b>re-capped</b> at the new gross but <b>not</b> recomputed — the band depends on the
/// headcount, which did not change — the <b>deposit is not touched</b>, and a write that would
/// leave the collected amount above the new net amount due is refused with nothing written
/// (BR-05).
/// </para>
/// </summary>
public class ReplaceBookingAddonsCommand
{
    private readonly IBookingRepository _bookings;
    private readonly BookingLineItemResolver _lineItems;
    private readonly BookingAddonSetPresenter _presenter;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public ReplaceBookingAddonsCommand(
        IBookingRepository bookings,
        BookingLineItemResolver lineItems,
        BookingAddonSetPresenter presenter,
        IBookingPolicyProvider policy,
        IDateTime clock)
    {
        _bookings = bookings;
        _lineItems = lineItems;
        _presenter = presenter;
        _policy = policy;
        _clock = clock;
    }

    public async Task<Result<BookingAddonSetResponse>> ExecuteAsync(
        int transactionId,
        BookingAddonsRequest request,
        CancellationToken cancellationToken = default)
    {
        // DEC-085: an absent or null member is a 400 — "nothing was submitted" is never read as
        // "clear every line". An explicitly empty array is the client's clear.
        if (request.Addons is null)
        {
            return Result<BookingAddonSetResponse>.Failure(
                "The 'addons' member is required. Send an empty array to clear the lines.");
        }

        var booking = await _bookings.GetWithLineItemsAsync(transactionId, cancellationToken);

        if (booking is null)
        {
            return new BookingNotFoundResult<BookingAddonSetResponse>("Booking not found.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result<BookingAddonSetResponse>.Failure(
                "A cancelled booking's add-on lines cannot be changed.");
        }

        if (booking.Status == BookingStatus.Completed)
        {
            return Result<BookingAddonSetResponse>.Failure(
                "A completed booking's add-on lines cannot be changed.");
        }

        var window = BookingMenuWindow.Resolve(
            booking.EventDate,
            _policy.MenuSelectionLockDaysBeforeEvent,
            DateOnly.FromDateTime(_clock.UtcNow));

        if (window.IsLocked)
        {
            return Result<BookingAddonSetResponse>.Failure(
                $"The add-on lines cannot be changed: the menu-change window closed on " +
                $"{window.LockDate:yyyy-MM-dd}.");
        }

        var resolved = await _lineItems.ResolveAsync(
            booking.PackageId,
            booking.NumberOfHeads,
            selections: null,
            request.Addons,
            cancellationToken);

        if (!resolved.Succeeded)
        {
            return Result<BookingAddonSetResponse>.Failure(resolved.Errors);
        }

        var addonsTotal = resolved.Data!.AddonsTotal;

        // DEC-152: the discount is RE-CAPPED at the new gross, not recomputed — the band depends
        // on the headcount, which this write does not touch.
        var newSubtotal = BookingAmountDueCalculator.Round2(
            booking.PackageAmount + booking.ExtendedAreaAmount + addonsTotal);
        var newDiscount = BookingAmountDueCalculator.Round2(
            Math.Min(booking.DiscountAmount, newSubtotal));
        var newAmountDue = BookingAmountDueCalculator.Round2(
            Math.Max(0m, newSubtotal - newDiscount + booking.TaxAmount));

        // BR-05: a payment may never exceed the net amount due. The operator settles the credit
        // first; nothing is written until then.
        var collected = BookingPaymentPopulation.Collected(booking);

        if (collected > newAmountDue)
        {
            return Result<BookingAddonSetResponse>.Failure(
                $"The add-on lines would leave the net amount due at {newAmountDue:0.00}, below the " +
                $"{collected:0.00} already collected. Settle the credit before reducing the lines.");
        }

        Apply(booking, resolved.Data!.Addons);

        booking.RecalculateFinancials(
            extendedAreaAmount: booking.ExtendedAreaAmount,
            addonsAmount: addonsTotal,
            discountAmount: newDiscount,
            taxAmount: booking.TaxAmount);

        // The live payments were loaded with the line items. Overwrite the cache-based
        // refresh so a headcount-independent due change cannot leave payment state stale.
        booking.RefreshPaymentStatus();

        booking.UpdatedDate = DateTime.UtcNow;

        await _bookings.SaveChangesAsync(cancellationToken);

        // The catalogue navigation (and its category) is only hydrated by the repository, so the
        // saved lines are described from a fresh read rather than from the in-memory aggregate,
        // which would publish a null category for a line added by this write (contract §16.3).
        var saved = await _bookings.GetWithLineItemsAsync(booking.TransactionId, cancellationToken);

        return Result<BookingAddonSetResponse>.Success(
            _presenter.Build(saved ?? booking));
    }

    /// <summary>
    /// Applies the full replace on the aggregate's own collection: added add-ons resurrect,
    /// omitted rows are soft-deleted (DEC-134).
    /// </summary>
    private static void Apply(Booking booking, IReadOnlyList<ResolvedAddonLine> lines)
    {
        var requestedIds = lines.Select(line => line.AddonId).ToHashSet();

        var existingById = booking.AddOns
            .GroupBy(line => line.AddonId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(line => line.IsDeleted).First());

        foreach (var line in lines)
        {
            if (existingById.TryGetValue(line.AddonId, out var existing))
            {
                // The line's frozen values are re-frozen from the request (DEC-139, DEC-158).
                existing.IsDeleted = false;
                existing.Description = line.Description;
                existing.Amount = line.Amount;
                existing.Unit = line.Unit;
                existing.PricingBasis = line.PricingBasis;
                continue;
            }

            booking.AddOns.Add(new BookingAddOn
            {
                AddonId = line.AddonId,
                Description = line.Description,
                Amount = line.Amount,
                Unit = line.Unit,
                PricingBasis = line.PricingBasis,
                Booking = booking
            });
        }

        foreach (var line in booking.AddOns.Where(line => !line.IsDeleted))
        {
            if (!requestedIds.Contains(line.AddonId))
            {
                line.IsDeleted = true;
            }
        }
    }
}
