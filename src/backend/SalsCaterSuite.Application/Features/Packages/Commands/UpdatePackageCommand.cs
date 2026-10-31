using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Bookings.Services;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>
/// PUT /api/packages/{id} — update a package.
/// <para>
/// The command runs the same <see cref="CreatePackageRequest"/> validator the create command
/// runs (BR-11, spec §3.3, DEC-069). Before this correction the update applied no validation
/// at all, so a blank description or a negative rate reached the database.
/// </para>
/// <para>
/// A missing tier is reported as a <see cref="PackageNotFoundResult"/> and a rejected payload
/// as an ordinary failure, so the API distinguishes the two <b>by result type</b> — a 404 and a
/// 400 — and never by the message text.
/// </para>
/// <para>
/// The applicable-pax classification is validated in two halves (REQ-PKG-04, DEC-173): the
/// <b>syntactic</b> half in the validator and the <b>referential</b> half here, because the
/// synchronous validator performs no I/O.
/// </para>
/// </summary>
public class UpdatePackageCommand
{
    private readonly IPackageRepository _packages;
    private readonly IPackagePaxApplicableRepository _paxApplicables;
    private readonly IValidator<CreatePackageRequest> _validator;

    public UpdatePackageCommand(
        IPackageRepository packages,
        IPackagePaxApplicableRepository paxApplicables,
        IValidator<CreatePackageRequest> validator)
    {
        _packages = packages;
        _paxApplicables = paxApplicables;
        _validator = validator;
    }

    public async Task<Result> ExecuteAsync(int id, CreatePackageRequest request, CancellationToken cancellationToken = default)
    {
        // The validation runs first, exactly as it does in CreatePackageCommand, so an invalid
        // payload is rejected before anything is written.
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // The referential half of the applicable-pax validation (DEC-173). It runs before the
        // write, so an unknown or removed classification changes nothing.
        if (!await _paxApplicables.ExistsAsync(request.NumPaxId, cancellationToken))
        {
            return Result.Failure("Applicable pax classification was not found.");
        }

        var package = await _packages.GetByIdAsync(id, cancellationToken);
        if (package is null)
        {
            return new PackageNotFoundResult("Package not found.");
        }

        package.PackageType = request.PackageType;
        package.Description = request.Description;
        package.AmountPax = request.AmountPax;
        package.MinPax = request.MinPax;
        package.NumPaxId = request.NumPaxId;

        // The code is frozen at create and is never touched here. The extended-location amount
        // follows the type: a tier that leaves Regular/Premier loses it (the owner's ruling).
        package.ExtendedLocationAmount = PackageSurchargeEligibility.IsEligible(request.PackageType)
            ? request.ExtendedLocationAmount
            : null;

        await _packages.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
