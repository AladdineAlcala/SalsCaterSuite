using FluentValidation;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

namespace SalsCaterSuite.Application.Features.Menus.Validators;

/// <summary>
/// The validation rules of the margin preview (spec §6.2). A negative price or a negative food
/// cost is rejected with the standard <c>{ "errors": [ ... ] }</c> contract; a zero price is
/// accepted and yields zero percentages, exactly as the costing service does.
/// </summary>
public class MenuCostingPreviewValidator : AbstractValidator<MenuCostingPreviewRequest>
{
    public MenuCostingPreviewValidator()
    {
        RuleFor(x => x.SellingPrice)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Selling price must be zero or greater.");

        RuleFor(x => x.FoodCost)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Food cost must be zero or greater.");
    }
}
