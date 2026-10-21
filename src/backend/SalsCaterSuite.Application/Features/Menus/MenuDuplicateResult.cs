using SalsCaterSuite.Application.Common.Models;

namespace SalsCaterSuite.Application.Features.Menus;

/// <summary>
/// A menu failure that must be surfaced as HTTP 409 Conflict (contract §7).
/// <para>
/// It exists so the WebApi layer can distinguish "the dish code is already taken" from "the
/// payload was rejected" without inspecting the error message text — a distinction the POST
/// endpoint needs now that it validates its input (spec §6.4, §7.1).
/// </para>
/// </summary>
public sealed class MenuDuplicateResult : Result<string>
{
    public MenuDuplicateResult(string error)
        : base(false, null, new[] { error })
    {
    }
}
