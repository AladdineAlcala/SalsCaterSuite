using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>
/// PUT /api/menus/{id} — update a menu item.
/// <para>
/// The command runs the same <see cref="CreateMenuRequestDto"/> validator the create command
/// runs (BR-11, spec §6.4). Before this correction the update applied no validation at all, so
/// a blank or an over-length name reached the database and produced a truncation error (a 500)
/// instead of a rejected request.
/// </para>
/// <para>
/// The dish code of the body is validated but not applied: the route identifies the record and
/// the specification does not authorise a key change.
/// </para>
/// </summary>
public class UpdateMenuCommand
{
    private readonly IMenuRepository _menus;
    private readonly IValidator<CreateMenuRequestDto> _validator;

    public UpdateMenuCommand(IMenuRepository menus, IValidator<CreateMenuRequestDto> validator)
    {
        _menus = menus;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(string id, CreateMenuRequestDto request, CancellationToken cancellationToken = default)
    {
        // The validation runs first, exactly as it does in CreateMenuCommand, so an invalid
        // payload is rejected before anything is written.
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var menu = await _menus.GetByIdAsync(id, cancellationToken);
        if (menu is null)
        {
            return new MenuNotFoundResult("Menu item not found.");
        }

        menu.CourseId = request.CourseId;
        menu.MenuName = request.MenuName;
        menu.Note = request.Note;
        menu.Price = request.Price;
        menu.FoodCost = request.FoodCost;
        menu.Image = request.Image;

        await _menus.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
