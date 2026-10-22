namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using FluentValidation.TestHelper;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Validators;
using SalsCaterSuite.Domain.Enums;
using Xunit;

public class RecordPaymentValidatorTests
{
    private readonly RecordPaymentValidator _validator;

    public RecordPaymentValidatorTests()
    {
        _validator = new RecordPaymentValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenTransactionIdIsZeroOrNegative_ShouldHaveValidationError(int txId)
    {
        var model = new RecordPaymentRequestDto { TransactionId = txId };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.TransactionId)
              .WithErrorMessage("Valid transaction ID is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Validate_WhenAmountPaidIsZeroOrNegative_ShouldHaveValidationError(decimal amount)
    {
        var model = new RecordPaymentRequestDto { AmountPaid = amount };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.AmountPaid)
              .WithErrorMessage("Payment amount must be greater than zero.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenParticularIsNullOrEmpty_ShouldHaveValidationError(string? particular)
    {
        var model = new RecordPaymentRequestDto { Particular = particular! };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Particular);
    }

    [Fact]
    public void Validate_WhenParticularExceeds150Chars_ShouldHaveValidationError()
    {
        var model = new RecordPaymentRequestDto { Particular = new string('P', 151) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Particular)
              .WithErrorMessage("Payment particular / description is required.");
    }

    [Fact]
    public void Validate_WhenPaymentMeansIsUndefinedEnumValue_ShouldHaveValidationError()
    {
        var model = new RecordPaymentRequestDto
        {
            PaymentMeans = (PaymentMeans)99
        };

        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PaymentMeans)
              .WithErrorMessage("Payment means is required (e.g., Cash, Check, GCash, LDDAP).");
    }

    [Theory]
    [InlineData(PaymentMeans.Cash)]
    [InlineData(PaymentMeans.Check)]
    [InlineData(PaymentMeans.GCash)]
    [InlineData(PaymentMeans.LDDAP)]
    [InlineData(PaymentMeans.CreditCard)]
    public void Validate_WhenPaymentMeansIsDefinedEnumValue_ShouldAcceptIt(PaymentMeans means)
    {
        var model = new RecordPaymentRequestDto
        {
            TransactionId = 101,
            AmountPaid = 1000m,
            Particular = "Deposit",
            PaymentMeans = means
        };

        var result = _validator.TestValidate(model);
        result.ShouldNotHaveValidationErrorFor(x => x.PaymentMeans);
    }

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldPassValidation()
    {
        var model = new RecordPaymentRequestDto
        {
            TransactionId = 101,
            AmountPaid = 25000.50m,
            Particular = "50% Downpayment for wedding",
            PaymentMeans = PaymentMeans.GCash
        };

        var result = _validator.TestValidate(model);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
