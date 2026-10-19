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