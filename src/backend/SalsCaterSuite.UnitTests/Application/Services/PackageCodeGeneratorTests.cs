namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Services;
using Xunit;

/// <summary>
/// The package-code generator (owner request 2026-10-24).
/// <para>
/// The code is <c>PKG-{TYPE}-{NN}</c>, generated once at create and frozen. The six known types
/// have a fixed abbreviation; an unknown free-text type (DEC-070 allows one) uses its first three
/// alphanumeric characters, upper-cased and padded with <c>X</c>.
/// </para>
/// </summary>
public class PackageCodeGeneratorTests
{
    [Theory]
    [InlineData("Regular", "REG")]
    [InlineData("Wedding", "WED")]
    [InlineData("Vip", "VIP")]
    [InlineData("Premier", "PRE")]
    [InlineData("Pm", "PM")]
    [InlineData("Sd", "SD")]
    [InlineData("  Regular  ", "REG")]
    [InlineData("regular", "REG")]
    public void Known_types_use_the_fixed_abbreviation(string packageType, string expected)
    {
        PackageCodeGenerator.Abbreviation(packageType).Should().Be(expected);
    }

    [Theory]
    [InlineData("Buffet Premium", "BUF")]
    [InlineData("Corporate", "COR")]
    [InlineData("A", "AXX")]
    [InlineData("AB", "ABX")]
    [InlineData("", "GEN")]
    [InlineData("   ", "GEN")]
    public void Unknown_types_use_the_first_three_alphanumeric_characters(string packageType, string expected)
    {
        PackageCodeGenerator.Abbreviation(packageType).Should().Be(expected);
    }

    [Fact]
    public void The_code_pads_the_sequence_to_two_digits()
    {
        PackageCodeGenerator.Build("REG", 1).Should().Be("PKG-REG-01");
        PackageCodeGenerator.Build("REG", 9).Should().Be("PKG-REG-09");
        PackageCodeGenerator.Build("REG", 10).Should().Be("PKG-REG-10");
        PackageCodeGenerator.Build("REG", 99).Should().Be("PKG-REG-99");
    }

    [Fact]
    public void The_code_widens_beyond_99_instead_of_truncating()
    {
        PackageCodeGenerator.Build("REG", 100).Should().Be("PKG-REG-100");
        PackageCodeGenerator.Build("REG", 1000).Should().Be("PKG-REG-1000");
    }

    [Fact]
    public void The_next_sequence_counts_only_the_same_type_and_never_reuses_a_number()
    {
        var existing = new[]
        {
            "PKG-REG-01",
            "PKG-REG-04",
            "PKG-WED-07",
            "PKG-REG-03",
            "PKG-REG-100",
        };

        PackageCodeGenerator.NextSequence(existing, "REG").Should().Be(101);
        PackageCodeGenerator.NextSequence(existing, "WED").Should().Be(8);
        PackageCodeGenerator.NextSequence(existing, "PRE").Should().Be(1);
    }
}
