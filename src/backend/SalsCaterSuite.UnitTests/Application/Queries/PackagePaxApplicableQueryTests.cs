namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The applicable-pax classification lookup (REQ-PKG-04, DEC-173).
/// <para>
/// The five rows are fixed reference data seeded by the schema migration. The query publishes
/// them verbatim and in identifier order, so the client can render the selector straight from
/// the response without re-sorting it.
/// </para>
/// </summary>
public class PackagePaxApplicableQueryTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly GetPackagePaxApplicablesQuery _query;

    public PackagePaxApplicableQueryTests()
    {
        _store.SeedPackagePaxApplicables();
        _query = new GetPackagePaxApplicablesQuery(new FakePackagePaxApplicableRepository(_store));
    }

    [Fact]
    public async Task The_query_should_return_the_five_fixed_options_in_identifier_order()
    {
        var result = await _query.ExecuteAsync();

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(5);
        result.Data!.Select(option => option.NumPaxId)
            .Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task The_query_should_publish_the_labels_verbatim()
    {
        // The skipped ranges and the "80 to 100 Above" wording are intentional (DEC-173). They
        // are stored and published exactly as the owner supplied them.
        var result = await _query.ExecuteAsync();

        result.Data!.Select(option => option.PackageApplicablePax)
            .Should().ContainInOrder(
                "15 to 19 Pax",
                "20 to 29 Pax",
                "30 to 49 Pax",
                "80 to 100 Above",
                "Not Applicable to PackMeals");
    }

    [Fact]
    public async Task The_query_should_not_parse_the_label_as_a_numeric_interval()
    {
        // "15 to 19 Pax" is a label, not a range. The published value is the label itself.
        var result = await _query.ExecuteAsync();

        var first = result.Data!.Single(option => option.NumPaxId == 1);

        first.PackageApplicablePax.Should().Be("15 to 19 Pax");
        // No parsed lower/upper bound is published: the classification is descriptive only.
        first.GetType().GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(new[] { "NumPaxId", "PackageApplicablePax" });
    }

    [Fact]
    public async Task The_query_should_exclude_a_soft_deleted_option()
    {
        // A removed classification is no longer a selectable option.
        _store.PackagePaxApplicables.Single(option => option.NumPaxId == 3).IsDeleted = true;

        var result = await _query.ExecuteAsync();

        result.Data.Should().HaveCount(4);
        result.Data!.Should().NotContain(option => option.NumPaxId == 3);
    }

    [Fact]
    public async Task The_query_should_return_an_empty_list_when_the_lookup_is_empty()
    {
        // The lookup ships with the five seeded rows, so an empty result means the reference data
        // is missing. It is reported honestly — nothing is fabricated (BR-13).
        _store.PackagePaxApplicables.Clear();

        var result = await _query.ExecuteAsync();

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task The_repository_should_report_only_a_live_option_as_existing()
    {
        var repository = new FakePackagePaxApplicableRepository(_store);

        (await repository.ExistsAsync(4)).Should().BeTrue();
        (await repository.ExistsAsync(999)).Should().BeFalse();

        _store.PackagePaxApplicables.Single(option => option.NumPaxId == 4).IsDeleted = true;

        (await repository.ExistsAsync(4)).Should().BeFalse();
    }
}
