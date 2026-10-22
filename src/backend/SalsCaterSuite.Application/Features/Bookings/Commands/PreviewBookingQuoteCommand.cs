using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
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
    private readonly IValidator<BookingQuoteRequestDto> _validator;
    private readonly ICustomerRepository _customers;
    private readonly IPackageRepository _packages;
    private readonly IAreaRepository _areas;
    private readonly IDiscountRepository _discounts;
    private readonly IPackageAreaCoverageRepository _coverages;
    private readonly IBookingRepository _bookings;
    private readonly BookingPricingService _pricing;
    private readonly BookingTierEstimateService _tierEstimates;
    private readonly IBookingPolicyProvider _policy;
    private readonly IDateTime _clock;

    public PreviewBookingQuoteCommand(
        IValidator<BookingQuoteRequestDto> validator,
        ICustomerRepository customers,
        IPackageRepository packages,
        IAreaRepository areas,
        IDiscountRepository discounts,
        IPackageAreaCoverageRepository coverages,
        IBookingRepository bookings,
        BookingPricingService pricing,
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
        _tierEstimates = tierEstimates;
        _policy = policy;
        _clock = clock;
    }

    public async Task<Result<BookingQuoteDto>> ExecuteAsync(
        BookingQuoteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<BookingQuoteDto>.Failure(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);

        // ── The client ────────────────────────────────────────────────────────────────
        var customer = await _customers.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result<BookingQuoteDto>.Failure("Customer not found.");
        }

        var clientIsValid = !customer.IsDeleted;

        // ── The tier ─────────────────────────────────────────────────────────────────
        var package = await _packages.GetByIdAsync(request.PackageId, cancellationToken);
        if (package is null)
        {
            return Result<BookingQuoteDto>.Failure($"Package with ID {request.PackageId} not found.");
        }

        var tierIsActive = package.IsActive && !package.IsDeleted;

        // ── The municipality ─────────────────────────────────────────────────────────
        Area? area = null;
        if (request.ExtendedAreaId.HasValue)
        {
            area = await _areas.GetLiveByIdAsync(request.ExtendedAreaId.Value, cancellationToken);
            if (area is null)
            {
                return Result<BookingQuoteDto>.Failure(
                    $"Municipality with ID {request.ExtendedAreaId} not found.");
            }
        }

        // ── The discount ─────────────────────────────────────────────────────────────
        Discount? discount = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var matches = await _discounts.GetActiveByCodeAsync(
                request.DiscountCode.Trim(), cancellationToken);

            if (matches.Count == 0)
            {
                return Result<BookingQuoteDto>.Failure("The discount code was not found.");
            }

            if (matches.Count > 1)
            {
                return Result<BookingQuoteDto>.Failure("The discount code is ambiguous.");
            }

            discount = matches[0];
        }

        // ── The money ────────────────────────────────────────────────────────────────
        var pricePerHead = package.AmountPax ?? 0m;

        var pricing = _pricing.Price(new BookingPricingInput
        {
            PricePerHead = pricePerHead,
            NumberOfHeads = request.NoOfPerson,
            ExtendedAreaRate = area?.AdditionalRate,
            ApplyExtendedAmount = request.ApplyExtendedAmount,
            Discount = discount,
            DiscountCode = string.IsNullOrWhiteSpace(request.DiscountCode) ? null : request.DiscountCode.Trim(),
            StatedDepositAmount = request.DepositAmount
        });

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

        var quote = new BookingQuoteDto
        {
            PackageName = package.Description,
            PackageType = package.PackageType,
            MinPax = package.MinPax,
            IsTierActive = tierIsActive,
            PricePerHead = pricePerHead,
            BaseAmount = pricing.BaseAmount,

            ExtendedAreaId = area?.AreaId,
            ExtendedAreaName = area?.AreaDetails,
            ExtendedAreaRate = pricing.ExtendedAreaRate,
            ExtendedAreaAmount = pricing.ExtendedAreaAmount,

            DiscountCode = pricing.DiscountCode,
            DiscountType = pricing.DiscountType,
            DiscountPercentage = pricing.DiscountPercentage,
            DiscountAmount = pricing.DiscountAmount,

            GrossAmount = pricing.GrossAmount,
            NetAmountDue = pricing.NetAmountDue,

            RequiredDepositPercentage = pricing.RequiredDepositPercentage,
            RequiredDepositAmount = pricing.RequiredDepositAmount,
            DepositAmount = pricing.DepositAmount,

            InitialPaymentAmount = initialPaymentAmount,
            OutstandingBalance = outstandingBalance,

            HeadcountFloorNote = BuildHeadcountFloorNote(package.MinPax, request.NoOfPerson),

            Capacity = capacity,
            StandardRunFoodCostPerPax = estimate?.StandardRunFoodCostPerPax,
            BaseGrossProfitPercentage = estimate?.BaseGrossProfitPercentage,

            Coverage = new BookingQuoteCoverageDto
            {
                IsCovered = isCovered,
                CoveredAreaCount = coverages.Count
            },

            Checks = checks
        };

        return Result<BookingQuoteDto>.Success(quote);
    }

    /// <summary>
    /// The five named checks of BR-29. Four are advisory — they warn and never block. Only
    /// <c>TierActive</c> blocks, because a deactivated tier is not offered.
    /// </summary>
    private static List<BookingQuoteCheckDto> BuildChecks(
        bool clientIsValid,
        bool tierIsActive,
        int courseRuleCount,
        bool? isCovered,
        int? extendedAreaId,
        BookingPricingResult pricing) =>
        new List<BookingQuoteCheckDto>
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
    private async Task<BookingRegisterCapacityDto> BuildCapacityAsync(
        BookingQuoteRequestDto request,
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
}
