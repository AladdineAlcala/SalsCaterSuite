namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using Xunit;

/// <summary>
/// The out-of-town surcharge is limited to two tier <b>types</b> (BR-40, DEC-141, DEC-156).
/// <para>
/// The test is <b>pinned</b>: the tier's <c>PackageType</c> is trimmed and compared
/// case-insensitively for equality with <c>Regular</c> or <c>Premier</c> <b>verbatim</b>. The
/// description is never consulted — the development catalogue's own tier is typed <c>Regular</c>
/// while its description reads "2026 Premier Package B".
/// </para>
/// </summary>
public class PackageSurchargeEligibilityTests
{
    [Theory]
    [InlineData("Regular")]
    [InlineData("Premier")]
    [InlineData("  Regular  ")]
    [InlineData("PREMIER")]
    [InlineData("regular")]
    public void The_two_eligible_types_should_be_eligible(string packageType)
    {
        PackageSurchargeEligibility.IsEligible(packageType).Should().BeTrue();
    }

    [Theory]
    [InlineData("Premier Buffet")]
    [InlineData("Regular Buffet")]
    [InlineData("Bespoke")]
    [InlineData("Premiere")]
    [InlineData("Regular Premier")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Any_other_type_should_not_be_eligible(string? packageType)
    {
        PackageSurchargeEligibility.IsEligible(packageType).Should().BeFalse();
    }

    /// <summary>
    /// The <c>400</c> names both eligible types, so the operator is not left guessing, and it
    /// names the type the tier actually carries.
    /// </summary>
    [Fact]
    public void The_rejection_message_should_name_both_eligible_types_and_the_selected_one()
    {
        var message = PackageSurchargeEligibility.RejectionMessage("Premier Buffet");

        message.Should().Contain("Regular");
        message.Should().Contain("Premier");
        message.Should().Contain("'Premier Buffet'");
    }

    [Fact]
    public void The_rejection_message_should_say_when_no_type_is_stated()
    {
        PackageSurchargeEligibility.RejectionMessage(null).Should().Contain("not stated");
    }
}
