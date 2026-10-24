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
/// </summary>
public class CreatePackageCommand
{
    private readonly IPackageRepository _packages;
    private readonly IValidator<CreatePackageRequestDto> _validator;

    public CreatePackageCommand(IPackageRepository packages, IValidator<CreatePackageRequestDto> validator)
    {
        _packages = packages;
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

        var package = new Package
        {
            PackageType = request.PackageType,
            Description = request.Description,
            AmountPax = request.AmountPax,
            MinPax = request.MinPax,
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
