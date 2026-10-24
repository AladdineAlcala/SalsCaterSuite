using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

/// <summary>
/// <c>PUT /api/bookings/{id}/menus</c> — <b>replace</b> the booking's dish selection
/// (contract §15.2, DEC-134).
/// <para>
/// The write is a <b>full replace</b>, never a patch: the dishes no longer chosen are
/// soft-deleted, a re-chosen dish <b>resurrects</b> its row (the filtered unique index on
/// (<c>trn_Id</c>, <c>menuId</c>) is the database-level backstop), and an omitted
/// <c>prepInstruction</c> clears the per-event instruction (DEC-155).
/// </para>
/// <para>
/// The write is validated by the tier's <b>live</b> ruleset through the same resolver the quote
/// and the create use (BR-15, BR-35), refused after the menu-change window with a <c>400</c> that
/// names the lock date (BR-36), and it <b>never</b> moves the money (BR-37, DEC-129).
/// </para>
/// </summary>
public class ReplaceBookingMenuSelectionCommand
{
    private readonly IBookingRepository _bookings;
    private readonly BookingLineItemResolver _lineItems;
    private readonly BookingMenuSelectionPresenter _presenter;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public ReplaceBookingMenuSelectionCommand(
        IBookingRepository bookings,
        BookingLineItemResolver lineItems,
        BookingMenuSelectionPresenter presenter,
        IBookingPolicyProvider policy,
        IDateTime clock)
    {
        _bookings = bookings;
        _lineItems = lineItems;
        _presenter = presenter;
        _policy = policy;
        _clock = clock;
    }

    public async Task<Result<BookingMenuSelectionDto>> ExecuteAsync(
        int transactionId,
        BookingMenuSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        // DEC-085: an absent or null member is a 400 — "nothing was submitted" is never read as
        // "clear everything". An explicitly empty array is the client's clear.
        if (request.Selections is null)
        {
            return Result<BookingMenuSelectionDto>.Failure(
                "The 'selections' member is required. Send an empty array to clear the selection.");
        }

        var booking = await _bookings.GetWithLineItemsAsync(transactionId, cancellationToken);

        if (booking is null)
        {
            return new BookingNotFoundResult<BookingMenuSelectionDto>("Booking not found.");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return Result<BookingMenuSelectionDto>.Failure(
                "A cancelled booking's dish selection cannot be changed.");
        }

        var window = BookingMenuWindow.Resolve(
            booking.EventDate,
            _policy.MenuSelectionLockDaysBeforeEvent,
            DateOnly.FromDateTime(_clock.UtcNow));

        if (window.IsLocked)
        {
            return Result<BookingMenuSelectionDto>.Failure(
                $"The dish selection cannot be changed: the menu-change window closed on " +
                $"{window.LockDate:yyyy-MM-dd}.");
        }

        // The tier's LIVE ruleset validates the write (BR-15, BR-35).
        var resolved = await _lineItems.ResolveAsync(
            booking.PackageId,
            booking.NumberOfHeads,
            request.Selections,
            addons: null,
            cancellationToken);

        if (!resolved.Succeeded)
        {
            return Result<BookingMenuSelectionDto>.Failure(resolved.Errors);
        }

        Apply(booking, resolved.Data!.Selections);

        booking.UpdatedDate = DateTime.UtcNow;

        await _bookings.SaveChangesAsync(cancellationToken);

        return Result<BookingMenuSelectionDto>.Success(
            await _presenter.BuildAsync(booking, cancellationToken));
    }

    /// <summary>
    /// Applies the full replace on the aggregate's own collection: chosen dishes resurrect,
    /// omitted rows are soft-deleted (DEC-134).
    /// </summary>
    private static void Apply(Booking booking, IReadOnlyList<ResolvedSelectionRow> selection)
    {
        var requestedIds = selection
            .Select(row => row.MenuId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // One row per dish: the filtered unique index is the database backstop, so the live row
        // is the one that must be resurrected or updated.
        var existingByMenuId = booking.Menus
            .GroupBy(row => row.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(row => row.IsDeleted).First(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var row in selection)
        {
            if (existingByMenuId.TryGetValue(row.MenuId, out var existing))
            {
                existing.IsDeleted = false;
                existing.CourseId = row.CourseId;
                existing.PrepInstruction = row.PrepInstruction;
                continue;
            }

            booking.Menus.Add(new BookingMenu
            {
                CourseId = row.CourseId,
                MenuId = row.MenuId,
                PrepInstruction = row.PrepInstruction,
                Booking = booking
            });
        }

        foreach (var row in booking.Menus.Where(row => !row.IsDeleted))
        {
            if (!requestedIds.Contains(row.MenuId))
            {
                row.IsDeleted = true;
            }
        }
    }
}
