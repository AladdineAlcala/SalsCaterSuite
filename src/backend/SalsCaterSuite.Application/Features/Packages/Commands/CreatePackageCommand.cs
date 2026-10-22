using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Packages.DTOs;
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
            IsActive = true
        };

        await _packages.AddAsync(package, cancellationToken);
        await _packages.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(package.PackageId);
    }
}
