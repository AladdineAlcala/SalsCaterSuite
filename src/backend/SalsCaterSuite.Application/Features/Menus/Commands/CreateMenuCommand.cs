using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Menus.Commands;

/// <summary>
/// POST /api/menus — create a menu item.
/// <para>
/// The payload is validated before anything is persisted (BR-11, spec §6.4). A rejected payload
/// returns the validation messages, which the API renders as <c>400 { "errors": [ ... ] }</c>; a
/// duplicate dish code returns <c>409 Conflict</c>.
/// </para>
/// </summary>
public class CreateMenuCommand
{
    private readonly IMenuRepository _menus;
    private readonly IValidator<CreateMenuRequestDto> _validator;

    public CreateMenuCommand(IMenuRepository menus, IValidator<CreateMenuRequestDto> validator)
    {
        _menus = menus;
        _validator = validator;
    }

    public async Task<Result<string>> ExecuteAsync(CreateMenuRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<string>.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // The dish code is upper-cased by the server (spec §5.2), so the uniqueness check runs
        // against the stored form — the database's key comparison is case-insensitive, the check
        // must be too.
        var menuId = request.MenuId.ToUpper();

        var exists = await _menus.ExistsAsync(menuId, cancellationToken);
        if (exists)
        {
            return new MenuDuplicateResult($"Menu item '{menuId}' already exists.");
        }

        var menu = new Menu
        {
            MenuId = menuId,
            CourseId = request.CourseId,
            MenuName = request.MenuName,
            Note = request.Note,
            Price = request.Price,
            FoodCost = request.FoodCost,
            Image = request.Image
        };

        await _menus.AddAsync(menu, cancellationToken);
        await _menus.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(menu.MenuId);
    }
}
