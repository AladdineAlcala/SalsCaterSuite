using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;

namespace SalsCaterSuite.Application.Features.Addons.Commands;

/// <summary>
/// <c>PUT /api/addons/{id}</c> — update an add-on (the owner's 2026-10-24 Settings → Add-ons
/// request). The route identifies the record; a missing or soft-deleted add-on is a
/// <see cref="AddonNotFoundResult"/> (404) and a rejected payload is an ordinary failure (400).
/// </summary>
public class UpdateAddonCommand
{
    private readonly IAddonRepository _addons;
    private readonly IDepartmentRepository _departments;
    private readonly IValidator<AddonUpsertRequest> _validator;

    public UpdateAddonCommand(
        IAddonRepository addons,
        IDepartmentRepository departments,
        IValidator<AddonUpsertRequest> validator)
    {
        _addons = addons;
        _departments = departments;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(
        int addonId,
        AddonUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(validation.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var addon = await _addons.GetByIdAsync(addonId, cancellationToken);
        if (addon is null)
        {
            return new AddonNotFoundResult("Add-on not found.");
        }

        var referenceError = await AddonReferenceGuard.CheckAsync(_addons, _departments, request, cancellationToken);
        if (referenceError is not null)
        {
            return Result.Failure(referenceError);
        }

        addon.AddonCategoryId = request.AddonCategoryId;
        addon.DeptId = request.DeptId;
        addon.PricingBasis = request.PricingBasis;
        addon.Description = request.Description.Trim();
        addon.UnitPrice = request.UnitPrice;
        addon.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        addon.IsActive = request.IsActive;
        addon.UpdatedDate = DateTime.UtcNow;

        await _addons.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
