using FluentValidation;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Addons.Validators;

/// <summary>
/// The shape validation of the add-on writes (the owner's 2026-10-24 Settings → Add-ons request).
/// The length limits are the <b>columns</b>: <c>addonDescription</c> is <c>nvarchar(150)</c> and
/// <c>unit</c> is <c>nvarchar(50)</c>, so a longer payload is rejected here rather than reaching
/// the database as an unhandled 500. The category and department are checked by the command
/// against the live masters.
/// </summary>
public class AddonUpsertValidator : AbstractValidator<AddonUpsertRequest>
{
    public AddonUpsertValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Add-on description is required.")
            .MaximumLength(150)
            .WithMessage("Add-on description must not exceed 150 characters.");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .WithMessage("Unit must not exceed 50 characters.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Unit price must be zero or greater.");

        RuleFor(x => x.PricingBasis)
            .IsInEnum()
            .When(x => x.PricingBasis.HasValue)
            .WithMessage("The pricing basis is not valid.");

        RuleFor(x => x.AddonCategoryId)
            .GreaterThan(0)
            .When(x => x.AddonCategoryId.HasValue)
            .WithMessage("The add-on category must be a positive identifier.");

        RuleFor(x => x.DeptId)
            .GreaterThan(0)
            .When(x => x.DeptId.HasValue)
            .WithMessage("The department must be a positive identifier.");
    }
}
