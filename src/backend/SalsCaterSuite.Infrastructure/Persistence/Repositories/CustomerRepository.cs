namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Customers.DTOs;
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
/// </summary>
public class CustomerRepository : ICustomerRepository
{
    private readonly SalsCaterSuiteDbContext _context;

    public CustomerRepository(SalsCaterSuiteDbContext context)
    {
        _context = context;
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
        return await _context.Customers
            .AsNoTracking()
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Payments)
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
                TotalReceivables = c.Bookings!
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Select(b => new
                    {
                        b.AmountDue,
                        AmountPaid = b.Payments!.Sum(p => p.AmountPaid)
                    })
                    .Where(x => x.AmountDue > x.AmountPaid)
                    .Sum(x => x.AmountDue - x.AmountPaid)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetProjectionByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
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
                TotalReceivables = c.Bookings!
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Select(b => new
                    {
                        b.AmountDue,
                        AmountPaid = b.Payments!.Sum(p => p.AmountPaid)
                    })
                    .Where(x => x.AmountDue > x.AmountPaid)
                    .Sum(x => x.AmountDue - x.AmountPaid)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
