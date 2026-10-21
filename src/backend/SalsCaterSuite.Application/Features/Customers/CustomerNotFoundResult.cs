using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Customers;

/// <summary>
/// A customer failure that must be surfaced as HTTP 404 (contract §7).
/// <para>
/// It exists so the WebApi layer can distinguish "the customer does not exist" from "the
/// payload was rejected" without inspecting the error message text — a distinction the PUT
/// endpoint needs now that it validates its input (spec §7.3).
/// </para>
/// </summary>
public sealed class CustomerNotFoundResult : Result
{
    public CustomerNotFoundResult(string error)
        : base(false, new[] { error })
    {
    }
}
