using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// POST /api/areas — add a municipality to the catalogue (contract §13.10, REQ-PKG-02).
/// <para>
/// The payload is validated before anything is persisted (BR-11). A rejected payload returns the
/// validator's messages, which the API renders as <c>400 { "errors": [ ... ] }</c>, and nothing
/// is written.
/// </para>
/// <para>
/// No municipality is seeded (BR-13) — the catalogue starts empty and the operator fills it.
/// </para>
/// </summary>
public class CreateAreaCommand
{
    private readonly IAreaRepository _areas;
    private readonly IValidator<AreaRequest> _validator;

    public CreateAreaCommand(IAreaRepository areas, IValidator<AreaRequest> validator)
    {
        _areas = areas;
        _validator = validator;
    }

    public async Task<Result<int>> ExecuteAsync(
        AreaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result<int>.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var area = new Area
        {
            AreaDetails = request.AreaDetails,
            AdditionalRate = request.AdditionalRate,
            DistanceKm = request.DistanceKm,
            LogisticsNote = request.LogisticsNote,
            LogisticsCost = request.LogisticsCost
        };

        await _areas.AddAsync(area, cancellationToken);
        await _areas.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(area.AreaId);
    }
}
