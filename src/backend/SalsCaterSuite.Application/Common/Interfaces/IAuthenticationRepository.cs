namespace SalsCaterSuite.Application.Common.Interfaces;

using SalsCaterSuite.Application.Common.Models;

/// <summary>
/// Credential verification against the identity store (DEC-037).
/// <para>
/// This is the only sanctioned way for an Application use case — or the WebApi composing
/// layer — to reach Identity data. The contract is expressed in neutral Application types:
/// <c>ApplicationUser</c>, <c>UserManager</c>, <c>SignInManager</c>, <c>IdentityResult</c>
/// and every EF Core type stay inside the Infrastructure implementation.
/// </para>
/// </summary>
public interface IAuthenticationRepository
{
    /// <summary>
    /// Resolves <paramref name="userName"/> and verifies <paramref name="password"/>.
    /// <para>
    /// Behaviour contract, preserved verbatim from the login endpoint this replaces:
    /// an unknown user name or an inactive account returns
    /// <see cref="AuthenticationStatus.UserUnavailable"/> before the password is examined;
    /// a failed password check returns <see cref="AuthenticationStatus.InvalidPassword"/>.
    /// Failed attempts are counted and can lock the account out
    /// (<c>lockoutOnFailure: true</c> is applied by the implementation).
    /// </para>
    /// </summary>
    Task<AuthenticationAttempt> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}
