using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Customers.Queries;

/// <summary>GET /api/customers/{id} — a single customer projection.</summary>
public class GetCustomerByIdQuery
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdQuery(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Result<CustomerResponse>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetProjectionByIdAsync(id, cancellationToken);

        return customer is null
            ? Result<CustomerResponse>.Failure("Customer not found.")
            : Result<CustomerResponse>.Success(customer);
    }
}
