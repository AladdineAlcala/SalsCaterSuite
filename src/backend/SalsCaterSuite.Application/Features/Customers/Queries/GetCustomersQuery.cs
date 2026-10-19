using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>GET /api/customers — list customers with an optional search term.</summary>
public class GetCustomersQuery
{
    private readonly ICustomerRepository _customers;

    public GetCustomersQuery(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Result<List<CustomerDto>>> ExecuteAsync(string? search, CancellationToken cancellationToken = default)
    {
        var customers = await _customers.GetListProjectionAsync(search, cancellationToken);

        return Result<List<CustomerDto>>.Success(customers);
    }
}
