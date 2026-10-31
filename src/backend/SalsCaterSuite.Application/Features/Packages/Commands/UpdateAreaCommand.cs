using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// PUT /api/areas/{id} — update a municipality (contract §13.11, REQ-PKG-02).
/// <para>
/// The payload is validated before anything is persisted (BR-11). A missing municipality is
/// reported as an <see cref="AreaNotFoundResult"/> and a rejected payload as an ordinary
/// failure, so the API distinguishes the two <b>by result type</b> — a <c>404</c> and a
/// <c>400</c> — and never by the message text.
/// </para>
/// <para>
/// A municipality's rate is <b>one</b> rate per municipality, not one per package (DEC-078):
/// editing it affects every tier that covers the municipality.
/// </para>
/// </summary>
public class UpdateAreaCommand
{
    private readonly IAreaRepository _areas;
    private readonly IValidator<AreaRequest> _validator;

    public UpdateAreaCommand(IAreaRepository areas, IValidator<AreaRequest> validator)
    {
        _areas = areas;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(
        int areaId,
        AreaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Result.Failure(
                validationResult.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var area = await _areas.GetLiveByIdAsync(areaId, cancellationToken);

        if (area is null)
        {
            return new AreaNotFoundResult("Area not found.");
        }

        area.AreaDetails = request.AreaDetails;
        area.AdditionalRate = request.AdditionalRate;
        area.DistanceKm = request.DistanceKm;
        area.LogisticsNote = request.LogisticsNote;
        area.LogisticsCost = request.LogisticsCost;

        await _areas.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
