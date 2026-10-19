namespace SalsCaterSuite.Application.Common.Authorization;

/// <summary>
/// Canonical role names (DEC-017). PROPOSED pending owner confirmation — kept in
/// one place so the final names can be changed without touching controllers.
/// </summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Accountant = "Accountant";
}

/// <summary>
/// Canonical authorization policy names and their role requirements (DEC-017).
/// PROPOSED pending owner confirmation.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Cancel a booking: Administrator or Manager.</summary>
    public const string BookingsCancel = "Bookings.Cancel";

    /// <summary>Record a payment: Administrator, Manager or Cashier.</summary>
    public const string PaymentsRecord = "Payments.Record";

    public static readonly string[] BookingsCancelRoles =
    {
        Roles.Administrator,
        Roles.Manager
    };

    public static readonly string[] PaymentsRecordRoles =
    {
        Roles.Administrator,
        Roles.Manager,
        Roles.Cashier
    };
}
