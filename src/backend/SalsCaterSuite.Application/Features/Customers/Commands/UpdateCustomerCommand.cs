using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace SalsCaterSuite.Application.Features.Customers.Commands;

/// <summary>PUT /api/customers/{id} — update an existing customer.</summary>
public class UpdateCustomerCommand
{
    private readonly ICustomerRepository _customers;

    public UpdateCustomerCommand(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<Result> ExecuteAsync(int id, CreateCustomerRequestDto request, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure("Customer not found.");
        }

        customer.LastName = request.LastName;
        customer.FirstName = request.FirstName;
        customer.Middle = request.Middle;
        customer.Address = request.Address;
        customer.Contact1 = request.Contact1;
        customer.Contact2 = request.Contact2;
        customer.Company = request.Company;

        await _customers.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
