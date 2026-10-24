namespace SalsCaterSuite.Application.Features.Packages.Services;

/// <summary>
/// The <b>one</b> package-code generator (the owner's 2026-10-24 request).
/// <para>
/// The format is <c>PKG-{TYPE}-{NN}</c>: the type abbreviation, then the per-type sequence
/// number padded to at least two digits (<c>01</c>, <c>02</c>, … <c>99</c>, <c>100</c>). The
/// six known tier types have a fixed abbreviation; any other free-text type (DEC-070 allows
/// them) uses its first three alphanumeric characters, upper-cased, padded with <c>X</c> when
/// it is shorter.
/// </para>
/// <para>
/// The code is generated <b>once</b>, when the tier is created, and is <b>frozen</b>: editing
/// the type or the description never renumbers a tier, because a code is a business identifier
/// that documents and operators may already have quoted.
/// </para>
/// </summary>
public static class PackageCodeGenerator
{
    /// <summary>The fixed abbreviation of each known <c>PackageType</c> member.</summary>
    private static readonly IReadOnlyDictionary<string, string> KnownAbbreviations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Regular"] = "REG",
            ["Wedding"] = "WED",
            ["Vip"] = "VIP",
            ["Premier"] = "PRE",
            ["Pm"] = "PM",
            ["Sd"] = "SD"
        };

    /// <summary>
    /// The type abbreviation. A blank type is <c>GEN</c>; an unknown type uses its first three
    /// alphanumeric characters (upper-cased), padded with <c>X</c> when it is shorter.
    /// </summary>
    public static string Abbreviation(string? packageType)
    {
        var trimmed = packageType?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "GEN";
        }

        if (KnownAbbreviations.TryGetValue(trimmed, out var known))
        {
            return known;
        }

        var letters = new string(trimmed.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        if (letters.Length == 0)
        {
            return "GEN";
        }

        return letters.Length >= 3 ? letters[..3] : letters.PadRight(3, 'X');
    }

    /// <summary>The code the given sequence number produces, e.g. <c>PKG-REG-01</c>.</summary>
    public static string Build(string abbreviation, int sequence) =>
        $"PKG-{abbreviation}-{sequence:D2}";

    /// <summary>
    /// The next per-type sequence number: the highest suffix already used by a code of the same
    /// prefix, plus one. Codes of other types are ignored.
    /// <para>
    /// The caller passes <b>every</b> code the catalogue holds, including the soft-deleted ones,
    /// so a number is never reused. The unique index on the live rows is the database-level
    /// guard; the small race two concurrent creates can still lose is the same documented risk
    /// the receipt-number generator carries (RSK-013).
    /// </para>
    /// </summary>
    public static int NextSequence(IEnumerable<string> existingCodes, string abbreviation)
    {
        var prefix = $"PKG-{abbreviation}-";
        var highest = 0;

        foreach (var code in existingCodes)
        {
            if (string.IsNullOrWhiteSpace(code)
                || !code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = code[prefix.Length..];

            if (int.TryParse(suffix, out var sequence) && sequence > highest)
            {
                highest = sequence;
            }
        }

        return highest + 1;
    }
}
