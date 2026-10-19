using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>GET /api/customers/{id} — a single customer projection.</summary>
public class GetCustomerByIdQuery
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdQuery(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Result<CustomerDto>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetProjectionByIdAsync(id, cancellationToken);

        return customer is null
            ? Result<CustomerDto>.Failure("Customer not found.")
            : Result<CustomerDto>.Success(customer);
    }
}
