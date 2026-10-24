namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

/// <summary>
/// Hand-written fake of <see cref="IBookingRepository"/> (DEC-037). No EF Core is involved;
/// the fake reproduces the EF behaviours the use cases depend on: reference tracking,
/// IDENTITY assignment on save, and collection fix-up for the "with details" loaders.
/// </summary>
public sealed class FakeBookingRepository : IBookingRepository
{
    private readonly FakeSalsCaterSuiteDataStore _store;

    private int _nextBookingMenuId = 1;
    private int _nextBookingAddOnId = 1;

    public FakeBookingRepository(FakeSalsCaterSuiteDataStore store)
    {
        _store = store;
    }

    /// <summary>All bookings currently in the store — the assertion surface for tests.</summary>
    public IReadOnlyList<Booking> Bookings => _store.Bookings;

    /// <summary>Number of successful saves, for asserting the commit sequence.</summary>
    public int SaveCount { get; private set; }

    public Task<Booking?> GetByIdAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Bookings.FirstOrDefault(b => b.TransactionId == transactionId));
    }

    public Task<Booking?> GetWithDetailsAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = _store.Bookings.FirstOrDefault(b => b.TransactionId == transactionId);
        if (booking is null)
        {
            return Task.FromResult<Booking?>(null);
        }

        HydrateCustomerAndPackage(booking);
        booking.Payments = _store.Payments.Where(p => p.TransactionId == transactionId).ToList();
        booking.Refunds = _store.Refunds.Where(r => r.TransactionId == transactionId).ToList();

        return Task.FromResult<Booking?>(booking);
    }

    public Task<Booking?> GetWithPaymentsAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = _store.Bookings.FirstOrDefault(b => b.TransactionId == transactionId);
        if (booking is null)
        {
            return Task.FromResult<Booking?>(null);
        }

        booking.Payments = _store.Payments.Where(p => p.TransactionId == transactionId).ToList();

        return Task.FromResult<Booking?>(booking);
    }

    /// <summary>
    /// The line-item dossier (contract §15.1, §16.3). Mirrors the EF implementation's includes:
    /// the Client, the tier, the municipality, the live Payments and the <b>whole</b> selection
    /// and add-on collections — soft-deleted rows included, because a write resurrects them
    /// (DEC-134).
    /// </summary>
    public Task<Booking?> GetWithLineItemsAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        var booking = _store.Bookings.FirstOrDefault(b => b.TransactionId == transactionId);
        if (booking is null)
        {
            return Task.FromResult<Booking?>(null);
        }

        HydrateCustomerAndPackage(booking);
        booking.Payments = _store.Payments
            .Where(p => p.TransactionId == transactionId && !p.IsDeleted)
            .ToList();
        HydrateLineItems(booking);

        return Task.FromResult<Booking?>(booking);
    }

    public Task<IReadOnlyList<Booking>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Booking> result = _store.Bookings
            .Where(b => b.EventDate >= fromDate && b.EventDate <= toDate)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Booking>> GetForCalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b =>
                b.Status != BookingStatus.Cancelled &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
            .OrderBy(b => b.EventDate)
            .ToList();

        foreach (var booking in result)
        {
            HydrateCustomerAndPackage(booking);
            booking.Payments = _store.Payments.Where(p => p.TransactionId == booking.TransactionId).ToList();
        }

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    /// <summary>
    /// The dashboard's period population (spec §7 R2) — mirrors the EF implementation's
    /// filters exactly: event date in range, not cancelled, not expired, not soft-deleted.
    /// </summary>
    public Task<IReadOnlyList<Booking>> GetDashboardPeriodBookingsAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToList();

        Hydrate(result);

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    /// <summary>
    /// The dashboard's receivables population (spec §7 R3) — every non-cancelled,
    /// non-soft-deleted booking.
    /// </summary>
    public Task<IReadOnlyList<Booking>> GetDashboardReceivablesBookingsAsync(CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b => !b.IsDeleted && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.TransactionId)
            .ToList();

        Hydrate(result);

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    /// <summary>
    /// The dashboard's upcoming schedule (spec §7 R5) — event date from the given date on,
    /// not cancelled, not expired, not soft-deleted, ordered by event date then transaction id.
    /// </summary>
    public Task<IReadOnlyList<Booking>> GetDashboardUpcomingBookingsAsync(DateOnly fromDate, CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= fromDate)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToList();

        Hydrate(result);

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    /// <summary>
    /// The register's base population (REQ-BKG-05) — mirrors the EF implementation's filters
    /// exactly: not soft-deleted, not cancelled, narrowed by the search term and the inclusive
    /// event-date range.
    /// </summary>
    public Task<IReadOnlyList<Booking>> GetRegisterPopulationAsync(
        string? search,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b => !b.IsDeleted && b.Status != BookingStatus.Cancelled)
            .Where(b => !fromDate.HasValue || b.EventDate >= fromDate.Value)
            .Where(b => !toDate.HasValue || b.EventDate <= toDate.Value)
            .Where(b => MatchesSearch(b, search))
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToList();

        Hydrate(result);

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    /// <summary>
    /// The live bookings inside the inclusive event-date range (KPI-38) — not soft-deleted and
    /// not cancelled.
    /// </summary>
    public Task<IReadOnlyList<Booking>> GetLiveBookingsInRangeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var result = _store.Bookings
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.EventDate >= fromDate &&
                b.EventDate <= toDate)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId)
            .ToList();

        Hydrate(result);

        return Task.FromResult<IReadOnlyList<Booking>>(result);
    }

    public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        _store.Bookings.Add(booking);
        return Task.CompletedTask;
    }
    public void Update(Booking booking)
    {
        // The store tracks by reference, so an in-place mutation is already visible.
    }

    public void Remove(Booking booking)
    {
        _store.Bookings.Remove(booking);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        // Mirror the database: the IDENTITY TransactionId is assigned on INSERT.
        foreach (var booking in _store.Bookings.Where(b => b.TransactionId == 0).ToList())
        {
            _store.AssignTransactionId(booking);
        }

        // Mirror EF Core's graph traversal. The repository contract is that ONE SaveChangesAsync
        // commits the whole pending change set (DEC-037's atomicity rule), so a payment that
        // hangs off a booking's own collection is inserted with that booking, in the same
        // commit, and carries the booking's generated key. The create path relies on exactly
        // this: the initial payment is staged on the booking's collection so EF Core fixes up
        // the foreign key and inserts both rows in one transaction (BR-33).
        foreach (var booking in _store.Bookings)
        {
            foreach (var payment in booking.Payments)
            {
                if (_store.Payments.Any(existing => ReferenceEquals(existing, payment)))
                {
                    continue;
                }

                payment.TransactionId = booking.TransactionId;
                _store.Payments.Add(payment);
            }

            // The selection and add-on rows are IDENTITY-assigned child rows, inserted with the
            // booking in the same commit (DEC-134, DEC-151).
            foreach (var menu in booking.Menus)
            {
                menu.TransactionId = booking.TransactionId;
                if (menu.BookingMenuId == 0)
                {
                    menu.BookingMenuId = ++_nextBookingMenuId;
                }
            }

            foreach (var addon in booking.AddOns)
            {
                addon.TransactionId = booking.TransactionId;
                if (addon.BookingAddOnId == 0)
                {
                    addon.BookingAddOnId = ++_nextBookingAddOnId;
                }
            }
        }

        return Task.FromResult(1);
    }

    private void HydrateCustomerAndPackage(Booking booking)
    {
        var customer = _store.Customers.FirstOrDefault(c => c.CustomerId == booking.CustomerId);
        if (customer is not null)
        {
            booking.Customer = customer;
        }

        if (booking.PackageId.HasValue)
        {
            var package = _store.Packages.FirstOrDefault(p => p.PackageId == booking.PackageId.Value);
            if (package is not null)
            {
                booking.Package = package;
            }
        }
    }

    /// <summary>
    /// Mirrors the EF <c>Include(b =&gt; b.Customer).Include(b =&gt; b.Payments)</c> of the
    /// dashboard repository methods, plus the register's line-item includes.
    /// </summary>
    private void Hydrate(IReadOnlyList<Booking> bookings)
    {
        foreach (var booking in bookings)
        {
            HydrateCustomerAndPackage(booking);
            booking.Payments = _store.Payments.Where(p => p.TransactionId == booking.TransactionId).ToList();
            HydrateLineItems(booking);
        }
    }

    /// <summary>
    /// Mirrors EF Core's relationship fix-up for the selection and add-on rows: every child
    /// points back at the booking that owns it. The fake's bookings hold their own rows (the
    /// aggregate root owns the collection), so nothing has to be loaded — only the back
    /// references are fixed up, exactly as the change tracker would. The add-on lines also carry
    /// their catalogue row, which the <c>GET /api/bookings/{id}/addons</c> projection reads
    /// (contract §16.3).
    /// </summary>
    private void HydrateLineItems(Booking booking)
    {
        foreach (var menu in booking.Menus)
        {
            menu.Booking = booking;
            menu.TransactionId = booking.TransactionId;
        }

        foreach (var addon in booking.AddOns)
        {
            addon.Booking = booking;
            addon.TransactionId = booking.TransactionId;
            addon.AddonDetail ??= _store.AddonDetails
                .FirstOrDefault(detail => detail.AddonId == addon.AddonId);

            // The real repository includes AddonDetail.AddonCategory (contract 16.3), so the
            // presenter can publish the category name; the fake must hydrate it too.
            if (addon.AddonDetail is { } detailRow)
            {
                detailRow.AddonCategory ??= _store.AddonCategories
                    .FirstOrDefault(category =>
                        category.AddonCategoryId == detailRow.AddonCategoryId);
            }
        }
    }

    /// <summary>
    /// The register's search: a case-insensitive substring over the booking number, the occasion,
    /// the venue and the customer's first name, last name and company.
    /// </summary>
    private bool MatchesSearch(Booking booking, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim().ToLower();

        var customer = _store.Customers.FirstOrDefault(c => c.CustomerId == booking.CustomerId);

        return Contains(booking.BookingNumber, term)
            || Contains(booking.Occasion, term)
            || Contains(booking.Venue, term)
            || Contains(customer?.FirstName, term)
            || Contains(customer?.LastName, term)
            || Contains(customer?.Company, term);
    }

    private static bool Contains(string? value, string term) =>
        value is not null && value.ToLower().Contains(term);
}
