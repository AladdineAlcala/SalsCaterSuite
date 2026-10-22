namespace SalsCaterSuite.UnitTests.Common.Fakes;

using SalsCaterSuite.Application.Common.Interfaces;

/// <summary>
/// A hand-written <see cref="IDateTime"/> so the dashboard's period resolution and its
/// "today" anchor are deterministic in the unit tests (no EF Core involved — DEC-037).
/// </summary>
public sealed class FakeDateTime : IDateTime
{
    public FakeDateTime(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime Now => UtcNow.ToLocalTime();

    public DateTime UtcNow { get; }
}
