namespace SalsCaterSuite.Application.Common.Models;

/// <summary>
/// Outcome of a credential verification (DEC-037). The three states exist because the
/// approved login contract distinguishes them: a missing or inactive account and a wrong
/// password produce different messages.
/// </summary>
public enum AuthenticationStatus
{
    /// <summary>The credentials verified; <see cref="AuthenticationAttempt.User"/> is populated.</summary>
    Succeeded,

    /// <summary>
    /// No account carries the supplied user name, or the account exists but is inactive.
    /// — evaluated <em>before</em> the password, exactly as the login endpoint always did.
    /// </summary>
    UserUnavailable,

    /// <summary>
    /// The password did not verify. Also covers a locked-out account, because the check
    /// runs with <c>lockoutOnFailure: true</c> and reports a plain failure.
    /// </summary>
    InvalidPassword
}

/// <summary>
/// Result of <c>IAuthenticationRepository.AuthenticateAsync</c>. Carries no exception and
/// no provider-specific error detail: an authentication failure is an expected outcome,
/// not a fault.
/// </summary>
public sealed record AuthenticationAttempt(AuthenticationStatus Status, AuthenticatedUser? User)
{
    public static AuthenticationAttempt ForAuthenticatedUser(AuthenticatedUser user) =>
        new(AuthenticationStatus.Succeeded, user);

    public static AuthenticationAttempt ForUnavailableUser() =>
        new(AuthenticationStatus.UserUnavailable, null);

    public static AuthenticationAttempt ForInvalidPassword() =>
        new(AuthenticationStatus.InvalidPassword, null);
}
