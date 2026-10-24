using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Addons;

/// <summary>
/// An add-on failure that must be surfaced as HTTP 404 Not Found (contract §7), so the API
/// distinguishes a missing add-on from a rejected payload by <b>result type</b>, never by the
/// message text.
/// </summary>
public sealed class AddonNotFoundResult : Result
{
    public AddonNotFoundResult(string error)
        : base(false, new[] { error })
    {
    }
}

/// <summary>
/// The generic form of <see cref="AddonNotFoundResult"/> — a missing add-on that also carries a
/// payload, used by the detail read.
/// </summary>
public sealed class AddonNotFoundResult<T> : Result<T>
{
    public AddonNotFoundResult(string error)
        : base(false, default, new[] { error })
    {
    }
}
