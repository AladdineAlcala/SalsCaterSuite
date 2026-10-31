using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>
/// GET /api/customers/directory — the paged customer directory with its AR ledger state
/// (spec §5.1, REQ-CUS-01).
/// <para>
/// The use case owns the paging contract: <c>page</c> is clamped to 1 and <c>pageSize</c> to
/// 1…100. An unrecognised <c>customerType</c> or <c>arState</c> is ignored rather than
/// rejected (spec §5.1).
/// </para>
/// </summary>
public class GetCustomerDirectoryQuery
{
    /// <summary>The default page size of the directory (spec §5.1).</summary>
    public const int DefaultPageSize = 10;

    /// <summary>The largest page the directory will serve.</summary>
    public const int MaxPageSize = 100;

    private readonly ICustomerRepository _customers;

    public GetCustomerDirectoryQuery(ICustomerRepository customers)
    {
        _customers = customers;
    }

    /// <summary>
    /// The paged directory. The <see cref="CustomerDirectoryResponse.Summary"/> describes the whole
    /// filtered result set, not the returned page, while the
    /// <see cref="CustomerDirectoryResponse.QuickViewCounts"/> describe the search-filtered
    /// population with the type and AR-state filters ignored (spec §4.3).
    /// </summary>
    public async Task<Result<CustomerDirectoryResponse>> ExecuteAsync(
        string? search,
        string? customerType,
        string? arState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var effectivePage = ClampPage(page);
        var effectivePageSize = ClampPageSize(pageSize);

        var directory = await _customers.GetDirectoryProjectionAsync(
            search, customerType, arState, effectivePage, effectivePageSize, cancellationToken);

        return Result<CustomerDirectoryResponse>.Success(directory);
    }

    /// <summary>
    /// The whole filtered result set, without paging. The XLSX export of the directory is the
    /// current filtered result set, not the current page (spec §5.5).
    /// </summary>
    public async Task<Result<List<CustomerDirectoryItemResponse>>> ExecuteUnpagedAsync(
        string? search,
        string? customerType,
        string? arState,
        CancellationToken cancellationToken = default)
    {
        var rows = await _customers.GetDirectoryItemsAsync(
            search, customerType, arState, cancellationToken);

        return Result<List<CustomerDirectoryItemResponse>>.Success(rows);
    }

    private static int ClampPage(int page) => page < 1 ? 1 : page;

    private static int ClampPageSize(int pageSize) => pageSize switch
    {
        < 1 => 1,
        > MaxPageSize => MaxPageSize,
        _ => pageSize
    };
}
