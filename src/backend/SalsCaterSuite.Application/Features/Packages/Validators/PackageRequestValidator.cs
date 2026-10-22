using FluentValidation;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of the package write commands (spec §3.3, BR-11, DEC-069).
/// <para>
/// Before this correction neither <c>CreatePackageCommand</c> nor <c>UpdatePackageCommand</c> ran
/// a validator, so a blank tier type, a blank description, a negative per-guest rate and a
/// minimum headcount below one all reached the database.
/// </para>
/// <para>
/// The tier type is deliberately <b>not</b> restricted to the <c>PackageType</c> enum members
/// (DEC-070): the column is free text and the legacy import may carry other values, so the
/// validator refuses only an empty or an over-length value.
/// </para>
/// <para>
/// The length limits are the <b>column</b> limits: <c>PackageConfiguration</c> declares
/// <c>p_type</c> as <c>nvarchar(50)</c> and <c>p_descripton</c> as <c>nvarchar(250)</c>. A limit
/// wider than the column would accept a payload the database then rejects as an unhandled
/// <c>500</c> - the defect BR-11 exists to close (DEC-075).
/// </para>
/// <para>
/// The same validator serves the create and the update command — they accept the same body —
/// so the two can never drift apart.
/// </para>
/// </summary>
public class PackageRequestValidator : AbstractValidator<CreatePackageRequestDto>
{
    public PackageRequestValidator()
    {
        RuleFor(x => x.PackageType)
            .NotEmpty()
            .WithMessage("Package type is required.")
            .MaximumLength(50)
            .WithMessage("Package type must not exceed 50 characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Package description is required.")
            .MaximumLength(250)
            .WithMessage("Package description must not exceed 250 characters.");

        // A null rate means "not priced" and a null floor means "not set" — neither is an error.
        RuleFor(x => x.AmountPax)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Rate per guest must be zero or greater.");

        RuleFor(x => x.MinPax)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Minimum pax must be at least 1.");
    }
}
