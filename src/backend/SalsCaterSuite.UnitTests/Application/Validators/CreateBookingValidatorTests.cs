namespace SalsCaterSuite.UnitTests.Application.Validators;

using FluentAssertions;
using FluentValidation.TestHelper;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Bookings.Validators;
using Xunit;

public class CreateBookingValidatorTests
{
    private readonly CreateBookingValidator _validator;

    public CreateBookingValidatorTests()
    {
        _validator = new CreateBookingValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenCustomerIdIsZeroOrNegative_ShouldHaveValidationError(int customerId)
    {
        var model = new CreateBookingRequest { CustomerId = customerId };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.CustomerId)
              .WithErrorMessage("A valid customer is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WhenNoOfPersonIsZeroOrNegative_ShouldHaveValidationError(int persons)
    {
        var model = new CreateBookingRequest { NoOfPerson = persons };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.NoOfPerson)
              .WithErrorMessage("Number of persons must be greater than 0.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenOccasionIsNullOrEmpty_ShouldHaveValidationError(string? occasion)
    {
        var model = new CreateBookingRequest { Occasion = occasion! };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Occasion);
    }

    [Fact]
    public void Validate_WhenOccasionExceeds150Chars_ShouldHaveValidationError()
    {
        var model = new CreateBookingRequest { Occasion = new string('A', 151) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Occasion)
              .WithErrorMessage("Occasion is required and must not exceed 150 characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenVenueIsNullOrEmpty_ShouldHaveValidationError(string? venue)
    {
        var model = new CreateBookingRequest { Venue = venue! };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Venue);
    }

    [Fact]
    public void Validate_WhenVenueExceeds200Chars_ShouldHaveValidationError()
    {
        var model = new CreateBookingRequest { Venue = new string('V', 201) };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Venue)
              .WithErrorMessage("Venue is required and must not exceed 200 characters.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_WhenPackageIdIsZeroOrNegative_ShouldHaveValidationError(int packageId)
    {
        var model = new CreateBookingRequest { PackageId = packageId };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.PackageId)
              .WithErrorMessage("When a package is specified it must be a valid package ID.");
    }

    [Fact]
    public void Validate_WhenStartDateIsEmpty_ShouldHaveValidationError()
    {
        var model = new CreateBookingRequest { StartDate = default };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.StartDate)
              .WithErrorMessage("Event start date and time is required.");
    }

    [Fact]
    public void Validate_WhenEndDateIsBeforeStartDate_ShouldHaveValidationError()
    {
        var start = DateTime.UtcNow.AddDays(10);
        var end = start.AddHours(-2);
        var model = new CreateBookingRequest
        {
            StartDate = start,
            EndDate = end
        };

        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.EndDate)
              .WithErrorMessage("Event end date must be on or after the start date.");
    }

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldPassValidation()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var model = new CreateBookingRequest
        {
            CustomerId = 1,
            NoOfPerson = 100,
            Occasion = "Grand Wedding Reception",
            Venue = "Grand Ballroom A",
            PackageId = 2,
            StartDate = start,
            EndDate = start.AddHours(5)
        };

        var result = _validator.TestValidate(model);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
