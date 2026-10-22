using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Packages;

/// <summary>
/// A package failure that must be surfaced as HTTP 404 Not Found (contract §7).
/// <para>
/// It exists so the WebApi layer can distinguish "the tier does not exist" from "the payload
/// was rejected" without inspecting the error message text — a distinction the update endpoint
/// needs now that it validates its input (spec §3.3, DEC-069).
/// </para>
/// </summary>
public sealed class PackageNotFoundResult : Result
{
    public PackageNotFoundResult(string error)
        : base(false, new[] { error })
    {
    }
}

/// <summary>
/// The generic form of <see cref="PackageNotFoundResult"/> — a missing tier that also carries a
/// payload (REQ-PKG-02, contract §13.2 .. §13.7).
/// <para>
/// The workbench's writes and previews answer <b>200</b> with a computed payload, so they must be
/// able to report "the tier does not exist" (<c>404</c>) and "the payload was rejected"
/// (<c>400</c>) <b>by result type</b>, exactly as the tier's own update command does — never by
/// the message text.
/// </para>
/// </summary>
public sealed class PackageNotFoundResult<T> : Result<T>
{
    public PackageNotFoundResult(string error)
        : base(false, default, new[] { error })
    {
    }
}
