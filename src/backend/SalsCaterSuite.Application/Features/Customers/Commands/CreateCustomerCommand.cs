using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Customers.Commands;

/// <summary>POST /api/customers — create a new customer.</summary>
public class CreateCustomerCommand
{
    private readonly ICustomerRepository _customers;
    private readonly IValidator<CreateCustomerRequest> _validator;

    public CreateCustomerCommand(ICustomerRepository customers, IValidator<CreateCustomerRequest> validator)
    {
        _customers = customers;
        _validator = validator;
    }

    public async Task<Result<int>> ExecuteAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<int>.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var customer = new Customer
        {
            LastName = request.LastName,
            FirstName = request.FirstName,
            Middle = request.Middle,
            Address = request.Address,
            Contact1 = request.Contact1,
            Contact2 = request.Contact2,
            Company = request.Company,
            DateRegistered = DateTime.UtcNow
        };

        await _customers.AddAsync(customer, cancellationToken);
        await _customers.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(customer.CustomerId);
    }
}
