namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Domain.Entities;

/// <summary>
/// Customer aggregate persistence (DEC-037). Expressed in terms of the
/// <see cref="Customer"/> domain entity and Application projections only — no EF Core
/// type leaks through this contract.
/// <para>
/// The receivables projections (<see cref="GetListProjectionAsync"/>,
/// <see cref="GetProjectionByIdAsync"/>) and the statement loader
/// (<see cref="GetWithStatementDetailsAsync"/>) are deliberately hosted here so the
/// DEC-025 Accounts Receivable aggregate stays a single EF Core-translatable server-side
/// query rather than degrading to client-side evaluation.
/// </para>
/// <para>
/// Atomicity: every repository implementation shares the request-scoped
/// <c>SalsCaterSuiteDbContext</c>, so a single <see cref="SaveChangesAsync"/> commits the
/// entire pending change set.
/// </para>
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>Loads a customer with Bookings, their Payments and Packages (statement of account).</summary>
    Task<Customer?> GetWithStatementDetailsAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customer list projection with the DEC-025 receivables aggregate, optionally filtered
    /// by a case-insensitive search term over last name, first name, company and contact.
    /// </summary>
    Task<List<CustomerDto>> GetListProjectionAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Single-customer projection carrying the DEC-025 receivables aggregate.</summary>
    Task<CustomerDto?> GetProjectionByIdAsync(int customerId, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
