using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;

namespace SalsCaterSuite.Application.Features.Bookings.Validators;

public class UpdateBookingCoreValidator : AbstractValidator<UpdateBookingCoreRequest>
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
