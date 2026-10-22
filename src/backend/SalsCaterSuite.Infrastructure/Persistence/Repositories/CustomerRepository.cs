namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Customers;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

/// <summary>
/// EF Core implementation of the customer repository (DEC-037).
/// <para>
/// The receivables projections are hosted here so the DEC-025 Accounts Receivable
/// aggregate is a single EF Core-translatable server-side query:
/// <c>Σ max(0, NetAmountDue − PaymentsReceived)</c> over non-cancelled bookings.
/// The <c>Where(due &gt; paid)</c> predicate is equivalent to <c>max(0, due − paid)</c>
/// and keeps the expression translatable — EF Core cannot translate SUM over an
/// expression containing a nested correlated SUM, but it does translate this filtered
/// derived-table form.
/// </para>
/// <para>
/// The Customer Master directory projection (spec §5.1) applies the customer-level filters
/// the database can express (soft delete, search) and the ordering in the database, then
/// composes the money figures in the Application layer through the single shared
/// <see cref="BookingAmountDueCalculator"/> overload. The persisted
/// <c>Booking.AmountDue</c> column and the denormalized <c>Booking.AmountPaid</c> cache are
/// never used (DEC-012, DEC-035, DEC-046, BR-10), so the directory, the statement, the
/// ledger and the dashboard report the same money.
/// </para>
/// <para>
/// The projection is produced in <b>one</b> pass: the search-filtered population is loaded
/// once, and the quick-view counts (spec §4.3), the filtered set and its summary are all
/// derived from those same rows — never a query per customer.
/// </para>
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly SalsCaterSuiteDbContext _context;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly ICustomerAgingService _aging;
    private readonly IDateTime _clock;

    public CustomerRepository(
        SalsCaterSuiteDbContext context,
        BookingAmountDueCalculator calculator,
        ICustomerAgingService aging,
        IDateTime clock)
    {
        _context = context;
        _calculator = calculator;
        _aging = aging;
        _clock = clock;
    }

    public async Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
    }

    public async Task<Customer?> GetWithStatementDetailsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        // Single query with eager loading — no N+1. The statement DTO (line building,
        // status classification, ordering) is composed by the Application use case.
        // The refunds are loaded so the statement can report the refunded amount
        // separately (DEC-013).
        return await _context.Customers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Payments)
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Refunds)
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Package)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
    }

    public async Task<List<CustomerDto>> GetListProjectionAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.Contains(term)));
        }

        return await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                LastName = c.LastName,
                FirstName = c.FirstName,
                Middle = c.Middle,
                FullName = c.FullName,
                Address = c.Address,
                Contact1 = c.Contact1,
                Contact2 = c.Contact2,
                Company = c.Company,
                DateRegistered = c.DateRegistered,
                TotalBookings = c.Bookings!.Count(b => b.Status != BookingStatus.Cancelled),
                // Accounts Receivable = Σ max(0, NetAmountDue − PaymentsReceived) over
                // non-cancelled bookings, using the authoritative payment sum (DEC-025).
                // AmountDue is the frozen net amount due, so no pricing is recomputed here.
                // A soft-deleted payment is excluded from the SQL roll-up as well
                // (BR-27, DEC-111) — it is not money the business holds.
                TotalReceivables = c.Bookings!
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Select(b => new
                    {
                        b.AmountDue,
                        AmountPaid = b.Payments!.Where(p => !p.IsDeleted).Sum(p => p.AmountPaid)
                    })
                    .Where(x => x.AmountDue > x.AmountPaid)
                    .Sum(x => x.AmountDue - x.AmountPaid)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetProjectionByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Payments)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var today = _aging.Today;
        var rollUp = RollUp(customer, today);

        return new CustomerDto
        {
            CustomerId = customer.CustomerId,
            LastName = customer.LastName,
            FirstName = customer.FirstName,
            Middle = customer.Middle,
            FullName = customer.FullName,
            Address = customer.Address,
            Contact1 = customer.Contact1,
            Contact2 = customer.Contact2,
            Company = customer.Company,
            DateRegistered = customer.DateRegistered,
            TotalBookings = rollUp.Counts.Total,
            // The accounts receivable, computed through the shared calculator path so the
            // customer profile, the directory and the dashboard can never disagree
            // (DEC-025, DEC-042).
            TotalReceivables = rollUp.OutstandingBalance,
            CustomerType = DeriveCustomerType(customer.Company),
            BookingCounts = rollUp.Counts,
            TotalContractedValue = rollUp.ContractedValue,
            TotalPaid = rollUp.TotalPaid,
            OutstandingBalance = rollUp.OutstandingBalance,
            LedgerState = rollUp.LedgerState,
            LedgerStateLabel = rollUp.LedgerStateLabel,
            UpcomingBooking = ComposeUpcomingBooking(rollUp.ActiveBookings, today)
        };
    }

    public async Task<CustomerDirectoryDto> GetDirectoryProjectionAsync(
        string? search,
        string? customerType,
        string? arState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // The population is the search-filtered set — the type and AR-state filters are NOT
        // applied to it, because the quick-view pills describe that population (spec §4.3).
        var population = await GetDirectoryPopulationAsync(search, cancellationToken);

        // The quick-view counts are computed over the population, in the same pass — no
        // extra round trip, and never one query per customer.
        var quickViewCounts = BuildQuickViewCounts(population);

        // The summary and the rows describe the fully filtered set.
        var filtered = ApplyDirectoryFilters(population, customerType, arState);

        var totalCount = filtered.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        // The offset is computed in 64 bits and clamped, so an absurd page number can never
        // overflow into a negative skip.
        var offset = (long)(page - 1) * pageSize;

        return new CustomerDirectoryDto
        {
            Summary = BuildSummary(filtered),
            QuickViewCounts = quickViewCounts,
            Items = filtered.Skip((int)Math.Min(offset, int.MaxValue)).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            GeneratedAt = _clock.UtcNow
        };
    }

    public async Task<List<CustomerDirectoryItemDto>> GetDirectoryItemsAsync(
        string? search,
        string? customerType,
        string? arState,
        CancellationToken cancellationToken = default)
    {
        // The XLSX export is the current filtered result set, not the current page
        // (spec §5.5) — so it honours the very same filters as the directory.
        var population = await GetDirectoryPopulationAsync(search, cancellationToken);

        return ApplyDirectoryFilters(population, customerType, arState);
    }

    /// <summary>
    /// The search-filtered population of the directory: the non-deleted customers that match
    /// the search term, ordered and composed into their directory rows. The type and the
    /// AR-state filters are deliberately not applied here — the quick-view counts are
    /// computed over exactly this set (spec §4.3).
    /// </summary>
    private async Task<List<CustomerDirectoryItemDto>> GetDirectoryPopulationAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var today = _aging.Today;

        var query = _context.Customers
            .AsNoTracking()
            // DEC-050: the Customer Master filters the soft-deleted rows inside its own
            // queries only — no global query filter is introduced (DEC-038).
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // The existing GET /api/customers search semantics: a case-insensitive substring
            // over the last name, the first name, the company and contact 1.
            var term = search.ToLower();
            query = query.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.ToLower().Contains(term)));
        }

        var customers = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.CustomerId)
            .AsSplitQuery()
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Payments)
            .ToListAsync(cancellationToken);

        return customers
            .Select(c => ComposeDirectoryItem(c, today))
            .ToList();
    }

    /// <summary>
    /// The derived classification filter and the AR-state filter (spec §4.3). Both are
    /// applied on the composed rows, because "not blank" is a whitespace-aware test and the
    /// AR state depends on the derived aging state.
    /// </summary>
    private List<CustomerDirectoryItemDto> ApplyDirectoryFilters(
        List<CustomerDirectoryItemDto> population,
        string? customerType,
        string? arState)
    {
        var rows = population;

        if (IsCustomerType(customerType, CustomerClassification.Corporate))
        {
            rows = rows
                .Where(r => string.Equals(r.CustomerType, CustomerClassification.Corporate, StringComparison.Ordinal))
                .ToList();
        }
        else if (IsCustomerType(customerType, CustomerClassification.Private))
        {
            rows = rows
                .Where(r => string.Equals(r.CustomerType, CustomerClassification.Private, StringComparison.Ordinal))
                .ToList();
        }

        var stateFilter = NormalizeArState(arState);
        if (stateFilter is not null)
        {
            rows = rows.Where(r => MatchesArState(r, stateFilter)).ToList();
        }

        return rows;
    }

    /// <summary>
    /// The quick-view pills' live counts (spec §4.3, contract §10.1). They describe the
    /// search-filtered population with the type and AR-state filters ignored, so selecting a
    /// scope never changes what the pills report.
    /// </summary>
    private CustomerQuickViewCountsDto BuildQuickViewCounts(List<CustomerDirectoryItemDto> population) => new()
    {
        All = population.Count,
        Corporate = population.Count(r => string.Equals(r.CustomerType, CustomerClassification.Corporate, StringComparison.Ordinal)),
        Private = population.Count(r => string.Equals(r.CustomerType, CustomerClassification.Private, StringComparison.Ordinal)),
        WithOpenBalance = population.Count(r => r.OutstandingBalance > 0m),
        Overdue30 = population.Count(r => _aging.IsOverdue30(r.LedgerState))
    };

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────── composition helpers ───────────────────────────

    /// <summary>
    /// The customer's derived figures. The money comes from the single shared calculator
    /// overload, so the directory, the statement, the ledger and the dashboard agree to the
    /// cent (DEC-042, DEC-046, AC-B3).
    /// </summary>
    private CustomerRollUp RollUp(Customer customer, DateOnly today)
    {
        var allBookings = customer.Bookings?.ToList() ?? new List<Booking>();
        var activeBookings = allBookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        decimal contractedValue = 0m;
        decimal totalPaid = 0m;
        decimal outstandingBalance = 0m;
        var agingBalances = new List<CustomerAgingBalance>();

        foreach (var booking in activeBookings)
        {
            var paid = AmountPaid(booking);
            var breakdown = _calculator.Calculate(booking, paid);

            contractedValue += breakdown.NetAmountDue;
            totalPaid += paid;
            outstandingBalance += breakdown.RemainingBalance;

            if (breakdown.RemainingBalance > 0m)
            {
                agingBalances.Add(new CustomerAgingBalance(booking.EventDate, breakdown.RemainingBalance));
            }
        }

        var state = _aging.GetWorstBucketKey(agingBalances, today);

        var counts = new CustomerBookingCountsDto
        {
            Total = activeBookings.Count,
            Past = activeBookings.Count(b => b.EventDate < today),
            Upcoming = activeBookings.Count(b => b.EventDate >= today),
            Completed = allBookings.Count(b => b.Status == BookingStatus.Completed),
            Cancelled = allBookings.Count(b => b.Status == BookingStatus.Cancelled)
        };

        return new CustomerRollUp(
            contractedValue,
            totalPaid,
            outstandingBalance,
            state,
            _aging.GetBucketLabel(state),
            counts,
            activeBookings);
    }

    private CustomerDirectoryItemDto ComposeDirectoryItem(Customer customer, DateOnly today)
    {
        var rollUp = RollUp(customer, today);

        return new CustomerDirectoryItemDto
        {
            CustomerId = customer.CustomerId,
            LastName = customer.LastName,
            FirstName = customer.FirstName,
            Middle = customer.Middle,
            FullName = customer.FullName,
            Company = customer.Company,
            Address = customer.Address,
            Contact1 = customer.Contact1,
            Contact2 = customer.Contact2,
            DateRegistered = customer.DateRegistered,
            CustomerType = DeriveCustomerType(customer.Company),
            BookingCounts = rollUp.Counts,
            TotalContractedValue = rollUp.ContractedValue,
            TotalPaid = rollUp.TotalPaid,
            OutstandingBalance = rollUp.OutstandingBalance,
            LedgerState = rollUp.LedgerState,
            LedgerStateLabel = rollUp.LedgerStateLabel
        };
    }

    /// <summary>
    /// The customer's next non-cancelled booking with an event date of today or later
    /// (spec §4.8).
    /// </summary>
    private CustomerUpcomingBookingDto? ComposeUpcomingBooking(
        IReadOnlyList<Booking> activeBookings,
        DateOnly today)
    {
        var upcoming = activeBookings
            .Where(b => b.EventDate >= today)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .FirstOrDefault();

        if (upcoming is null)
        {
            return null;
        }

        var breakdown = _calculator.Calculate(upcoming, AmountPaid(upcoming));

        return new CustomerUpcomingBookingDto
        {
            TransactionId = upcoming.TransactionId,
            BookingNumber = upcoming.BookingNumber,
            Occasion = upcoming.Occasion ?? "Event",
            EventDate = upcoming.EventDate,
            AmountDue = breakdown.NetAmountDue
        };
    }

    /// <summary>
    /// The directory's KPI cards, computed over the <b>whole</b> filtered result set
    /// (spec §4.2 / §5.1).
    /// </summary>
    private CustomerDirectorySummaryDto BuildSummary(List<CustomerDirectoryItemDto> rows)
    {
        var totalClients = rows.Count;
        var activeArAmount = rows.Sum(r => r.OutstandingBalance);
        var contractedValue = rows.Sum(r => r.TotalContractedValue);

        var overdueRows = rows.Where(r => _aging.IsOverdue30(r.LedgerState)).ToList();

        return new CustomerDirectorySummaryDto
        {
            TotalClients = totalClients,
            ActiveArAmount = activeArAmount,
            ClientsWithBalance = rows.Count(r => r.OutstandingBalance > 0m),
            TotalContractedValue = contractedValue,
            // A null means "undefined" — never zero (contract §9.2 rule 6).
            AverageValuePerClient = totalClients > 0
                ? Math.Round(contractedValue / totalClients, 2, MidpointRounding.AwayFromZero)
                : null,
            OverdueAmount = overdueRows.Sum(r => r.OutstandingBalance),
            OverdueClientCount = overdueRows.Count
        };
    }

    private bool MatchesArState(CustomerDirectoryItemDto row, string arState) => arState switch
    {
        CustomerAgingBucket.ArStateClean => row.LedgerState == CustomerAgingBucket.Clean,
        CustomerAgingBucket.ArStateOpen => row.OutstandingBalance > 0m,
        CustomerAgingBucket.ArStateOverdue30 => _aging.IsOverdue30(row.LedgerState),
        CustomerAgingBucket.ArStateOverdue60 => _aging.IsOverdue60(row.LedgerState),
        _ => true
    };

    /// <summary>
    /// The derived account classification (spec §4.4, DEC-051): <c>Corporate</c> when the
    /// company name is not blank, <c>Private</c> otherwise. It is never persisted.
    /// </summary>
    private static string DeriveCustomerType(string? company) =>
        CustomerClassification.Derive(company);

    private static bool IsCustomerType(string? value, string expected) =>
        string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The AR-state filter value, or <c>null</c> when the caller sent something the contract
    /// does not define — an unrecognised value is ignored, never rejected (spec §5.1).
    /// </summary>
    private static string? NormalizeArState(string? arState)
    {
        if (string.IsNullOrWhiteSpace(arState))
        {
            return null;
        }

        var value = arState.Trim();

        return value.ToLowerInvariant() switch
        {
            "clean" => CustomerAgingBucket.ArStateClean,
            "open" => CustomerAgingBucket.ArStateOpen,
            "overdue30" => CustomerAgingBucket.ArStateOverdue30,
            "overdue60" => CustomerAgingBucket.ArStateOverdue60,
            _ => null
        };
    }

    /// <summary>
    /// The authoritative amount paid for a booking: the sum of its <c>Payment</c> rows
    /// (DEC-012). The denormalized <c>Booking.AmountPaid</c> cache is never used.
    /// <para>
    /// A soft-deleted payment is excluded (BR-27, DEC-111) — the rule lives in
    /// <see cref="BookingPaymentPopulation"/>, so the directory, the profile, the statement,
    /// the calendar feed, the ledger and the register cannot drift apart.
    /// </para>
    /// </summary>
    private static decimal AmountPaid(Booking booking) =>
        BookingPaymentPopulation.Collected(booking);

    /// <summary>The customer's derived figures.</summary>
    private sealed record CustomerRollUp(
        decimal ContractedValue,
        decimal TotalPaid,
        decimal OutstandingBalance,
        string LedgerState,
        string LedgerStateLabel,
        CustomerBookingCountsDto Counts,
        IReadOnlyList<Booking> ActiveBookings);
}
