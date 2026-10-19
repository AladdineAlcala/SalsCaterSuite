using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalsCaterSuite.WebApi.Serialization;

/// <summary>
/// Central System.Text.Json configuration for the API.
/// </summary>
/// <remarks>
/// Approved JSON convention (architecture/approved-api-contracts.md §8, DEC-011): enum values are
/// exchanged as <b>string names</b> (for example <c>"status": "DepositPaid"</c>,
/// <c>"bookType": "Inside"</c>) rather than numbers. <see cref="JsonStringEnumConverter"/> also
/// still accepts numeric enum values on input, preserving backward compatibility with clients that
/// were written against the previous default behaviour.
/// </remarks>
public static class ApiJsonSerializerOptions
{
    /// <summary>
    /// Applies the Sal's CaterSuite JSON conventions to the supplied <paramref name="options"/>.
    /// Called once for the MVC pipeline in <c>Program.cs</c>; the unit tests call the same method so
    /// they exercise the exact configuration the API uses.
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new JsonStringEnumConverter());
    }
}
