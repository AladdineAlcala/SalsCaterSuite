using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.DTOs;

namespace SalsCaterSuite.Application.Features.Bookings.Validators;

public class UpdateBookingCoreValidator : AbstractValidator<UpdateBookingCoreRequestDto>
{
    public UpdateBookingCoreValidator()
    {
        RuleFor(x => x.NumberOfHeads)
            .GreaterThan(0)
            .WithMessage("Number of heads must be greater than zero.");

        RuleFor(x => x.EventDate)
            .NotEmpty()
            .WithMessage("Event date is required.");
    }
}
