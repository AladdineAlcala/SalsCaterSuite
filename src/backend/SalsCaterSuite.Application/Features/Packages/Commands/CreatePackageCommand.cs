using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>POST /api/packages — create a package.</summary>
/// <para>
/// The payload is validated before anything is persisted (BR-11, spec §3.3, DEC-069). A
/// rejected payload returns the validation messages, which the API renders as
/// <c>400 { "errors": [ ... ] }</c>, and nothing is written.
/// </para>
/// <para>
/// The applicable-pax classification is validated in two halves (REQ-PKG-04, DEC-173). The
/// <b>syntactic</b> half runs in <see cref="PackageRequestValidator"/> — the identifier must
/// be present and positive. The <b>referential</b> half runs here: the identifier must
/// reference a live lookup row, which the synchronous validator cannot check without I/O.
/// </para>
/// </summary>
public class CreatePackageCommand
{
    private readonly IPackageRepository _packages;
    private readonly IPackagePaxApplicableRepository _paxApplicables;
    private readonly IValidator<CreatePackageRequestDto> _validator;

    public CreatePackageCommand(
        IPackageRepository packages,
        IPackagePaxApplicableRepository paxApplicables,
        IValidator<CreatePackageRequestDto> validator)
    {
        _packages = packages;
        _paxApplicables = paxApplicables;
        _validator = validator;
    }

    public async Task<Result<int>> ExecuteAsync(CreatePackageRequestDto request, CancellationToken cancellationToken = default)
    {
        // The validation runs first, so an invalid payload never reaches the database.
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<int>.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // The referential half of the applicable-pax validation (DEC-173). It runs before any
        // write, so an unknown or removed classification persists nothing.
        if (!await _paxApplicables.ExistsAsync(request.NumPaxId, cancellationToken))
        {
            return Result<int>.Failure("Applicable pax classification was not found.");
        }

        var package = new Package
        {
            PackageType = request.PackageType,
            Description = request.Description,
            AmountPax = request.AmountPax,
            MinPax = request.MinPax,
            NumPaxId = request.NumPaxId,
            ExtendedLocationAmount = request.ExtendedLocationAmount,
            IsActive = true
        };

        // The human-readable code is generated once, at create, and frozen (the owner's
        // 2026-10-24 request). The per-type sequence counts the codes already held, including
        // the soft-deleted ones, so a number is never reused.
        var abbreviation = PackageCodeGenerator.Abbreviation(request.PackageType);
        var existingCodes = await _packages.GetPackageCodesAsync(cancellationToken);
        package.PackageCode = PackageCodeGenerator.Build(
            abbreviation,
            PackageCodeGenerator.NextSequence(existingCodes, abbreviation));

        await _packages.AddAsync(package, cancellationToken);
        await _packages.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(package.PackageId);
    }
}
