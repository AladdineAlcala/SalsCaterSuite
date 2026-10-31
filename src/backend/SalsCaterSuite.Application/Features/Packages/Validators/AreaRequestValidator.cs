using FluentValidation;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of one municipality (spec §3.6, BR-11, DEC-077).
/// <para>
/// The limits are the <b>column</b> limits of the <c>Area</c> table: <c>areaDetails</c> is
/// <c>nvarchar(150)</c> and the new <c>logisticsNote</c> is <c>nvarchar(150)</c> as well.
/// </para>
/// <para>
/// A null rate is "no rate has been stated" and a null logistics cost omits the logistics band
/// (DEC-086, DEC-081) — neither is an error. A negative rate, distance or logistics cost is,
/// because a negative money figure has no meaning.
/// </para>
/// </summary>
public class AreaRequestValidator : AbstractValidator<AreaRequest>
{
    public AreaRequestValidator()
    {
        RuleFor(x => x.AreaDetails)
            .NotEmpty()
            .WithMessage("Area name is required.")
            .MaximumLength(150)
            .WithMessage("Area name must not exceed 150 characters.");

        RuleFor(x => x.AdditionalRate)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Additional rate must be zero or greater.");

        RuleFor(x => x.DistanceKm)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Distance must be zero or greater.");

        RuleFor(x => x.LogisticsNote)
            .MaximumLength(150)
            .WithMessage("Logistics note must not exceed 150 characters.");

        RuleFor(x => x.LogisticsCost)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Logistics cost must be zero or greater.");
    }
}
