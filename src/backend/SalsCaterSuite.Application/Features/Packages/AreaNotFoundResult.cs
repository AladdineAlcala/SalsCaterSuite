using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Packages;

/// <summary>
/// A municipality failure that must be surfaced as HTTP 404 Not Found (contract §13.11).
/// <para>
/// It exists so the WebApi layer can distinguish "the municipality does not exist" from "the
/// payload was rejected" without inspecting the error message text — the same distinction the
/// package workbench makes for a tier (DEC-069, DEC-074).
/// </para>
/// </summary>
public sealed class AreaNotFoundResult : Result
{
    public AreaNotFoundResult(string error)
        : base(false, new[] { error })
    {
    }
}

/// <summary>
/// The generic form of <see cref="AreaNotFoundResult"/> — a missing municipality that also
/// carries a payload (REQ-PKG-02, contract §13.7).
/// </summary>
public sealed class AreaNotFoundResult<T> : Result<T>
{
    public AreaNotFoundResult(string error)
        : base(false, default, new[] { error })
    {
    }
}
