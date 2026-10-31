namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;
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
/// The Customer Master's directory projection
/// (<see cref="GetDirectoryProjectionAsync"/>, <see cref="GetDirectoryItemsAsync"/>) is
/// hosted here for the same reason: the customer-level filters (search, classification,
/// soft-delete) and the ordering are applied by the database, while the money figures are
/// composed in the Application layer through the single shared
/// <c>BookingAmountDueCalculator</c> overload so the directory, the statement, the ledger
/// and the dashboard can never disagree (DEC-042, DEC-046).
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
    Task<List<CustomerResponse>> GetListProjectionAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Single-customer projection carrying the DEC-025 receivables aggregate.</summary>
    Task<CustomerResponse?> GetProjectionByIdAsync(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The paged Customer Master directory (spec §5.1). The returned summary describes the
    /// whole filtered result set, not the returned page; the returned quick-view counts
    /// describe the search-filtered population with the type and AR-state filters ignored.
    /// </summary>
    /// <param name="search">Case-insensitive substring over last name, first name, company and contact 1.</param>
    /// <param name="customerType"><c>Corporate</c> or <c>Private</c>; anything else is ignored.</param>
    /// <param name="arState"><c>Clean</c>, <c>Open</c>, <c>Overdue30</c> or <c>Overdue60</c>; anything else is ignored.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CustomerDirectoryResponse> GetDirectoryProjectionAsync(
        string? search,
        string? customerType,
        string? arState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The whole filtered directory result set, without paging — the XLSX export of the
    /// current filtered result set (spec §5.5).
    /// </summary>
    Task<List<CustomerDirectoryItemResponse>> GetDirectoryItemsAsync(
        string? search,
        string? customerType,
        string? arState,
        CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
