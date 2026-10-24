using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// GET /api/bookings/summary — the register aggregate (contract §14.2, KPI-33 .. KPI-39).
/// <para>
/// The summary describes the <b>same filtered population</b> as the list, so with no filter
/// applied it reconciles with the operations dashboard's KPI-12 and KPI-13 to the cent
/// (AC-BKG-05). Every figure is server-computed (DEC-002).
/// </para>
/// </summary>
public class GetBookingRegisterSummaryQuery
{
    /// <summary>
    /// The near-term commissary window (KPI-38): the next 14 days, today included.
    /// </summary>
    public const int CapacityWindowDays = BookingCapacityWindow.Days;

    private readonly BookingRegisterPopulationService _population;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;
    private readonly IValidator<BookingRegisterFilterRequest> _validator;

    public GetBookingRegisterSummaryQuery(
        BookingRegisterPopulationService population,
        IBookingPolicyProvider policy,
        IDateTime clock,
        IValidator<BookingRegisterFilterRequest> validator)
    {
        _population = population;
        _policy = policy;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<BookingRegisterSummaryDto>> ExecuteAsync(
        BookingRegisterFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BookingRegisterSummaryDto>.Failure(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var filter = BookingRegisterFilter.From(request);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        var rows = await _population.LoadAsync(filter, today, cancellationToken);
        var filtered = BookingRegisterProjection.ApplyFilters(rows, filter);

        var dto = new BookingRegisterSummaryDto
        {
            Pipeline = BuildPipeline(filtered),
            LeadingTier = await BuildLeadingTierAsync(filtered, filter, today, cancellationToken),
            ContractValue = BuildContractValue(filtered),
            Headcount = BuildHeadcount(filtered, today),
            PaymentStates = BuildPaymentStates(filtered),
            Capacity = BuildCapacity(filtered, today),
            Receivables = BuildReceivables(filtered),
            MenuWindows = BuildMenuWindows(filtered),
            BandDiscount = BuildBandDiscount(filtered),
            VatPercentage = _policy.VatPercentage,
            GeneratedAt = _clock.UtcNow
        };

        return Result<BookingRegisterSummaryDto>.Success(dto);
    }

    /// <summary>The pipeline count and its status split (KPI-33).</summary>
    private static BookingRegisterPipelineDto BuildPipeline(List<BookingRegisterRow> filtered)
    {
        var confirmed = filtered.Count(row =>
            row.Booking.Status is BookingStatus.Confirmed or BookingStatus.DepositPaid or BookingStatus.FullyPaid);
        var tentative = filtered.Count(row => row.Booking.Status == BookingStatus.Pending);
        var completed = filtered.Count(row => row.Booking.Status == BookingStatus.Completed);

        return new BookingRegisterPipelineDto
        {
            BookingCount = filtered.Count,
            ConfirmedCount = confirmed,
            TentativeCount = tentative,
            CompletedCount = completed,
            OtherCount = filtered.Count - confirmed - tentative - completed,
            CommittedPax = filtered.Sum(row => row.Booking.NumberOfHeads)
        };
    }

    /// <summary>
    /// The leading tier — the tier with the highest net contracted value — its share of the
    /// population's net contracted value, and the change against the immediately preceding
    /// equal-length window (KPI-34, DEC-109).
    /// <para>
    /// A booking with no tier is excluded from the leading tier (BR-27).
    /// </para>
    /// </summary>
    private async Task<BookingRegisterLeadingTierDto?> BuildLeadingTierAsync(
        List<BookingRegisterRow> filtered,
        BookingRegisterFilter filter,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var totalNetContractedValue = filtered.Sum(row => row.NetAmountDue);

        var leading = filtered
            .Where(row => row.Booking.PackageId.HasValue)
            .GroupBy(row => row.Booking.PackageId!.Value)
            .Select(group => new
            {
                PackageId = group.Key,
                NetContractedValue = group.Sum(row => row.NetAmountDue),
                PackageType = group.Select(row => row.Booking.Package?.PackageType)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                PackageName = group.Select(row => row.Booking.PackageName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            })
            .OrderByDescending(group => group.NetContractedValue)
            .ThenBy(group => group.PackageId)
            .FirstOrDefault();

        if (leading is null)
        {
            return null;
        }

        var previousNet = await PreviousNetContractedValueAsync(
            leading.PackageId, filter, filtered, today, cancellationToken);

        return new BookingRegisterLeadingTierDto
        {
            PackageType = leading.PackageType,
            PackageName = leading.PackageName,
            NetContractedValue = leading.NetContractedValue,
            SharePercentage = totalNetContractedValue > 0m
                ? BookingAmountDueCalculator.Round2(leading.NetContractedValue / totalNetContractedValue * 100m)
                : null,
            PreviousNetContractedValue = previousNet,
            ChangePercentage = previousNet.HasValue && previousNet.Value != 0m
                ? BookingAmountDueCalculator.Round2(
                    (leading.NetContractedValue - previousNet.Value) / previousNet.Value * 100m)
                : null
        };
    }

    /// <summary>
    /// The leading tier's net contracted value in the immediately preceding equal-length window.
    /// <para>
    /// With an active date filter the window is the filter's own length, immediately before it.
    /// Without one it is the equal-length window that ends the day before the population's
    /// earliest event date (KPI-34's date basis). <c>null</c> when the window holds no booking
    /// of the tier — unknown, never zero (DEC-109).
    /// </para>
    /// </summary>
    private async Task<decimal?> PreviousNetContractedValueAsync(
        int packageId,
        BookingRegisterFilter filter,
        List<BookingRegisterRow> filtered,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var window = ResolvePrecedingWindow(filter, filtered);

        if (window is null)
        {
            return null;
        }

        var previousRows = await _population.LoadWindowAsync(
            filter.Search, window.Value.From, window.Value.To, today, cancellationToken);

        var previousNet = previousRows
            .Where(row => row.Booking.PackageId == packageId)
            .Sum(row => row.NetAmountDue);

        return previousRows.Any(row => row.Booking.PackageId == packageId) ? previousNet : null;
    }

    /// <summary>
    /// The preceding window. With an inclusive date filter it is the filter's own length,
    /// immediately before it; without one it is the equal-length window that ends the day before
    /// the population's earliest event date.
    /// </summary>
    private static (DateOnly From, DateOnly To)? ResolvePrecedingWindow(
        BookingRegisterFilter filter,
        List<BookingRegisterRow> filtered)
    {
        if (filter.FromDate.HasValue && filter.ToDate.HasValue)
        {
            var windowLength = filter.ToDate.Value.DayNumber - filter.FromDate.Value.DayNumber + 1;
            return (filter.FromDate.Value.AddDays(-windowLength), filter.FromDate.Value.AddDays(-1));
        }

        if (filtered.Count == 0)
        {
            return null;
        }

        var earliest = filtered.Min(row => row.Booking.EventDate);
        var latest = filtered.Max(row => row.Booking.EventDate);
        var spanLength = latest.DayNumber - earliest.DayNumber + 1;

        return (earliest.AddDays(-spanLength), earliest.AddDays(-1));
    }

    /// <summary>The contract value and its split (KPI-35).</summary>
    private static BookingRegisterContractValueDto BuildContractValue(List<BookingRegisterRow> filtered)
    {
        var net = filtered.Sum(row => row.NetAmountDue);
        var collected = filtered.Sum(row => row.Collected);
        var outstanding = filtered.Sum(row => row.Outstanding);

        return new BookingRegisterContractValueDto
        {
            NetContractedValue = net,
            CollectedAmount = collected,
            OutstandingAmount = outstanding,
            CollectionRatioPercentage = net > 0m
                ? BookingAmountDueCalculator.Round2(collected / net * 100m)
                : null
        };
    }

    /// <summary>The committed headcount and the next event (KPI-36).</summary>
    private static BookingRegisterHeadcountDto BuildHeadcount(List<BookingRegisterRow> filtered, DateOnly today)
    {
        var totalPax = filtered.Sum(row => row.Booking.NumberOfHeads);

        var next = filtered
            .Where(row => row.Booking.EventDate >= today)
            .OrderBy(row => row.Booking.EventDate)
            .ThenBy(row => row.Booking.TransactionId)
            .FirstOrDefault();

        return new BookingRegisterHeadcountDto
        {
            TotalPax = totalPax,
            AveragePaxPerBooking = filtered.Count > 0
                ? BookingAmountDueCalculator.Round2(totalPax / (decimal)filtered.Count)
                : null,
            NextEventDate = next?.Booking.EventDate,
            NextEventBookingNumber = next?.Booking.BookingNumber
        };
    }

    /// <summary>The payment-state distribution (KPI-37).</summary>
    private static List<BookingRegisterPaymentStateDto> BuildPaymentStates(List<BookingRegisterRow> filtered) =>
        BookingPaymentState.All
            .Select(state => new BookingRegisterPaymentStateDto
            {
                Key = state,
                Count = filtered.Count(row => row.PaymentState == state),
                Amount = filtered.Where(row => row.PaymentState == state).Sum(row => row.Outstanding)
            })
            .ToList();

    /// <summary>
    /// The near-term commissary load against the configured capacity (KPI-38, DEC-102). The
    /// capacity is <c>null</c> when the key is absent, and the utilisation is then
    /// <c>null</c> as well — unknown, never zero and never 100 %.
    /// </summary>
    private BookingRegisterCapacityDto BuildCapacity(List<BookingRegisterRow> filtered, DateOnly today)
    {
        var windowEnd = today.AddDays(CapacityWindowDays);

        var committedPax = filtered
            .Where(row => row.Booking.EventDate >= today && row.Booking.EventDate < windowEnd)
            .Sum(row => row.Booking.NumberOfHeads);

        var capacity = _policy.MaximumCapacityPax;

        return new BookingRegisterCapacityDto
        {
            CommittedPax = committedPax,
            CapacityPax = capacity,
            UtilisationPercentage = capacity.HasValue && capacity.Value > 0
                ? BookingAmountDueCalculator.Round2(committedPax / (decimal)capacity.Value * 100m)
                : null,
            IsConfigured = capacity.HasValue
        };
    }

    /// <summary>The register's own accounts receivable (KPI-39, DEC-041).</summary>
    private static BookingRegisterReceivablesDto BuildReceivables(List<BookingRegisterRow> filtered)
    {
        var withBalance = filtered.Where(row => row.Outstanding > 0m).ToList();
        var overdue = withBalance.Where(row => row.IsOverdue).ToList();

        return new BookingRegisterReceivablesDto
        {
            OutstandingAmount = filtered.Sum(row => row.Outstanding),
            BookingCount = withBalance.Count,
            OverdueCount = overdue.Count,
            OverdueAmount = overdue.Sum(row => row.Outstanding)
        };
    }

    /// <summary>
    /// The menu-change window's open and closed counts (KPI-41, DEC-128). Every booking of the
    /// population has a concrete lock date, so the two counts always add up to the population.
    /// </summary>
    private static BookingRegisterMenuWindowsDto BuildMenuWindows(List<BookingRegisterRow> filtered) => new()
    {
        OpenSelectionCount = filtered.Count(row => !row.IsMenuSelectionLocked),
        LockedSelectionCount = filtered.Count(row => row.IsMenuSelectionLocked)
    };

    /// <summary>
    /// The automatic headcount band's take (KPI-42, DEC-154): the bookings the <b>derived</b>
    /// source says the band priced, and their net amount due. The count is never <c>null</c>.
    /// </summary>
    private static BookingRegisterBandDiscountDto BuildBandDiscount(List<BookingRegisterRow> filtered)
    {
        var byBand = filtered
            .Where(row => DiscountSourceResolver.Resolve(
                row.Booking.DiscountCode,
                row.Booking.NumberOfHeads,
                row.Booking.DiscountAmount) == DiscountSource.HeadcountBand)
            .ToList();

        return new BookingRegisterBandDiscountDto
        {
            DiscountedBookingCount = byBand.Count,
            DiscountedNetAmountDue = byBand.Sum(row => row.NetAmountDue)
        };
    }
}
