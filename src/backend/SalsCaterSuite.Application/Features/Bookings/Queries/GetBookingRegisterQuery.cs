using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Queries;

/// <summary>
/// GET /api/bookings — the Bookings &amp; Events Register (contract §14.1, REQ-BKG-05).
/// <para>
/// The register is a <b>server-side</b> faceted list. The repository loads the <b>base
/// population</b> — the bookings matching the search and the date range — the Application layer
/// computes each row's net amount due, collected amount, payment state and overdue flag, and
/// only then are the facet filters, the totals, the facets, the sort and the page applied
/// (spec §3.3).
/// </para>
/// <para>
/// The totals strip and the facet counts describe the <b>whole filtered population</b>, never
/// the page (BR-32). The cost is therefore proportional to the filtered population and not to
/// the page size (RSK-056) — a deliberate trade, because the totals must describe the whole
/// population anyway.
/// </para>
/// </summary>
public class GetBookingRegisterQuery
{
    private readonly BookingPlanningEstimateService _estimates;
    private readonly BookingRegisterPopulationService _population;
    private readonly IDateTime _clock;
    private readonly IValidator<BookingRegisterFilterRequest> _validator;

    public GetBookingRegisterQuery(
        BookingPlanningEstimateService estimates,
        BookingRegisterPopulationService population,
        IDateTime clock,
        IValidator<BookingRegisterFilterRequest> validator)
    {
        _estimates = estimates;
        _population = population;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<BookingRegisterPageDto>> ExecuteAsync(
        BookingRegisterFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BookingRegisterPageDto>.Failure(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var filter = BookingRegisterFilter.From(request);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        var rows = await _population.LoadAsync(filter, today, cancellationToken);
        var filtered = Sort(BookingRegisterProjection.ApplyFilters(rows, filter), filter.Sort);

        var page = filtered
            .Skip(Offset(filter.Page, filter.PageSize))
            .Take(filter.PageSize)
            .ToList();

        var estimates = await _estimates.GetBaseGrossProfitPercentagesAsync(
            page.Select(row => row.Booking).ToList(),
            cancellationToken);

        var dto = new BookingRegisterPageDto
        {
            Items = page.Select(row => Map(row, estimates)).ToList(),
            TotalCount = filtered.Count,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalPages = (int)Math.Ceiling(filtered.Count / (double)filter.PageSize),
            Summary = BuildTotals(filtered),
            QuickViewCounts = BookingRegisterProjection.BuildFacets(rows, filter),
            GeneratedAt = _clock.UtcNow
        };

        return Result<BookingRegisterPageDto>.Success(dto);
    }

    /// <summary>
    /// The <b>whole</b> filtered population with its totals — the XLSX export's source. It is
    /// never paged (contract §14.6, spec §4.6).
    /// </summary>
    public async Task<Result<BookingRegisterExportData>> ExecuteUnpagedAsync(
        BookingRegisterFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BookingRegisterExportData>.Failure(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var filter = BookingRegisterFilter.From(request);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        var rows = await _population.LoadAsync(filter, today, cancellationToken);
        var filtered = Sort(BookingRegisterProjection.ApplyFilters(rows, filter), filter.Sort);

        // The export does not publish the per-booking planning estimate, so it is not computed
        // for it (the dictionary is empty; the field stays null in the sheet, RSK-054).
        var estimates = new Dictionary<int, decimal?>();

        return Result<BookingRegisterExportData>.Success(new BookingRegisterExportData
        {
            Items = filtered.Select(row => Map(row, estimates)).ToList(),
            Totals = BuildTotals(filtered)
        });
    }

    /// <summary>The totals strip — over the whole filtered population (BR-32).</summary>
    private static BookingRegisterTotalsDto BuildTotals(List<BookingRegisterRow> filtered) => new()
    {
        BookingCount = filtered.Count,
        NetContractedValue = filtered.Sum(row => row.NetAmountDue),
        CollectedAmount = filtered.Sum(row => row.Collected),
        OutstandingAmount = filtered.Sum(row => row.Outstanding),
        TotalPax = filtered.Sum(row => row.Booking.NumberOfHeads)
    };

    /// <summary>Maps one row onto the dossier card.</summary>
    private static BookingRegisterItemDto Map(
        BookingRegisterRow row,
        IReadOnlyDictionary<int, decimal?> estimates)
    {
        var booking = row.Booking;

        var baseGrossProfitPercentage = estimates.TryGetValue(
            booking.TransactionId, out var estimate)
                ? estimate
                : null;

        var liveSelection = booking.Menus.Any(menu => !menu.IsDeleted);
        var liveAddons = booking.AddOns.Where(line => !line.IsDeleted).ToList();

        return new BookingRegisterItemDto
        {
            TransactionId = booking.TransactionId,
            BookingNumber = booking.BookingNumber,
            EventDate = booking.EventDate,
            EventStartTime = booking.EventStartTime,
            EventEndTime = booking.EventEndTime,
            CallTime = booking.CallTime,
            PackoutTime = booking.PackoutTime,

            Status = booking.Status,
            PaymentState = row.PaymentState,
            IsOverdue = row.IsOverdue,
            DaysUntilEvent = row.DaysUntilEvent,

            CustomerId = booking.CustomerId,
            CustomerName = booking.Customer?.FullName ?? string.Empty,
            CustomerCompany = booking.Customer?.Company,
            CustomerContact = booking.Customer?.Contact1 ?? booking.Customer?.Contact2,
            Occasion = booking.Occasion,
            Venue = booking.Venue,

            PackageId = booking.PackageId,
            PackageName = booking.PackageName,
            PackageType = booking.Package?.PackageType,
            NumberOfHeads = booking.NumberOfHeads,
            PricePerHead = booking.PricePerHead,
            PackageAmount = booking.PackageAmount,

            NetAmountDue = row.NetAmountDue,
            AmountPaid = row.Collected,
            OutstandingBalance = row.Outstanding,

            ExtendedAreaId = booking.ExtendedAreaId,
            ExtendedAreaName = booking.ExtendedArea?.AreaDetails,
            ExtendedAreaAmount = booking.ExtendedAreaAmount,
            ApplyExtendedAmount = booking.ApplyExtendedAmount,
            DepositAmount = booking.DepositAmount,
            DiscountCode = booking.DiscountCode,
            DiscountAmount = booking.DiscountAmount,

            BaseGrossProfitPercentage = baseGrossProfitPercentage,

            DiscountSource = DiscountSourceResolver.Resolve(
                booking.DiscountCode, booking.NumberOfHeads, booking.DiscountAmount),
            SelectionState = DiscountSourceResolver.ResolveSelectionState(
                row.IsMenuSelectionLocked, liveSelection),
            AddonCount = liveAddons.Count,
            AddonsTotal = booking.AddonsTotal
        };
    }

    /// <summary>
    /// The page offset, computed in 64 bits and clamped, so an absurd page number can never
    /// overflow into a negative skip.
    /// </summary>
    private static int Offset(int page, int pageSize)
    {
        var offset = (long)(page - 1) * pageSize;
        return (int)Math.Min(Math.Max(offset, 0), int.MaxValue);
    }

    /// <summary>
    /// The server-side ordering (contract §14.1). Every sort carries a stable tiebreak on the
    /// transaction id, so the order never depends on the storage order.
    /// </summary>
    private static List<BookingRegisterRow> Sort(
        List<BookingRegisterRow> rows,
        string sort) => sort switch
        {
            BookingRegisterSort.EventDateDesc => rows
                .OrderByDescending(row => row.Booking.EventDate)
                .ThenBy(row => row.Booking.TransactionId)
                .ToList(),

            BookingRegisterSort.ContractValueDesc => rows
                .OrderByDescending(row => row.NetAmountDue)
                .ThenBy(row => row.Booking.EventDate)
                .ThenBy(row => row.Booking.TransactionId)
                .ToList(),

            BookingRegisterSort.ContractValueAsc => rows
                .OrderBy(row => row.NetAmountDue)
                .ThenBy(row => row.Booking.EventDate)
                .ThenBy(row => row.Booking.TransactionId)
                .ToList(),

            BookingRegisterSort.BookingNumberAsc => rows
                .OrderBy(row => row.Booking.BookingNumber, StringComparer.Ordinal)
                .ThenBy(row => row.Booking.TransactionId)
                .ToList(),

            _ => rows
                .OrderBy(row => row.Booking.EventDate)
                .ThenBy(row => row.Booking.TransactionId)
                .ToList()
        };
}
