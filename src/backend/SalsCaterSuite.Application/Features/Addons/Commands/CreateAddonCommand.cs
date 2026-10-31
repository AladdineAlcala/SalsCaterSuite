using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Addons.DTOs.Request;
using SalsCaterSuite.Application.Features.Addons.DTOs.Response;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Addons.Commands;

/// <summary>
/// <c>POST /api/addons</c> — create an add-on (the owner's 2026-10-24 Settings → Add-ons
/// request). The payload is validated, its optional category and department are checked against
/// the live masters, and nothing is persisted on a rejection.
/// </summary>
public class CreateAddonCommand
{
    private readonly IAddonRepository _addons;
    private readonly IDepartmentRepository _departments;
    private readonly IValidator<AddonUpsertRequest> _validator;

    public CreateAddonCommand(
        IAddonRepository addons,
        IDepartmentRepository departments,
        IValidator<AddonUpsertRequest> validator)
    {
        _addons = addons;
        _departments = departments;
        _validator = validator;
    }

    public async Task<Result<int>> ExecuteAsync(
        AddonUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<int>.Failure(validation.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var referenceError = await AddonReferenceGuard.CheckAsync(_addons, _departments, request, cancellationToken);
        if (referenceError is not null)
        {
            return Result<int>.Failure(referenceError);
        }

        var addon = new AddonDetail
        {
            AddonCategoryId = request.AddonCategoryId,
            DeptId = request.DeptId,
            PricingBasis = request.PricingBasis,
            Description = request.Description.Trim(),
            UnitPrice = request.UnitPrice,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim(),
            IsActive = request.IsActive
        };

        await _addons.AddAsync(addon, cancellationToken);
        await _addons.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(addon.AddonId);
    }
}
