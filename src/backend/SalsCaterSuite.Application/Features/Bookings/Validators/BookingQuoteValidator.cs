using FluentValidation;
using SalsCaterSuite.Application.Features.Bookings.DTOs;

namespace SalsCaterSuite.Application.Features.Bookings.Validators;

/// <summary>
/// The validator of the workbench quote (contract §14.3, spec §3.12).
/// <para>
/// The quote prices a <b>proposed</b> booking, so the same field rules as the create path
/// apply — a quote that could never be created would be a misleading figure.
/// </para>
/// </summary>
public class BookingQuoteValidator : AbstractValidator<BookingQuoteRequestDto>
{
    public BookingQuoteValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("A valid customer is required.");

        RuleFor(x => x.PackageId)
            .GreaterThan(0)
            .WithMessage("A valid package is required.");

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

        RuleFor(x => x.EndTime)
            .GreaterThanOrEqualTo(x => x.StartTime)
            .WithMessage("The programme's end time must not be before its start time.");

        RuleFor(x => x.ExtendedAreaId)
            .GreaterThan(0)
            .When(x => x.ExtendedAreaId.HasValue)
            .WithMessage("When an extended area is specified it must be a valid municipality ID.");

        RuleFor(x => x.ExtendedAreaId)
            .NotNull()
            .When(x => x.ApplyExtendedAmount)
            .WithMessage("A municipality must be selected when the extended area amount is applied.");

        RuleFor(x => x.DiscountCode)
            .MaximumLength(50)
            .When(x => x.DiscountCode is not null)
            .WithMessage("The discount code must not exceed 50 characters.");

        RuleFor(x => x.DepositAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DepositAmount.HasValue)
            .WithMessage("The deposit amount must be zero or greater.");

        RuleFor(x => x.InitialPaymentAmount)
            .GreaterThan(0)
            .When(x => x.InitialPaymentAmount.HasValue)
            .WithMessage("The initial payment amount must be greater than 0.");

        RuleFor(x => x.CallTime)
            .Must((request, callTime) => !callTime.HasValue || callTime.Value <= request.StartTime)
            .WithMessage("The call time must not be later than the programme's start time.");

        RuleFor(x => x.PackoutTime)
            .Must((request, packoutTime) => !packoutTime.HasValue || packoutTime.Value >= request.EndTime)
            .WithMessage("The pack-out time must not be earlier than the programme's end time.");
    }
}
