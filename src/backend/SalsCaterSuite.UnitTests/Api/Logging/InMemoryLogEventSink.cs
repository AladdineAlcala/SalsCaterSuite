namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// A hand-written test double that captures the Serilog events a test logger produced, so the
/// operational record's fields can be asserted without writing to disk (DEC-037 rule 6: the
/// Application-layer tests never touch EF Core, and neither do these).
/// </summary>
internal sealed class InMemoryLogEventSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}

/// <summary>Reads a Serilog event's named properties as plain CLR values.</summary>
internal static class LogEventPropertyExtensions
{
    /// <summary>Whether the event carries the named property.</summary>
    public static bool Has(this LogEvent logEvent, string propertyName) =>
        logEvent.Properties.ContainsKey(propertyName);

    /// <summary>The named property's value, unwrapped from its Serilog representation.</summary>
    public static object? Value(this LogEvent logEvent, string propertyName) =>
        logEvent.Properties.TryGetValue(propertyName, out var value) ? Unwrap(value) : null;

    /// <summary>The named property as a string.</summary>
    public static string? Text(this LogEvent logEvent, string propertyName) =>
        logEvent.Value(propertyName) as string;

    /// <summary>The named property as a boolean.</summary>
    public static bool? Flag(this LogEvent logEvent, string propertyName) =>
        logEvent.Value(propertyName) as bool?;

    /// <summary>The named property as a 64-bit integer.</summary>
    public static long? Number(this LogEvent logEvent, string propertyName) =>
        logEvent.Value(propertyName) as long?;

    /// <summary>The named property as a double.</summary>
    public static double? Measure(this LogEvent logEvent, string propertyName) =>
        logEvent.Value(propertyName) as double?;

    /// <summary>The named property as a string array.</summary>
    public static string[]? Texts(this LogEvent logEvent, string propertyName) =>
        logEvent.Value(propertyName) switch
        {
            string[] values => values,
            object[] values => values.OfType<string>().ToArray(),
            _ => null
        };

    private static object? Unwrap(LogEventPropertyValue value) => value switch
    {
        ScalarValue scalar => scalar.Value,
        SequenceValue sequence => sequence.Elements.Select(Unwrap).ToArray(),
        _ => value
    };
}
