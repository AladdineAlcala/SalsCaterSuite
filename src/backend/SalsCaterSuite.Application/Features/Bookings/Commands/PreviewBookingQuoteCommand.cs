using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Commands;

/// <summary>
/// POST /api/bookings/quote — the server-computed quote for the Reservation Workbench
/// (contract §14.3, BR-31).
/// <para>
/// The quote is a <b>what-if</b>: it prices a proposed booking and <b>persists nothing</b>. Its
/// figures are produced by the same <see cref="BookingPricingService"/> the create path uses,
/// so the figure a workbench quotes and the figure a confirmed booking freezes are the same
/// number produced by the same code.
/// </para>
/// </summary>
public class PreviewBookingQuoteCommand
{
    private readonly IValidator<BookingQuoteRequest> _validator;
    private readonly ICustomerRepository _customers;
    private readonly IPackageRepository _packages;
    private readonly IAreaRepository _areas;
    private readonly IDiscountRepository _discounts;
    private readonly IPackageAreaCoverageRepository _coverages;
    private readonly IBookingRepository _bookings;
    private readonly BookingPricingService _pricing;
    private readonly BookingLineItemResolver _lineItems;
    private readonly BookingMenuCourseSetBuilder _courseSets;
    private readonly BookingTierEstimateService _tierEstimates;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public PreviewBookingQuoteCommand(
        IValidator<BookingQuoteRequest> validator,
        ICustomerRepository customers,
        IPackageRepository packages,
        IAreaRepository areas,
        IDiscountRepository discounts,
        IPackageAreaCoverageRepository coverages,
        IBookingRepository bookings,
        BookingPricingService pricing,
        BookingLineItemResolver lineItems,
        BookingMenuCourseSetBuilder courseSets,
        BookingTierEstimateService tierEstimates,
        IBookingPolicyProvider policy,
        IDateTime clock)
    {
        _validator = validator;
        _customers = customers;
        _packages = packages;
        _areas = areas;
        _discounts = discounts;
        _coverages = coverages;
        _bookings = bookings;
        _pricing = pricing;
        _lineItems = lineItems;
        _courseSets = courseSets;
        _tierEstimates = tierEstimates;
        _policy = policy;
        _clock = clock;
    }

    public async Task<Result<BookingQuoteResponse>> ExecuteAsync(
        BookingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BookingQuoteResponse>.Failure(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);

        // ── The client ────────────────────────────────────────────────────────────────
        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result<BookingQuoteResponse>.Failure("Customer not found.");
        }

        var clientIsValid = !customer.IsDeleted;

        // ── The tier ─────────────────────────────────────────────────────────────────
        var package = await _packages.GetByIdAsync(request.PackageId, cancellationToken);
        if (package is null)
        {
            return Result<BookingQuoteResponse>.Failure($"Package with ID {request.PackageId} not found.");
        }

        var tierIsActive = package.IsActive && !package.IsDeleted;

        // ── The municipality ─────────────────────────────────────────────────────────
        Area? area = null;
        if (request.ExtendedAreaId.HasValue)
        {
            area = await _areas.GetLiveByIdAsync(request.ExtendedAreaId.Value, cancellationToken);
            if (area is null)
            {
                return Result<BookingQuoteResponse>.Failure(
                    $"Municipality with ID {request.ExtendedAreaId} not found.");
            }
        }

        // ── BR-40: the surcharge is limited to the Regular and Premier tier types ─────
        // The test is on the tier's own type column, never on its description (DEC-156). The
        // tier's own per-guest amount is part of the same surcharge (the owner's 2026-10-24
        // request), so either source makes the tier subject to the eligibility rule.
        var carriesExtendedAmount = request.ExtendedAreaId.HasValue
            || (package.ExtendedLocationAmount ?? 0m) > 0m;

        if (request.ApplyExtendedAmount
            && carriesExtendedAmount
            && !PackageSurchargeEligibility.IsEligible(package.PackageType))
        {
            return Result<BookingQuoteResponse>.Failure(
                PackageSurchargeEligibility.RejectionMessage(package.PackageType));
        }

        // ── The discount ─────────────────────────────────────────────────────────────
        Discount? discount = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var matches = await _discounts.GetActiveByCodeAsync(
                request.DiscountCode.Trim(), cancellationToken);

            if (matches.Count == 0)
            {
                return Result<BookingQuoteResponse>.Failure("The discount code was not found.");
            }

            if (matches.Count > 1)
            {
                return Result<BookingQuoteResponse>.Failure("The discount code is ambiguous.");
            }

            discount = matches[0];
        }

        // ── The money ────────────────────────────────────────────────────────────────
        // The dishes and the add-on lines are validated and frozen through the SAME resolver
        // the create path uses, so the quote and the confirmed booking cannot diverge
        // (BR-31, DEC-153).
        var lineItems = await _lineItems.ResolveAsync(
            package.PackageId,
            request.NoOfPerson,
            request.Selections,
            request.Addons,
            cancellationToken);

        if (!lineItems.Succeeded)
        {
            return Result<BookingQuoteResponse>.Failure(lineItems.Errors);
        }

        var pricePerHead = package.AmountPax ?? 0m;

        var pricing = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = pricePerHead,
            NumberOfHeads = request.NoOfPerson,
            ExtendedAreaRate = area?.AdditionalRate,
            PackageExtendedLocationRate = package.ExtendedLocationAmount,
            ApplyExtendedAmount = request.ApplyExtendedAmount,
            Discount = discount,
            DiscountCode = string.IsNullOrWhiteSpace(request.DiscountCode) ? null : request.DiscountCode.Trim(),
            AddonsTotal = lineItems.Data!.AddonsTotal,
            StatedDepositAmount = request.DepositAmount
        });

        // ── The tier's rules and the proposed selection (contract §14.3) ─────────────
        var courseSet = await _courseSets.BuildAsync(
            package.PackageId, request.NoOfPerson, cancellationToken);

        var window = BookingMenuWindow.Resolve(
            request.EventDate, _policy.MenuSelectionLockDaysBeforeEvent, today);

        var courses = BookingSelectionProjector.QuoteCourses(courseSet);
        var selection = BookingSelectionProjector.QuoteSelection(
            courseSet, lineItems.Data!.Selections, window, pricePerHead);

        var initialPaymentAmount = request.InitialPaymentAmount ?? 0m;
        var outstandingBalance = Math.Max(0m, pricing.NetAmountDue - initialPaymentAmount);

        // ── The tier's planning estimates (DEC-103) ──────────────────────────────────
        var estimates = await _tierEstimates.GetAsync(new[] { package.PackageId }, cancellationToken);
        var estimate = estimates.TryGetValue(package.PackageId, out var tierEstimate) ? tierEstimate : null;

        // ── The coverage matrix (BR-29) ──────────────────────────────────────────────
        var coverages = await _coverages.GetLiveCoveragesAsync(package.PackageId, cancellationToken);
        bool? isCovered = null;
        if (coverages.Count > 0)
        {
            isCovered = request.ExtendedAreaId.HasValue
                && coverages.Any(coverage => coverage.AreaId == request.ExtendedAreaId.Value);
        }

        // ── The capacity gauge (KPI-38, DEC-102) ─────────────────────────────────────
        var capacity = await BuildCapacityAsync(request, today, cancellationToken);

        // ── The five pre-dispatch checks (BR-29) ─────────────────────────────────────
        var checks = BuildChecks(
            clientIsValid,
            tierIsActive,
            estimate?.CourseRuleCount ?? 0,
            isCovered,
            request.ExtendedAreaId,
            pricing);

        var quote = new BookingQuoteResponse
        {
            PackageName = package.Description,
            PackageCode = package.PackageCode,
            PackageType = package.PackageType,
            MinPax = package.MinPax,
            IsTierActive = tierIsActive,
            PricePerHead = pricePerHead,
            BaseAmount = pricing.BaseAmount,

            ExtendedAreaId = area?.AreaId,
            ExtendedAreaName = area?.AreaDetails,
            ExtendedAreaRate = pricing.ExtendedAreaRate,
            PackageExtendedLocationRate = pricing.PackageExtendedLocationRate,
            ExtendedAreaAmount = pricing.ExtendedAreaAmount,

            DiscountCode = pricing.DiscountCode,
            DiscountType = pricing.DiscountType,
            DiscountPercentage = pricing.DiscountPercentage,
            DiscountAmount = pricing.DiscountAmount,
            DiscountSource = pricing.DiscountSource,
            DiscountBandLabel = pricing.DiscountBandLabel,
            DiscountBandRatePerHead = pricing.DiscountBandRatePerHead,

            GrossAmount = pricing.GrossAmount,
            NetAmountDue = pricing.NetAmountDue,
            AddonsTotal = pricing.AddonsTotal,
            IncludedVatAmount = pricing.IncludedVatAmount,
            VatPercentage = pricing.VatPercentage,

            RequiredDepositPercentage = pricing.RequiredDepositPercentage,
            RequiredDepositAmount = pricing.RequiredDepositAmount,
            DepositAmount = pricing.DepositAmount,

            InitialPaymentAmount = initialPaymentAmount,
            OutstandingBalance = outstandingBalance,

            HeadcountFloorNote = BuildHeadcountFloorNote(package.MinPax, request.NoOfPerson),

            Capacity = capacity,
            StandardRunFoodCostPerPax = estimate?.StandardRunFoodCostPerPax,
            BaseGrossProfitPercentage = estimate?.BaseGrossProfitPercentage,

            Coverage = new BookingQuoteCoverageResponse
            {
                IsCovered = isCovered,
                CoveredAreaCount = coverages.Count
            },

            Checks = checks,

            Courses = courses,
            Selection = selection
        };

        return Result<BookingQuoteResponse>.Success(quote);
    }

    /// <summary>
    /// The five named checks of BR-29. Four are advisory — they warn and never block. Only
    /// <c>TierActive</c> blocks, because a deactivated tier is not offered.
    /// </summary>
    private static List<BookingQuoteCheckResponse> BuildChecks(
        bool clientIsValid,
        bool tierIsActive,
        int courseRuleCount,
        bool? isCovered,
        int? extendedAreaId,
        BookingPricingResult pricing) =>
        new List<BookingQuoteCheckResponse>
        {
            new()
            {
                Key = "ClientValidated",
                Label = "Client validated",
                IsSatisfied = clientIsValid,
                IsBlocking = false,
                Detail = clientIsValid
                    ? "The client exists and is not archived."
                    : "The client is archived and cannot carry a new booking."
            },
            new()
            {
                Key = "TierActive",
                Label = "Tier is offered",
                IsSatisfied = tierIsActive,
                IsBlocking = true,
                Detail = tierIsActive
                    ? "The tier is active and may be booked."
                    : "The tier is deactivated and cannot be booked."
            },
            new()
            {
                Key = "CoursesSpecified",
                Label = "Courses specified",
                IsSatisfied = courseRuleCount > 0,
                IsBlocking = false,
                Detail = courseRuleCount > 0
                    ? $"The tier carries {courseRuleCount} course rule(s)."
                    : "The tier carries no course rule yet."
            },
            new()
            {
                Key = "MunicipalityCovered",
                Label = "Municipality covered",
                IsSatisfied = isCovered ?? false,
                IsBlocking = false,
                Detail = isCovered is null
                    ? "The tier carries no coverage matrix — the coverage is unknown."
                    : isCovered.Value
                        ? "The municipality is in the tier's coverage matrix."
                        : "The municipality is not in the tier's coverage matrix."
            },
            new()
            {
                Key = "DepositMatched",
                Label = "Deposit matched",
                IsSatisfied = pricing.RequiredDepositAmount is null
                    || pricing.EffectiveDeposit >= pricing.RequiredDepositAmount.Value,
                IsBlocking = false,
                Detail = pricing.RequiredDepositAmount is null
                    ? "No required deposit percentage is configured — the required deposit is not stated."
                    : pricing.EffectiveDeposit >= pricing.RequiredDepositAmount.Value
                        ? "The agreed deposit covers the required deposit."
                        : "The agreed deposit is below the required deposit."
            }
        };

    /// <summary>
    /// The advisory headcount-floor note (BR-16). A floor on the contract, not a charge, and it
    /// never blocks.
    /// </summary>
    private static string? BuildHeadcountFloorNote(int? minPax, int noOfPerson)
    {
        if (minPax is null || minPax.Value <= 0)
        {
            return null;
        }

        return noOfPerson < minPax.Value
            ? $"The headcount is below the tier's minimum of {minPax.Value} guests."
            : null;
    }

    /// <summary>
    /// The near-term commissary load against the configured capacity (KPI-38, DEC-102). The
    /// proposal's own headcount is included when its event date falls inside the window,
    /// because the gauge answers "can the commissary serve this event?".
    /// </summary>
    private async Task<BookingRegisterCapacityResponse> BuildCapacityAsync(
        BookingQuoteRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var windowEnd = today.AddDays(BookingCapacityWindow.Days);

        var live = await _bookings.GetLiveBookingsInRangeAsync(
            today, windowEnd.AddDays(-1), cancellationToken);

        var committedPax = live.Sum(booking => booking.NumberOfHeads);

        if (request.EventDate >= today && request.EventDate < windowEnd)
        {
            committedPax += request.NoOfPerson;
        }

        var capacity = _policy.MaximumCapacityPax;

        return new BookingRegisterCapacityResponse
        {
            CommittedPax = committedPax,
            CapacityPax = capacity,
            UtilisationPercentage = capacity.HasValue && capacity.Value > 0
                ? BookingAmountDueCalculator.Round2(committedPax / (decimal)capacity.Value * 100m)
                : null,
            IsConfigured = capacity.HasValue
        };
    }
}
