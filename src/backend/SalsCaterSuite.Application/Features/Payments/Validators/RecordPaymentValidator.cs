using FluentValidation;
using SalsCaterSuite.Application.Features.Payments.DTOs;

namespace SalsCaterSuite.Application.Features.Payments.Validators;

public class RecordPaymentValidator : AbstractValidator<RecordPaymentRequestDto>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.TransactionId)
            .GreaterThan(0)
            .WithMessage("Valid transaction ID is required.");

        RuleFor(x => x.AmountPaid)
            .GreaterThan(0)
            .WithMessage("Payment amount must be greater than zero.");

        RuleFor(x => x.Particular)
            .NotEmpty()
            .MaximumLength(150)
            .WithMessage("Payment particular / description is required.");

        RuleFor(x => x.PaymentMeans)
            .IsInEnum()
            .WithMessage("Payment means is required (e.g., Cash, Check, GCash, LDDAP).");
    }
}

