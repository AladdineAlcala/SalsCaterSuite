using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;
using SalsCaterSuite.Application.Features.Dashboard.Models;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.Services;

/// <summary>
/// The operations-dashboard aggregation (REQ-DSH-01, spec §6).
/// <para><b>Authoritative basis.</b> Every financial figure is computed here, server-side
/// (DEC-002), from the same sources the rest of the API uses:</para>
/// <list type="bullet">
/// <item><description>Net amount due comes from <see cref="BookingAmountDueCalculator"/> through
/// its single <c>Calculate(Booking, decimal)</c> overload — the identical argument set the
/// payment ledger uses, so a dashboard-vs-ledger divergence is structurally impossible
/// (DEC-042, AC-B3). The stored <c>Booking.AmountDue</c> column is <b>not</b> used.</description></item>
/// <item><description>Amount paid is <c>Σ Payment.AmountPaid</c> — the authoritative sum, never
/// the denormalized <c>Booking.AmountPaid</c> cache (DEC-012).</description></item>
/// <item><description>Balance is <c>max(0, net amount due − Σ payments)</c> (BR-03).</description></item>
/// </list>
/// <para><b>Soft-deleted rows.</b> The exclusion of soft-deleted bookings, payments and menus
/// is applied here and in the dashboard repository methods only. No global query filter is
/// introduced (DEC-038, spec §6.13).</para>
/// </summary>
public class DashboardService : IDashboardService
{
    /// <summary>The number of upcoming bookings the schedule tile shows (spec §7).</summary>
    private const int UpcomingBookingLimit = 5;

    /// <summary>The number of calendar days covered by the trailing-collections tile (KPI-17).</summary>
    private const int TrailingCollectionDays = 7;

    /// <summary>The label used for menus that are not assigned to a course (KPI-16).</summary>
    private const string UncategorisedCourseName = "Uncategorised";

    private readonly IBookingRepository _bookings;
    private readonly IPaymentRepository _payments;
    private readonly IMenuRepository _menus;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly IFoodCostBenchmarkProvider _benchmarks;
    private readonly IDateTime _dateTime;

    public DashboardService(
        IBookingRepository bookings,
        IPaymentRepository payments,
        IMenuRepository menus,
        BookingAmountDueCalculator calculator,
        IFoodCostBenchmarkProvider benchmarks,
        IDateTime dateTime)
    {
        _bookings = bookings;
        _payments = payments;
        _menus = menus;
        _calculator = calculator;
        _benchmarks = benchmarks;
        _dateTime = dateTime;
    }

    public async Task<Result<DashboardResponse>> GetAsync(DashboardPeriod period, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_dateTime.UtcNow);
        var benchmark = _benchmarks.Current;

        // ── Populations ────────────────────────────────────────────────────────────────
        // R2: the period population (event date in the period, not cancelled/expired).
        var periodBookings = await _bookings.GetDashboardPeriodBookingsAsync(
            period.StartDate, period.EndDate, cancellationToken);

        // R3: the receivables population (every non-cancelled booking).
        var receivableBookings = await _bookings.GetDashboardReceivablesBookingsAsync(cancellationToken);

        // R5: the upcoming schedule (event date from today on, not cancelled/expired).
        var upcomingBookings = await _bookings.GetDashboardUpcomingBookingsAsync(today, cancellationToken);

        // R1: the cash-basis flows.
        var periodCollections = await _payments.GetRangeAggregateAsync(
            period.StartDateTime, period.EndDateTime, cancellationToken);
        var previousCollections = await _payments.GetRangeAggregateAsync(
            period.PreviousPeriod.StartDateTime, period.PreviousPeriod.EndDateTime, cancellationToken);
        var trailingCollections = await _payments.GetRangeAggregateAsync(
            today.AddDays(-(TrailingCollectionDays - 1)).ToDateTime(TimeOnly.MinValue),
            today.ToDateTime(TimeOnly.MaxValue),
            cancellationToken);

        // R4: the menu-catalogue aggregates.
        var menuAggregate = await _menus.GetDashboardCostingAggregateAsync(cancellationToken);
        var courseAggregates = await _menus.GetDashboardCourseCostAggregatesAsync(cancellationToken);
        var courseNames = (await _menus.GetCourseProjectionsAsync(cancellationToken))
            .ToDictionary(c => c.CourseId, c => c.CourseName);

        // ── KPI-07 / KPI-01 / KPI-08 / KPI-04 / KPI-09 — the period population ──────────
        var periodEntries = periodBookings
            .Select(b =>
            {
                var paid = AmountPaid(b);
                return new BookingEntry(b, paid, NetAmountDue(b, paid));
            })
            .ToList();

        // DEC-177: commitment is lifecycle Confirmed only. DepositPaid and FullyPaid are
        // payment states and no longer make a Pending booking committed.
        var committedCount = periodEntries.Count(e => e.Booking.Status == BookingStatus.Confirmed);
        var tentativeCount = periodEntries.Count(e => e.Booking.Status == BookingStatus.Pending);
        var otherCount = periodEntries.Count(e =>
            e.Booking.Status is BookingStatus.InProgress or BookingStatus.Completed or BookingStatus.NoShow or BookingStatus.Rescheduled);

        var contractedValue = periodEntries.Sum(e => e.NetAmountDue);
        var bookingCount = periodEntries.Count;
        var averageBookingValue = bookingCount > 0
            ? Round(contractedValue / bookingCount)
            : (decimal?)null;

        // ── KPI-02 / KPI-10 / KPI-11 — collections ─────────────────────────────────────
        var collectionsAmount = periodCollections.TotalAmount;
        var previousPeriodAmount = previousCollections.TotalAmount;
        var changePercentage = previousPeriodAmount > 0m
            ? Round((collectionsAmount - previousPeriodAmount) / previousPeriodAmount * 100m)
            : (decimal?)null;
        var collectionRatioPercentage = contractedValue > 0m
            ? Round(collectionsAmount / contractedValue * 100m)
            : (decimal?)null;

        // ── KPI-12 / KPI-13 — accounts receivable and its aging ─────────────────────────
        var receivableEntries = receivableBookings
            .Select(b =>
            {
                var paid = AmountPaid(b);
                var net = NetAmountDue(b, paid);
                return new BookingEntry(b, paid, net) { Balance = Math.Max(0m, net - paid) };
            })
            .Where(e => e.Balance > 0m)
            .ToList();

        var outstandingAmount = receivableEntries.Sum(e => e.Balance);
        var agingBuckets = BuildAgingBuckets(receivableEntries, today);
        var overdueCount = agingBuckets
            .Where(b => b.Key is AgingKeyDays31To60 or AgingKeyDays61To90 or AgingKeyOver90Days)
            .Sum(b => b.BookingCount);
        var overdueAmount = agingBuckets
            .Where(b => b.Key is AgingKeyDays31To60 or AgingKeyDays61To90 or AgingKeyOver90Days)
            .Sum(b => b.Amount);

        // ── KPI-14 — dish-costing coverage ─────────────────────────────────────────────
        var totalMenuCount = menuAggregate.TotalCount;
        var costedMenuCount = menuAggregate.CostedCount;
        var coveragePercentage = totalMenuCount > 0
            ? Round((decimal)costedMenuCount / totalMenuCount * 100m)
            : 0m;

        // ── KPI-15 — catalogue gross margin ─────────────────────────────────────────────
        var catalogueRevenue = menuAggregate.CatalogueRevenueAmount;
        var catalogueFoodCost = menuAggregate.CatalogueFoodCostAmount;
        var catalogueMargin = catalogueRevenue - catalogueFoodCost;
        var catalogueMarginPercentage = catalogueRevenue > 0m
            ? Round(catalogueMargin / catalogueRevenue * 100m)
            : (decimal?)null;
        var varianceToBenchmark = catalogueMarginPercentage.HasValue
            ? Round(catalogueMarginPercentage.Value - benchmark.TargetMarginPercentage)
            : (decimal?)null;

        // ── KPI-16 — food cost by course category ───────────────────────────────────────
        var totalCategoryFoodCost = courseAggregates.Sum(c => c.FoodCostAmount);
        var foodCostByCategory = courseAggregates
            .Select(c => new DashboardFoodCostByCategoryResponse
            {
                CourseId = c.CourseId,
                CourseName = ResolveCourseName(c.CourseId, courseNames),
                FoodCostAmount = c.FoodCostAmount,
                SharePercentage = totalCategoryFoodCost > 0m
                    ? Round(c.FoodCostAmount / totalCategoryFoodCost * 100m)
                    : 0m,
                MenuCount = c.MenuCount
            })
            .OrderByDescending(c => c.FoodCostAmount)
            .ThenBy(c => c.CourseName, StringComparer.Ordinal)
            .ToList();

        // ── KPI-18 / KPI-19 — the ledger tiles ──────────────────────────────────────────
        var awaitingDeposit = receivableBookings
            .Select(b =>
            {
                var paid = AmountPaid(b);
                return new BookingEntry(b, paid, NetAmountDue(b, paid));
            })
            .Where(e => e.AmountPaid == 0m)
            .ToList();

        var dto = new DashboardResponse
        {
            Period = new DashboardPeriodResponse
            {
                Year = period.Year,
                Month = period.Month,
                Label = period.Label,
                StartDate = period.StartDate,
                EndDate = period.EndDate
            },
            Benchmarks = new DashboardBenchmarksResponse
            {
                TargetFoodCostPercentage = benchmark.TargetFoodCostPercentage,
                TargetMarginPercentage = benchmark.TargetMarginPercentage,
                IsConfigured = benchmark.IsConfigured
            },
            Bookings = new DashboardBookingsResponse
            {
                TotalCount = bookingCount,
                CommittedCount = committedCount,
                TentativeCount = tentativeCount,
                OtherCount = otherCount,
                ContractedValue = contractedValue,
                TotalPax = periodEntries.Sum(e => e.Booking.NumberOfHeads)
            },
            Revenue = new DashboardRevenueResponse
            {
                NetContractedRevenue = contractedValue,
                AverageBookingValue = averageBookingValue,
                BookingCount = bookingCount
            },
            Collections = new DashboardCollectionsResponse
            {
                Amount = collectionsAmount,
                ReceiptCount = periodCollections.PaymentCount,
                PreviousPeriodAmount = previousPeriodAmount,
                ChangePercentage = changePercentage,
                CollectionRatioPercentage = collectionRatioPercentage
            },
            Receivables = new DashboardReceivablesResponse
            {
                OutstandingAmount = outstandingAmount,
                BookingCount = receivableEntries.Count,
                OverdueCount = overdueCount,
                OverdueAmount = overdueAmount,
                AgingBuckets = agingBuckets
            },
            DishCosting = new DashboardDishCostingResponse
            {
                TotalMenuCount = totalMenuCount,
                CostedMenuCount = costedMenuCount,
                UncostedMenuCount = totalMenuCount - costedMenuCount,
                CoveragePercentage = coveragePercentage
            },
            Margin = new DashboardMarginResponse
            {
                CatalogueRevenueAmount = catalogueRevenue,
                CatalogueFoodCostAmount = catalogueFoodCost,
                CatalogueMarginAmount = catalogueMargin,
                CatalogueMarginPercentage = catalogueMarginPercentage,
                VarianceToBenchmarkPercentage = varianceToBenchmark
            },
            FoodCostByCategory = foodCostByCategory,
            UpcomingBookings = upcomingBookings
                .Take(UpcomingBookingLimit)
                .Select(b =>
                {
                    var paid = AmountPaid(b);
                    var net = NetAmountDue(b, paid);
                    return new DashboardUpcomingBookingResponse
                    {
                        TransactionId = b.TransactionId,
                        BookingNumber = b.BookingNumber,
                        CustomerName = b.Customer?.FullName ?? "Unknown",
                        Occasion = b.Occasion ?? "Event",
                        EventDate = b.EventDate,
                        NumberOfHeads = b.NumberOfHeads,
                        Status = b.Status,
                        PackageName = b.PackageName,
                        NetAmountDue = net,
                        AmountPaid = paid,
                        Balance = Math.Max(0m, net - paid)
                    };
                })
                .ToList(),
            Ledger = new DashboardLedgerResponse
            {
                CollectedLast7DaysAmount = trailingCollections.TotalAmount,
                CollectedLast7DaysReceiptCount = trailingCollections.PaymentCount,
                AwaitingDepositBookingCount = awaitingDeposit.Count,
                AwaitingDepositAmount = awaitingDeposit.Sum(e => e.NetAmountDue),
                // KPI-19: the payment rows whose payment date falls in the active period.
                ReceiptsIssuedCount = periodCollections.PaymentCount
            },
            GeneratedAt = _dateTime.UtcNow
        };

        return Result<DashboardResponse>.Success(dto);
    }

    /// <summary>
    /// The authoritative amount paid for a booking: the sum of its <c>Payment</c> rows
    /// (DEC-012). Soft-deleted payments are excluded (AC-B9) — the dashboard's exclusion is
    /// scoped to the dashboard and never changes the ledger's own figures.
    /// </summary>
    private static decimal AmountPaid(Booking booking) =>
        booking.Payments.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid);

    /// <summary>
    /// The net amount due, computed through the single shared calculator overload so the
    /// dashboard and the ledger can never disagree (DEC-042).
    /// </summary>
    private decimal NetAmountDue(Booking booking, decimal amountPaid) =>
        _calculator.Calculate(booking, amountPaid).NetAmountDue;

    private static IReadOnlyList<DashboardAgingBucketResponse> BuildAgingBuckets(
        IReadOnlyList<BookingEntry> entries,
        DateOnly today)
    {
        var notYetDue = new BucketAccumulator(AgingKeyNotYetDue, "Not yet due");
        var days1To30 = new BucketAccumulator(AgingKeyDays1To30, "1-30 days");
        var days31To60 = new BucketAccumulator(AgingKeyDays31To60, "31-60 days");
        var days61To90 = new BucketAccumulator(AgingKeyDays61To90, "61-90 days");
        var over90Days = new BucketAccumulator(AgingKeyOver90Days, "90+ days");

        foreach (var entry in entries)
        {
            var eventDate = entry.Booking.EventDate;

            if (eventDate >= today)
            {
                notYetDue.Add(entry.Balance);
                continue;
            }

            // The due date is the event date (DEC-041), so the days overdue are measured from it.
            var daysOverdue = today.DayNumber - eventDate.DayNumber;

            if (daysOverdue <= 30)
            {
                days1To30.Add(entry.Balance);
            }
            else if (daysOverdue <= 60)
            {
                days31To60.Add(entry.Balance);
            }
            else if (daysOverdue <= 90)
            {
                days61To90.Add(entry.Balance);
            }
            else
            {
                over90Days.Add(entry.Balance);
            }
        }

        return new[]
        {
            notYetDue.ToDto(),
            days1To30.ToDto(),
            days31To60.ToDto(),
            days61To90.ToDto(),
            over90Days.ToDto()
        };
    }

    private static string ResolveCourseName(int? courseId, IReadOnlyDictionary<int, string> courseNames)
    {
        if (courseId.HasValue && courseNames.TryGetValue(courseId.Value, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return UncategorisedCourseName;
    }

    /// <summary>
    /// Rounds to the two decimal places the reporting standard requires (DEC-003).
    /// Monetary totals are <b>not</b> rounded: they are the exact output of the shared
    /// calculator path, so the dashboard reconciles to the cent with the per-booking ledger.
    /// </summary>
    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private const string AgingKeyNotYetDue = "NotYetDue";
    private const string AgingKeyDays1To30 = "Days1To30";
    private const string AgingKeyDays31To60 = "Days31To60";
    private const string AgingKeyDays61To90 = "Days61To90";
    private const string AgingKeyOver90Days = "Over90Days";

    /// <summary>A booking together with its authoritative payment sum and net amount due.</summary>
    private sealed class BookingEntry
    {
        public BookingEntry(Booking booking, decimal amountPaid, decimal netAmountDue)
        {
            Booking = booking;
            AmountPaid = amountPaid;
            NetAmountDue = netAmountDue;
        }

        public Booking Booking { get; }

        public decimal AmountPaid { get; }

        public decimal NetAmountDue { get; }

        public decimal Balance { get; init; }
    }

    /// <summary>Accumulates the bookings and the amount of one aging bucket.</summary>
    private sealed class BucketAccumulator
    {
        private readonly string _key;
        private readonly string _label;

        public BucketAccumulator(string key, string label)
        {
            _key = key;
            _label = label;
        }

        public int BookingCount { get; private set; }

        public decimal Amount { get; private set; }

        public void Add(decimal amount)
        {
            BookingCount++;
            Amount += amount;
        }

        public DashboardAgingBucketResponse ToDto() => new()
        {
            Key = _key,
            Label = _label,
            BookingCount = BookingCount,
            Amount = Amount
        };
    }
}
