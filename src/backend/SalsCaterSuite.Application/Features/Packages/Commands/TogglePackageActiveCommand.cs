using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Packages.Commands;

/// <summary>PATCH /api/packages/{id}/toggle-active — flip the active flag.</summary>
public class TogglePackageActiveCommand
{
    private readonly IPackageRepository _packages;

    public TogglePackageActiveCommand(IPackageRepository packages)
    {
        _packages = packages;
    }

    public async Task<Result<bool>> ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await _packages.GetByIdAsync(id, cancellationToken);
        if (package is null)
        {
            return Result<bool>.Failure("Package not found.");
        }

        package.IsActive = !package.IsActive;
        await _packages.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(package.IsActive);
    }
}
