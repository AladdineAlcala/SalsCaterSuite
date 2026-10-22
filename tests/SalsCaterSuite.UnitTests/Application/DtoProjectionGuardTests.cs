namespace SalsCaterSuite.UnitTests.Application;

using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using Xunit;

/// <summary>
/// The <b>DTO projection guard</b> — a new test convention for this repository.
/// <para>
/// It protects against the defect class AGENT 01's live end-to-end validation found: a DTO
/// member that <b>no projection ever assigns</b>. The endpoint then answers a wrong value —
/// silently — for a booking that carries the right one, and no test notices because nothing
/// asserts the field.
/// </para>
/// <para>
/// The check is <b>source-level</b>: every public settable property of a read DTO must appear
/// as an assignment target inside the object initializer of the projection that populates it.
/// It therefore fails the moment a member is added to a DTO and not projected.
/// </para>
/// <para>
/// It covers the read DTOs of the Bookings &amp; Events feature — the booking details, the
/// register item and the workbench quote — and their three projections.
/// </para>
/// </summary>
public class DtoProjectionGuardTests
{
    private const string ApplicationRoot = "src/backend/SalsCaterSuite.Application/Features/Bookings";

    private const string DetailsProjection = ApplicationRoot + "/Queries/GetBookingDetailsQuery.cs";
    private const string RegisterProjection = ApplicationRoot + "/Queries/GetBookingRegisterQuery.cs";
    private const string QuoteProjection = ApplicationRoot + "/Commands/PreviewBookingQuoteCommand.cs";

    [Fact]
    public void Every_booking_details_member_should_be_projected_by_the_details_query()
    {
        AssertEveryMemberIsProjected(typeof(BookingDto), DetailsProjection);
    }

    [Fact]
    public void Every_register_item_member_should_be_projected_by_the_register_query()
    {
        AssertEveryMemberIsProjected(typeof(BookingRegisterItemDto), RegisterProjection);
    }

    [Fact]
    public void Every_quote_member_should_be_projected_by_the_quote_command()
    {
        AssertEveryMemberIsProjected(typeof(BookingQuoteDto), QuoteProjection);
    }

    /// <summary>
    /// The guard proves it would catch the defect: a member that the initializer does not
    /// assign is reported.
    /// </summary>
    [Fact]
    public void The_guard_should_report_a_member_that_the_projection_does_not_assign()
    {
        var initializer = "{ Assigned = 1, }";

        var unprojected = FindUnprojectedMembers(typeof(SyntheticDto), initializer);

        unprojected.Should().ContainSingle().Which.Should().Be(nameof(SyntheticDto.NotAssigned));
    }

    // ─────────────────────────── the guard ───────────────────────────

    private static void AssertEveryMemberIsProjected(Type dtoType, string relativeSourcePath)
    {
        var source = ReadProjectionSource(relativeSourcePath);
        var initializer = ExtractObjectInitializer(source, dtoType.Name);

        initializer.Should().NotBeNullOrEmpty(
            $"{relativeSourcePath} must construct a {dtoType.Name} — the guard cannot check it otherwise");

        var unprojected = FindUnprojectedMembers(dtoType, initializer!);

        unprojected.Should().BeEmpty(
            $"every public property of {dtoType.Name} must be assigned by {relativeSourcePath}: " +
            $"'{string.Join("', '", unprojected)}' is declared but never projected. " +
            "A declared-but-never-populated field is a silent wrong answer.");
    }

    /// <summary>
    /// The public settable properties of the DTO that the initializer does <b>not</b> assign.
    /// <para>
    /// The match requires the property name to be followed by <c>=</c> (and not <c>==</c>) and to
    /// be preceded by neither an identifier character nor a dot — so a <i>read</i> such as
    /// <c>booking.Status</c> is never mistaken for an assignment, and a comparison is never
    /// mistaken for one either.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> FindUnprojectedMembers(Type dtoType, string initializer) =>
        dtoType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite && property.SetMethod is { IsPublic: true })
            .Where(property => !Regex.IsMatch(
                initializer,
                $@"(?<![\w.]){Regex.Escape(property.Name)}\s*=(?!=)"))
            .Select(property => property.Name)
            .ToList();

    /// <summary>
    /// The object initializer of the DTO inside the projection source: the balanced-brace block
    /// that follows the <c>new {TypeName}</c>.
    /// </summary>
    private static string? ExtractObjectInitializer(string source, string typeName)
    {
        var marker = $"new {typeName}";
        var index = source.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            return null;
        }

        var brace = source.IndexOf('{', index);

        if (brace < 0)
        {
            return null;
        }

        var depth = 0;

        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring(brace, i - brace + 1);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The projection's source file. The repository root comes from the <c>RepositoryRoot</c>
    /// assembly metadata, because the test assembly may be built outside the source tree (the
    /// development machine redirects the output directory).
    /// </summary>
    private static string ReadProjectionSource(string relativeSourcePath)
    {
        var root = typeof(DtoProjectionGuardTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepositoryRoot")?.Value;

        root.Should().NotBeNullOrWhiteSpace(
            "the 'RepositoryRoot' assembly metadata is required by the DTO projection guard");

        var path = Path.Combine(
            Path.GetFullPath(root!),
            relativeSourcePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(path).Should().BeTrue($"the projection source '{relativeSourcePath}' must exist");

        return File.ReadAllText(path);
    }

    /// <summary>A stand-in DTO, used to prove the guard reports an unassigned member.</summary>
    private sealed class SyntheticDto
    {
        public int Assigned { get; set; }

        public int NotAssigned { get; set; }
    }
}
