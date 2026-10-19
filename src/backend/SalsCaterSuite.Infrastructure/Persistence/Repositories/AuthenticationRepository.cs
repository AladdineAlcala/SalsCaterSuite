namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.AspNetCore.Identity;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Infrastructure.Identity;

/// <summary>
/// Identity-backed implementation of <see cref="IAuthenticationRepository"/> (DEC-037).
/// <para>
/// This is where ASP.NET Core Identity's user store is reached for credential lookup, which
/// is precisely why it lives in <c>Infrastructure.Persistence.Repositories</c>: the WebApi
/// layer and every Application use case resolve the neutral contract instead of injecting
/// <see cref="UserManager{TUser}"/> or <see cref="SignInManager{TUser}"/> directly.
/// </para>
/// <para>
/// Verification semantics are unchanged from the login endpoint this replaces:
/// <list type="number">
/// <item>resolve by user name; unknown or inactive ⇒ <see cref="AuthenticationStatus.UserUnavailable"/>
/// (the password is never examined);</item>
/// <item>verify the password with <c>lockoutOnFailure: true</c>, so a failed attempt is
/// counted, can lock the account, and is reported as
/// <see cref="AuthenticationStatus.InvalidPassword"/>;</item>
/// <item>on success, return the account's identity fields and roles.</item>
/// </list>
/// </para>
/// </summary>
public class AuthenticationRepository : IAuthenticationRepository
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthenticationRepository(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <inheritdoc />
    public async Task<AuthenticationAttempt> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Identity's UserManager/SignInManager APIs expose no CancellationToken overload.
        // The parameter is kept so callers can propagate cancellation uniformly; every
        // awaited call below is a short, already-scoped database round trip.
        _ = cancellationToken;

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null || !user.IsActive)
        {
            return AuthenticationAttempt.ForUnavailableUser();
        }

        // lockoutOnFailure: true — identical to the previous controller behaviour. A failed
        // attempt increments the access-failed count and can lock the account; the caller
        // only ever learns that the credentials were rejected.
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return AuthenticationAttempt.ForInvalidPassword();
        }

        var roles = await _userManager.GetRolesAsync(user);

        return AuthenticationAttempt.ForAuthenticatedUser(new AuthenticatedUser(
            UserId: user.Id,
            UserName: user.UserName ?? string.Empty,
            FullName: user.FullName,
            IsActive: user.IsActive,
            Roles: roles.ToArray()));
    }
}
