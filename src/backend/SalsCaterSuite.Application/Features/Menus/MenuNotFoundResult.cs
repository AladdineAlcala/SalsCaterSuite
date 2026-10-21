using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Menus;

/// <summary>
/// A menu failure that must be surfaced as HTTP 404 Not Found (contract §7).
/// <para>
/// It exists so the WebApi layer can distinguish "the dish does not exist" from "the payload was
/// rejected" without inspecting the error message text — a distinction the PUT endpoint needs
/// now that it validates its input (spec §6.4, §7.1).
/// </para>
/// </summary>
public sealed class MenuNotFoundResult : Result
{
    public MenuNotFoundResult(string error)
        : base(false, new[] { error })
    {
    }
}
