using FluentValidation;
using SalsCaterSuite.Application.Features.Customers.DTOs;

namespace  SalsCaterSuite.Application.Features.Customers.Validators;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerRequestDto>
{
    public CreateCustomerValidator()
    {
         RuleFor(x => x)
            .Custom((customer, context) =>
            {
                if (string.IsNullOrWhiteSpace(customer.LastName))
                {
                    context.AddFailure(
                        nameof(customer.LastName),
                        "Last name is required.");
                }

                if (string.IsNullOrWhiteSpace(customer.FirstName))
                {
                    context.AddFailure(
                        nameof(customer.FirstName),
                        "First name is required.");
                }
            });

        // BR-11 / spec §7.3: the maximum lengths of the name columns are part of the
        // database schema (nvarchar(100) / nvarchar(100) / nvarchar(50)). Without these rules
        // an over-length name reached SQL Server and produced a truncation error (a 500)
        // instead of a rejected request.
        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.Middle)
            .MaximumLength(50)
            .WithMessage("Middle name must not exceed 50 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(200)
            .WithMessage("Address must not exceed 200 characters.");

        RuleFor(x => x.Contact1)
            .MaximumLength(15)
            .WithMessage("Contact number 1 must not exceed 15 characters.");

        RuleFor(x => x.Contact2)
            .MaximumLength(15)
            .WithMessage("Contact number 2 must not exceed 15 characters.");

        RuleFor(x => x.Company)
            .MaximumLength(150)
            .WithMessage("Company name must not exceed 150 characters.");

        
    }
}
