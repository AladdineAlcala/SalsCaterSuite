namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Customers.DTOs.Request;
using SalsCaterSuite.Application.Features.Customers.DTOs.Response;
using SalsCaterSuite.Application.Features.Customers.Validators;
using Xunit;

/// <summary>
/// The customer payload validator (BR-11, spec §7.3).
/// <para>
/// The name columns carry the maximum lengths of the database schema
/// (<c>nvarchar(100)</c> / <c>nvarchar(100)</c> / <c>nvarchar(50)</c>). Before this
/// correction an over-length name reached SQL Server and produced a truncation error (a
/// 500) instead of a rejected request.
/// </para>
/// </summary>
public class CreateCustomerValidatorTests
{
    private readonly CreateCustomerValidator _validator = new();

    private static CreateCustomerRequest ValidRequest() =>
        new("Santos", "Maria", "Cruz", "12 Rizal St, Manila", "09171234567", null, null);

    [Fact]
    public void A_valid_payload_should_pass()
    {
        var result = _validator.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void A_blank_last_name_should_be_rejected()
    {
        var result = _validator.Validate(ValidRequest() with { LastName = "  " });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Last name is required.");
    }

    [Fact]
    public void A_blank_first_name_should_be_rejected()
    {
        var result = _validator.Validate(ValidRequest() with { FirstName = "  " });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "First name is required.");
    }

    // ── the maximum-length rules the schema demands (B8) ─────────────────────────────

    [Fact]
    public void An_over_length_last_name_should_be_rejected()
    {
        // 200 characters — the column is nvarchar(100).
        var result = _validator.Validate(ValidRequest() with { LastName = new string('x', 200) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Last name must not exceed 100 characters.");
    }

    [Fact]
    public void A_last_name_of_exactly_the_maximum_length_should_pass()
    {
        var result = _validator.Validate(ValidRequest() with { LastName = new string('x', 100) });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_over_length_first_name_should_be_rejected()
    {
        var result = _validator.Validate(ValidRequest() with { FirstName = new string('x', 200) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "First name must not exceed 100 characters.");
    }

    [Fact]
    public void An_over_length_middle_name_should_be_rejected()
    {
        // 100 characters — the column is nvarchar(50).
        var result = _validator.Validate(ValidRequest() with { Middle = new string('x', 100) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Middle name must not exceed 50 characters.");
    }

    [Fact]
    public void A_middle_name_of_exactly_the_maximum_length_should_pass()
    {
        var result = _validator.Validate(ValidRequest() with { Middle = new string('x', 50) });

        result.IsValid.Should().BeTrue();
    }

    // ── the rules that already existed are unchanged (spec §7.3) ─────────────────────

    [Fact]
    public void The_existing_maximum_lengths_should_be_unchanged()
    {
        _validator.Validate(ValidRequest() with { Address = new string('x', 201) })
            .Errors.Should().Contain(e => e.ErrorMessage == "Address must not exceed 200 characters.");

        _validator.Validate(ValidRequest() with { Contact1 = new string('x', 16) })
            .Errors.Should().Contain(e => e.ErrorMessage == "Contact number 1 must not exceed 15 characters.");

        _validator.Validate(ValidRequest() with { Contact2 = new string('x', 16) })
            .Errors.Should().Contain(e => e.ErrorMessage == "Contact number 2 must not exceed 15 characters.");

        _validator.Validate(ValidRequest() with { Company = new string('x', 151) })
            .Errors.Should().Contain(e => e.ErrorMessage == "Company name must not exceed 150 characters.");
    }

    /// <summary>
    /// spec §8 / DEC-054: the "at least one contact method" invariant is explicitly NOT
    /// enforced — it would reject existing records and has not been ratified.
    /// </summary>
    [Fact]
    public void A_customer_without_any_contact_method_should_still_be_accepted()
    {
        var result = _validator.Validate(
            ValidRequest() with { Contact1 = null, Contact2 = null });

        result.IsValid.Should().BeTrue();
    }
}
