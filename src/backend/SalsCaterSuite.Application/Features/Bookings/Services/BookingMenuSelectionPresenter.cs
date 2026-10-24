using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>one</b> projection of a booking's dish selection onto <see cref="BookingMenuSelectionDto"/>
/// (contract §15.1).
/// <para>
/// The read endpoint and the two writes' responses both build their payload here, so the saved
/// selection is described exactly as the selection that was read before the save — never a
/// second, hand-assembled shape (DEC-134, BR-31).
/// </para>
/// </summary>
public class BookingMenuSelectionPresenter
{
    private readonly BookingMenuCourseSetBuilder _courseSets;
    private readonly BookingTierEstimateService _tierEstimates;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public BookingMenuSelectionPresenter(
        BookingMenuCourseSetBuilder courseSets,
        BookingTierEstimateService tierEstimates,
        BookingAmountDueCalculator calculator,
        IBookingPolicyProvider policy,
        IDateTime clock)
    {
        _courseSets = courseSets;
        _tierEstimates = tierEstimates;
        _calculator = calculator;
        _policy = policy;
        _clock = clock;
    }

    /// <summary>
    /// Describes the booking's <b>live</b> selection, the tier's live ruleset and the
    /// menu-change window.
    /// </summary>
    public async Task<BookingMenuSelectionDto> BuildAsync(
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        var courseSet = await _courseSets.BuildAsync(
            booking.PackageId, booking.NumberOfHeads, cancellationToken);

        var window = BookingMenuWindow.Resolve(
            booking.EventDate,
            _policy.MenuSelectionLockDaysBeforeEvent,
            DateOnly.FromDateTime(_clock.UtcNow));

        var liveSelection = booking.Menus
            .Where(row => !row.IsDeleted)
            .OrderBy(row => row.CourseId)
            .ThenBy(row => row.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = liveSelection
            .Select(row => new ResolvedSelectionRow(row.CourseId, row.MenuId, row.PrepInstruction))
            .ToList();

        var dto = new BookingMenuSelectionDto
        {
            TransactionId = booking.TransactionId,
            BookingNumber = booking.BookingNumber,
            EventDate = booking.EventDate,
            PackageId = booking.PackageId,
            PackageName = booking.PackageName ?? booking.Package?.Description,
            MenuSelectionLockDate = window.LockDate,
            MenuSelectionLockDays = window.LockDays,
            IsLocked = window.IsLocked,
            SelectionState = DiscountSourceResolver.ResolveSelectionState(
                window.IsLocked, liveSelection.Count > 0),
            AddonCount = booking.AddOns.Count(line => !line.IsDeleted),
            AddonsTotal = booking.AddonsTotal
        };

        BookingSelectionProjector.ApplyTo(dto, courseSet, rows, booking.PricePerHead);

        // The tier's standard run — the planning estimate the selection overrides (BR-19).
        if (booking.PackageId.HasValue)
        {
            var estimates = await _tierEstimates.GetAsync(
                new[] { booking.PackageId.Value }, cancellationToken);

            if (estimates.TryGetValue(booking.PackageId.Value, out var estimate))
            {
                dto.StandardRunFoodCostPerPax = estimate.StandardRunFoodCostPerPax;
                dto.StandardRunGrossProfitPercentage = estimate.BaseGrossProfitPercentage;
            }
        }

        // BR-42, DEC-143: the VAT is a memo disclosure WITHIN the net amount due.
        var collected = BookingPaymentPopulation.Collected(booking);
        var breakdown = _calculator.Calculate(booking, collected);

        dto.VatPercentage = _policy.VatPercentage;
        dto.IncludedVatAmount = IncludedVat.Calculate(
            breakdown.NetAmountDue, _policy.VatPercentage);

        // DEC-154, DEC-160: the discount's source and its band are published, never inferred.
        var band = HeadcountBandDiscount.Resolve(booking.NumberOfHeads);
        dto.DiscountSource = DiscountSourceResolver.Resolve(
            booking.DiscountCode, booking.NumberOfHeads, booking.DiscountAmount);
        dto.DiscountBandLabel =
            dto.DiscountSource == DiscountSource.HeadcountBand ? band?.Label : null;
        dto.DiscountBandRatePerHead =
            dto.DiscountSource == DiscountSource.HeadcountBand ? band?.RatePerHead : null;

        return dto;
    }
}
