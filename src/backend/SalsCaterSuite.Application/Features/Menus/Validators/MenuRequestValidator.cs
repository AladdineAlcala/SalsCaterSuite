using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Validators;

/// <summary>
/// The validation rules of the menu write commands (spec §6.4, BR-11, AC-M20).
/// <para>
/// Before this correction neither <c>CreateMenuCommand</c> nor <c>UpdateMenuCommand</c> ran a
/// validator, so a blank dish name, a negative price, an over-length name or an unknown course id
/// reached the database and surfaced as a 500 instead of a rejected request.
/// </para>
/// <para>
/// The rules are the <b>stricter</b> side of the schema: <c>Menu.MenuId</c> is
/// <c>nvarchar(50)</c>, <c>Menu.MenuName</c> is <c>nvarchar(150)</c> and <c>Menu.Note</c> is
/// <c>nvarchar(500)</c>, and the validator refuses anything longer. The course id must exist in
/// <c>CourseCategory</c>, which is the dish's foreign key.
/// </para>
/// <para>
/// The same validator serves the create and the update command — they accept the same body
/// (spec §6.4) — so the two can never drift apart.
/// </para>
/// </summary>
public class MenuRequestValidator : AbstractValidator<CreateMenuRequestDto>
{
    private readonly IMenuRepository _menus;

    public MenuRequestValidator(IMenuRepository menus)
    {
        _menus = menus;

        RuleFor(x => x.MenuId)
            .NotEmpty()
            .WithMessage("Menu id is required.")
            .MaximumLength(50)
            .WithMessage("Menu id must not exceed 50 characters.");

        RuleFor(x => x.CourseId)
            .GreaterThan(0)
            .WithMessage("A course category is required.")
            .MustAsync(async (courseId, cancellationToken) =>
                await _menus.CourseExistsAsync(courseId, cancellationToken))
            .WithMessage("The selected course category does not exist.");

        RuleFor(x => x.MenuName)
            .NotEmpty()
            .WithMessage("Menu name is required.")
            .MaximumLength(150)
            .WithMessage("Menu name must not exceed 150 characters.");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithMessage("Note must not exceed 500 characters.");

        // The plating photo is a URL string; the column is nvarchar(250) (spec §5.2, §7.7).
        RuleFor(x => x.Image)
            .MaximumLength(250)
            .WithMessage("Image must not exceed 250 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Price must be zero or greater.");

        RuleFor(x => x.FoodCost)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Food cost must be zero or greater.");
    }
}
