using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.DTOs;

namespace SalsCaterSuite.Application.Features.Bookings.Validators;

public class CreateBookingValidator : AbstractValidator<CreateBookingRequestDto>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("A valid customer is required.");

        RuleFor(x => x.NoOfPerson)
            .GreaterThan(0)
            .WithMessage("Number of persons must be greater than 0.");

        RuleFor(x => x.Occasion)
            .NotEmpty()
            .MaximumLength(150)
            .WithMessage("Occasion is required and must not exceed 150 characters.");

        RuleFor(x => x.Venue)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Venue is required and must not exceed 200 characters.");

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("Event start date and time is required.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("Event end date must be on or after the start date.");

        // PackageId is optional — bespoke bookings may not have one.
        // When provided it must be a valid positive integer.
        RuleFor(x => x.PackageId)
            .GreaterThan(0)
            .When(x => x.PackageId.HasValue)
            .WithMessage("When a package is specified it must be a valid package ID.");

        // OverridePricePerHead, when provided, must be non-negative.
        RuleFor(x => x.OverridePricePerHead)
            .GreaterThanOrEqualTo(0)
            .When(x => x.OverridePricePerHead.HasValue)
            .WithMessage("Price per head override must be zero or greater.");
    }
}
