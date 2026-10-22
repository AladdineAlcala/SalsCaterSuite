namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// A hand-written <see cref="IBookingPolicyProvider"/> so the deployment-configured figures
/// are deterministic in the unit tests (DEC-101, DEC-102). Both are <c>null</c> by default —
/// the "not stated" state.
/// </summary>
public sealed class FakeBookingPolicyProvider : IBookingPolicyProvider
{
    public decimal? RequiredDepositPercentage { get; set; }

    public int? MaximumCapacityPax { get; set; }
}
