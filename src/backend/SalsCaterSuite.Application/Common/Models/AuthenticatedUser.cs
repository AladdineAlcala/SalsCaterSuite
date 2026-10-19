namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// The neutral, identity-provider-agnostic view of a successfully authenticated user
/// (DEC-037). It carries exactly what the login response and the JWT claims need, so no
/// ASP.NET Core Identity type (<c>ApplicationUser</c>, <c>UserManager</c>,
/// <c>SignInManager</c>) and no EF Core type crosses the Application boundary.
/// </summary>
/// <param name="UserId">Identity key of the account (<c>NameIdentifier</c> claim).</param>
/// <param name="UserName">Login name exactly as stored, returned by <c>POST /api/auth/login</c>.</param>
/// <param name="FullName">Display name in the account's own "Lastname, Firstname[ M.]" format.</param>
/// <param name="IsActive">Account activation flag at the moment of authentication.</param>
/// <param name="Roles">Canonical role names (DEC-017) granted to the account.</param>
public sealed record AuthenticatedUser(
    string UserId,
    string UserName,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles);
