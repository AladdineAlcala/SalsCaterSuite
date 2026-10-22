namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Customers;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

/// <summary>
/// Hand-written fake of <see cref="ICustomerRepository"/> (DEC-037). No EF Core is
/// involved; the fake reproduces the behaviours the Application use cases depend on:
/// reference tracking for in-place updates, IDENTITY assignment on save, and
/// navigation "include" semantics for the statement loader.
/// <para>
/// The receivables projections are re-expressed with in-memory LINQ purely for the
/// Application-layer fakes. The authoritative DEC-025 server-side aggregate is asserted
/// against EF Core in <c>CustomerRepositoryTests</c> (InMemory) and, end to end, by the
/// integration <c>ReceivablesTests</c> on real SQL Server.
/// </para>
/// <para>
/// The Customer Master directory projection (spec §5.1) is mirrored here with exactly the
/// same rules the EF Core implementation applies, so the Application-layer tests assert the
/// real behaviour and not a simplified imitation.
/// </para>
/// </summary>
public sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly ICustomerAgingService _aging;
    private readonly IDateTime _clock;

    public FakeCustomerRepository(
        FakeSalsCaterSuiteDataStore store,
        BookingAmountDueCalculator? calculator = null,
        ICustomerAgingService? aging = null,
        IDateTime? clock = null)
    {
        _store = store;
        _calculator = calculator ?? new BookingAmountDueCalculator();
        _clock = clock ?? new FakeDateTime(DateTime.UtcNow);
        _aging = aging ?? new CustomerAgingService(_clock);
    }

    /// <summary>Number of successful saves, for asserting the commit boundary.</summary>
    public int SaveCount { get; private set; }

    public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Customers.FirstOrDefault(c => c.CustomerId == customerId));
    }

    public Task<Customer?> GetWithStatementDetailsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = _store.Customers.FirstOrDefault(c => c.CustomerId == customerId);
        if (customer is null)
        {
            return Task.FromResult<Customer?>(null);
        }

        var bookings = _store.Bookings.Where(b => b.CustomerId == customerId).ToList();
        foreach (var booking in bookings)
        {
            booking.Payments = _store.Payments
                .Where(p => p.TransactionId == booking.TransactionId)
                .ToList();

            booking.Refunds = _store.Refunds
                .Where(r => r.TransactionId == booking.TransactionId)
                .ToList();

            if (booking.PackageId.HasValue)
            {
                booking.Package = _store.Packages
                    .FirstOrDefault(p => p.PackageId == booking.PackageId.Value);
            }
        }

        customer.Bookings = bookings;

        return Task.FromResult<Customer?>(customer);
    }

    public Task<List<CustomerDto>> GetListProjectionAsync(string? search, CancellationToken cancellationToken = default)
    {
        IEnumerable<Customer> customers = _store.Customers;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            customers = customers.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.Contains(term)));
        }

        var result = customers
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(Project)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<CustomerDto?> GetProjectionByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = _store.Customers.FirstOrDefault(c => c.CustomerId == customerId);
        return Task.FromResult(customer is null ? null : ProjectDetail(customer));
    }

    public Task<CustomerDirectoryDto> GetDirectoryProjectionAsync(
        string? search,
        string? customerType,
        string? arState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // The population is the search-filtered set — the type and AR-state filters are not
        // applied to it, because the quick-view counts describe exactly that set.
        var population = GetDirectoryPopulation(search);

        var quickViewCounts = BuildQuickViewCounts(population);

        var filtered = ApplyDirectoryFilters(population, customerType, arState);

        var totalCount = filtered.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Task.FromResult(new CustomerDirectoryDto
        {
            Summary = BuildSummary(filtered),
            QuickViewCounts = quickViewCounts,
            Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            GeneratedAt = _clock.UtcNow
        });
    }

    public Task<List<CustomerDirectoryItemDto>> GetDirectoryItemsAsync(
        string? search,
        string? customerType,
        string? arState,
        CancellationToken cancellationToken = default)
    {
        var population = GetDirectoryPopulation(search);

        return Task.FromResult(ApplyDirectoryFilters(population, customerType, arState));
    }

    /// <summary>The search-filtered population of the directory (spec §4.3).</summary>
    private List<CustomerDirectoryItemDto> GetDirectoryPopulation(string? search)
    {
        var today = _aging.Today;

        IEnumerable<Customer> customers = _store.Customers.Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            customers = customers.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.ToLower().Contains(term)));
        }

        return customers
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.CustomerId)
            .Select(c => ProjectDirectoryItem(c, today))
            .ToList();
    }

    private List<CustomerDirectoryItemDto> ApplyDirectoryFilters(
        List<CustomerDirectoryItemDto> population,
        string? customerType,
        string? arState)
    {
        var rows = population;

        if (string.Equals(customerType?.Trim(), CustomerClassification.Corporate, StringComparison.OrdinalIgnoreCase))
        {
            rows = rows
                .Where(r => string.Equals(r.CustomerType, CustomerClassification.Corporate, StringComparison.Ordinal))
                .ToList();
        }
        else if (string.Equals(customerType?.Trim(), CustomerClassification.Private, StringComparison.OrdinalIgnoreCase))
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

    private CustomerQuickViewCountsDto BuildQuickViewCounts(List<CustomerDirectoryItemDto> population) => new()
    {
        All = population.Count,
        Corporate = population.Count(r => string.Equals(r.CustomerType, CustomerClassification.Corporate, StringComparison.Ordinal)),
        Private = population.Count(r => string.Equals(r.CustomerType, CustomerClassification.Private, StringComparison.Ordinal)),
        WithOpenBalance = population.Count(r => r.OutstandingBalance > 0m),
        Overdue30 = population.Count(r => _aging.IsOverdue30(r.LedgerState))
    };

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _store.Customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        // Mirror the database: IDENTITY customer ids are assigned on INSERT.
        foreach (var customer in _store.Customers.Where(c => c.CustomerId == 0).ToList())
        {
            _store.AssignCustomerId(customer);
        }

        return Task.FromResult(1);
    }

    private CustomerDto Project(Customer customer)
    {
        var activeBookings = _store.Bookings
            .Where(b => b.CustomerId == customer.CustomerId && b.Status != BookingStatus.Cancelled)
            .ToList();

        // AR = Σ max(0, NetAmountDue − PaymentsReceived) over non-cancelled bookings (DEC-025).
        var totalReceivables = activeBookings
            .Select(b => new
            {
                b.AmountDue,
                AmountPaid = _store.Payments
                    .Where(p => p.TransactionId == b.TransactionId)
                    .Sum(p => p.AmountPaid)
            })
            .Where(x => x.AmountDue > x.AmountPaid)
            .Sum(x => x.AmountDue - x.AmountPaid);

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
            TotalBookings = activeBookings.Count,
            TotalReceivables = totalReceivables
        };
    }

    /// <summary>
    /// The single-customer projection — the Customer Master's extended <see cref="CustomerDto"/>.
    /// </summary>
    private CustomerDto ProjectDetail(Customer customer)
    {
        var today = _aging.Today;
        var allBookings = _store.Bookings.Where(b => b.CustomerId == customer.CustomerId).ToList();
        var activeBookings = allBookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        decimal contracted = 0m;
        decimal paid = 0m;
        decimal outstanding = 0m;
        var agingBalances = new List<CustomerAgingBalance>();

        foreach (var booking in activeBookings)
        {
            var bookingPaid = _store.Payments
                .Where(p => p.TransactionId == booking.TransactionId)
                .Sum(p => p.AmountPaid);

            var breakdown = _calculator.Calculate(booking, bookingPaid);

            contracted += breakdown.NetAmountDue;
            paid += bookingPaid;
            outstanding += breakdown.RemainingBalance;

            if (breakdown.RemainingBalance > 0m)
            {
                agingBalances.Add(new CustomerAgingBalance(booking.EventDate, breakdown.RemainingBalance));
            }
        }

        var state = _aging.GetWorstBucketKey(agingBalances, today);

        var upcoming = activeBookings
            .Where(b => b.EventDate >= today)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .FirstOrDefault();

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
            TotalBookings = activeBookings.Count,
            TotalReceivables = outstanding,
            CustomerType = CustomerClassification.Derive(customer.Company),
            BookingCounts = new CustomerBookingCountsDto
            {
                Total = activeBookings.Count,
                Past = activeBookings.Count(b => b.EventDate < today),
                Upcoming = activeBookings.Count(b => b.EventDate >= today),
                Completed = allBookings.Count(b => b.Status == BookingStatus.Completed),
                Cancelled = allBookings.Count(b => b.Status == BookingStatus.Cancelled)
            },
            TotalContractedValue = contracted,
            TotalPaid = paid,
            OutstandingBalance = outstanding,
            LedgerState = state,
            LedgerStateLabel = _aging.GetBucketLabel(state),
            UpcomingBooking = upcoming is null
                ? null
                : new CustomerUpcomingBookingDto
                {
                    TransactionId = upcoming.TransactionId,
                    BookingNumber = upcoming.BookingNumber,
                    Occasion = upcoming.Occasion ?? "Event",
                    EventDate = upcoming.EventDate,
                    AmountDue = _calculator.Calculate(upcoming, 0m).NetAmountDue
                }
        };
    }

    private CustomerDirectoryItemDto ProjectDirectoryItem(Customer customer, DateOnly today)
    {
        var allBookings = _store.Bookings.Where(b => b.CustomerId == customer.CustomerId).ToList();
        var activeBookings = allBookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        decimal contracted = 0m;
        decimal paid = 0m;
        decimal outstanding = 0m;
        var agingBalances = new List<CustomerAgingBalance>();

        foreach (var booking in activeBookings)
        {
            var bookingPaid = _store.Payments
                .Where(p => p.TransactionId == booking.TransactionId)
                .Sum(p => p.AmountPaid);

            var breakdown = _calculator.Calculate(booking, bookingPaid);

            contracted += breakdown.NetAmountDue;
            paid += bookingPaid;
            outstanding += breakdown.RemainingBalance;

            if (breakdown.RemainingBalance > 0m)
            {
                agingBalances.Add(new CustomerAgingBalance(booking.EventDate, breakdown.RemainingBalance));
            }
        }

        var state = _aging.GetWorstBucketKey(agingBalances, today);

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
            CustomerType = CustomerClassification.Derive(customer.Company),
            BookingCounts = new CustomerBookingCountsDto
            {
                Total = activeBookings.Count,
                Past = activeBookings.Count(b => b.EventDate < today),
                Upcoming = activeBookings.Count(b => b.EventDate >= today),
                Completed = allBookings.Count(b => b.Status == BookingStatus.Completed),
                Cancelled = allBookings.Count(b => b.Status == BookingStatus.Cancelled)
            },
            TotalContractedValue = contracted,
            TotalPaid = paid,
            OutstandingBalance = outstanding,
            LedgerState = state,
            LedgerStateLabel = _aging.GetBucketLabel(state)
        };
    }

    private CustomerDirectorySummaryDto BuildSummary(List<CustomerDirectoryItemDto> rows)
    {
        var totalClients = rows.Count;
        var contracted = rows.Sum(r => r.TotalContractedValue);
        var overdueRows = rows.Where(r => _aging.IsOverdue30(r.LedgerState)).ToList();

        return new CustomerDirectorySummaryDto
        {
            TotalClients = totalClients,
            ActiveArAmount = rows.Sum(r => r.OutstandingBalance),
            ClientsWithBalance = rows.Count(r => r.OutstandingBalance > 0m),
            TotalContractedValue = contracted,
            AverageValuePerClient = totalClients > 0
                ? Math.Round(contracted / totalClients, 2, MidpointRounding.AwayFromZero)
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

    private static string? NormalizeArState(string? arState)
    {
        if (string.IsNullOrWhiteSpace(arState))
        {
            return null;
        }

        return arState.Trim().ToLowerInvariant() switch
        {
            "clean" => CustomerAgingBucket.ArStateClean,
            "open" => CustomerAgingBucket.ArStateOpen,
            "overdue30" => CustomerAgingBucket.ArStateOverdue30,
            "overdue60" => CustomerAgingBucket.ArStateOverdue60,
            _ => null
        };
    }
}
