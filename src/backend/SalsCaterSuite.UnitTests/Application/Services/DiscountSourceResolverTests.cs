namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Enums;
using Xunit;

/// <summary>
/// The derived discount source and the selection state (BR-44, DEC-154, BR-28).
/// <para>
/// Both are <b>derived</b>, never stored: the source from the frozen code, headcount and discount
/// amount; the state from the window and the live selection.
/// </para>
/// </summary>
public class DiscountSourceResolverTests
{
    [Fact]
    public void A_discount_code_should_be_the_source_even_when_a_band_also_qualifies()
    {
        DiscountSourceResolver.Resolve("EARLYBIRD", 120, 5_000m).Should().Be(DiscountSource.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_blank_code_should_not_be_read_as_a_code(string? code)
    {
        DiscountSourceResolver.Resolve(code, 120, 2_400m).Should().Be(DiscountSource.HeadcountBand);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(89)]
    [InlineData(100)]
    [InlineData(999)]
    public void A_qualifying_band_that_took_money_should_be_the_headcount_band(int heads)
    {
        DiscountSourceResolver.Resolve(null, heads, 1_000m).Should().Be(DiscountSource.HeadcountBand);
    }

    /// <summary>
    /// A band qualifies the <b>headcount</b> but took no money — the booking's net was zero, or
    /// it pre-dates the rule. That is <c>None</c>, never a phantom band (BR-44, DEC-154).
    /// </summary>
    [Fact]
    public void A_qualifying_band_that_took_nothing_should_be_none()
    {
        DiscountSourceResolver.Resolve(null, 120, 0m).Should().Be(DiscountSource.None);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(79)]
    [InlineData(90)]
    [InlineData(99)]
    [InlineData(1_000)]
    public void A_headcount_outside_the_bands_should_be_none(int heads)
    {
        DiscountSourceResolver.Resolve(null, heads, 500m).Should().Be(DiscountSource.None);
    }

    [Fact]
    public void A_locked_window_should_publish_locked_whatever_the_selection_is()
    {
        DiscountSourceResolver.ResolveSelectionState(isLocked: true, hasLiveSelection: true)
            .Should().Be(BookingSelectionState.Locked);
        DiscountSourceResolver.ResolveSelectionState(isLocked: true, hasLiveSelection: false)
            .Should().Be(BookingSelectionState.Locked);
    }

    [Fact]
    public void An_unlocked_window_should_publish_selected_or_none()
    {
        DiscountSourceResolver.ResolveSelectionState(isLocked: false, hasLiveSelection: true)
            .Should().Be(BookingSelectionState.Selected);
        DiscountSourceResolver.ResolveSelectionState(isLocked: false, hasLiveSelection: false)
            .Should().Be(BookingSelectionState.None);
    }
}
