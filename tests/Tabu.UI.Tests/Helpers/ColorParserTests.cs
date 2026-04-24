using System.Windows.Media;
using FluentAssertions;
using Tabu.UI.Helpers;
using Xunit;

namespace Tabu.UI.Tests.Helpers;

/// <summary>
/// Verifies the strict <c>#RRGGBB</c> contract enforced by
/// <see cref="ColorParser"/>. Settings persistence relies on this
/// whitelist to keep <c>settings.json</c> free of injected payloads,
/// so every accepted/rejected case is locked down here.
/// </summary>
public sealed class ColorParserTests
{
    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#00ff00")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#a1B2c3")]
    public void IsValidHex_AcceptsStrictSixDigitForm(string hex)
        => ColorParser.IsValidHex(hex).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FF0000")]      // missing #
    [InlineData("#FFF")]        // shorthand
    [InlineData("#FF00")]       // wrong length
    [InlineData("#FF00000")]    // wrong length
    [InlineData("#FF000G")]     // non-hex
    [InlineData("#FF0000FF")]   // alpha not allowed
    [InlineData("rgb(255,0,0)")]
    [InlineData("'; DROP TABLE settings; --")]
    public void IsValidHex_RejectsAnyOtherShape(string? input)
        => ColorParser.IsValidHex(input).Should().BeFalse();

    [Fact]
    public void TryParse_ReturnsTrueAndProducesExpectedColor()
    {
        ColorParser.TryParse("#1A2B3C", out var color).Should().BeTrue();
        color.Should().Be(Color.FromRgb(0x1A, 0x2B, 0x3C));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a color")]
    [InlineData("#ZZZZZZ")]
    public void TryParse_ReturnsFalseForMalformedInput(string? input)
    {
        ColorParser.TryParse(input, out var color).Should().BeFalse();
        color.Should().Be(default(Color));
    }

    [Fact]
    public void TryParse_AcceptsNamedColorsForBackwardsCompatibility()
    {
        // ColorConverter accepts "Red", "DodgerBlue" etc. We don't
        // enforce strict hex at TryParse level (only at IsValidHex),
        // so the auxiliary path stays compatible with WPF defaults.
        ColorParser.TryParse("Red", out var color).Should().BeTrue();
        color.Should().Be(Colors.Red);
    }
}
