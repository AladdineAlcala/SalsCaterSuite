using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace SalsCaterSuite.Application.Features.Customers.Commands;

/// <summary>
/// PUT /api/customers/{id} — update an existing customer.
/// <para>
/// The command runs the same <see cref="CreateCustomerRequestDto"/> validator the create
/// command runs (BR-11, spec §7.3). Before this correction the update applied no validation
/// at all, so a blank or an over-length name reached the database and produced a truncation
/// error (a 500) instead of a rejected request.
/// </para>
/// </summary>
public class UpdateCustomerCommand
{
    private readonly ICustomerRepository _customers;
    private readonly IValidator<CreateCustomerRequestDto> _validator;

    public UpdateCustomerCommand(ICustomerRepository customers, IValidator<CreateCustomerRequestDto> validator)
    {
        _customers = customers;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(int id, CreateCustomerRequestDto request, CancellationToken cancellationToken = default)
    {
        // The validation runs first, exactly as it does in CreateCustomerCommand, so an
        // invalid payload is rejected before anything is written (B7).
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var customer = await _customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return new CustomerNotFoundResult("Customer not found.");
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
