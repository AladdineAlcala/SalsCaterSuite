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

        // ── REQ-BKG-05 extensions (contract §14.4, spec §3.12) ──────────────────

        // The municipality must exist and be live when one is named.
        RuleFor(x => x.ExtendedAreaId)
            .GreaterThan(0)
            .When(x => x.ExtendedAreaId.HasValue)
            .WithMessage("When an extended area is specified it must be a valid municipality ID.");

        // The out-of-town surcharge needs a municipality to be priced against.
        RuleFor(x => x.ExtendedAreaId)
            .NotNull()
            .When(x => x.ApplyExtendedAmount)
            .WithMessage("A municipality must be selected when the extended area amount is applied.");

        // The frozen discount code — the column is nvarchar(50).
        RuleFor(x => x.DiscountCode)
            .MaximumLength(50)
            .When(x => x.DiscountCode is not null)
            .WithMessage("The discount code must not exceed 50 characters.");

        // The deposit is advisory, but it can never be negative (BR-30).
        RuleFor(x => x.DepositAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DepositAmount.HasValue)
            .WithMessage("The deposit amount must be zero or greater.");

        // The dispatch schedule. The call precedes the programme and the pack-out follows it —
        // the only non-vacuous reading of "they must fall on the event day" for a time-of-day
        // column (DEC-106).
        RuleFor(x => x.CallTime)
            .Must((request, callTime) => !callTime.HasValue || callTime.Value <= request.StartDate.TimeOfDay)
            .WithMessage("The call time must not be later than the programme's start time.");

        RuleFor(x => x.PackoutTime)
            .Must((request, packoutTime) => !packoutTime.HasValue || packoutTime.Value >= request.EndDate.TimeOfDay)
            .WithMessage("The pack-out time must not be earlier than the programme's end time.");

        // A draft never carries a payment (spec §2).
        RuleFor(x => x.InitialPayment)
            .Null()
            .When(x => !x.Confirm)
            .WithMessage("Only a confirmed booking may carry an initial payment.");

        // The initial payment itself.
        RuleFor(x => x.InitialPayment)
            .Must((request, payment) =>
                payment is null || payment.AmountPaid > 0m)
            .WithMessage("The initial payment amount must be greater than 0.")
            .When(x => x.InitialPayment is not null);

        RuleFor(x => x.InitialPayment!.CheckNo)
            .MaximumLength(50)
            .When(x => x.InitialPayment is not null && x.InitialPayment.CheckNo is not null)
            .WithMessage("The cheque number must not exceed 50 characters.");

        RuleFor(x => x.InitialPayment!.Notes)
            .MaximumLength(250)
            .When(x => x.InitialPayment is not null && x.InitialPayment.Notes is not null)
            .WithMessage("The payment notes must not exceed 250 characters.");

        RuleFor(x => x.InitialPayment!.Particular)
            .MaximumLength(250)
            .When(x => x.InitialPayment is not null && x.InitialPayment.Particular is not null)
            .WithMessage("The payment description must not exceed 250 characters.");

        // ── REQ-BKG-06/07 (contract §15.3, §16.4) ───────────────────────────────

        // An explicitly EMPTY array is a 400 on create: there is nothing to clear on a booking
        // that does not exist yet, and "absent" already means "none" (contract §15.3).
        RuleFor(x => x.Selections)
            .Must(selections => selections is null || selections.Count > 0)
            .WithMessage(
                "An empty 'selections' array cannot be submitted on create; omit the member to " +
                "book without a dish selection.");

        RuleForEach(x => x.Selections)
            .ChildRules(selection =>
            {
                selection.RuleFor(entry => entry.MenuId)
                    .NotEmpty()
                    .WithMessage("Every selected dish must name a menu.");

                selection.RuleFor(entry => entry.PrepInstruction)
                    .MaximumLength(500)
                    .When(entry => entry.PrepInstruction is not null)
                    .WithMessage("A prep instruction must not exceed 500 characters.");
            })
            .When(x => x.Selections is not null);

        RuleFor(x => x.Addons)
            .Must(addons => addons is null || addons.Count > 0)
            .WithMessage(
                "An empty 'addons' array cannot be submitted on create; omit the member to " +
                "book without add-ons.");

        RuleForEach(x => x.Addons)
            .ChildRules(addon =>
            {
                addon.RuleFor(entry => entry.AddonId)
                    .GreaterThan(0)
                    .WithMessage("Every add-on line must name a catalogue add-on.");

                addon.RuleFor(entry => entry.Amount)
                    .GreaterThanOrEqualTo(0)
                    .When(entry => entry.Amount.HasValue)
                    .WithMessage("An add-on line total must be zero or greater.");
            })
            .When(x => x.Addons is not null);
    }
}
