using System.Windows.Media;
using FluentAssertions;
using Tabu.UI.Services;
using Xunit;

namespace Tabu.UI.Tests.Services;

public sealed class AccentColorManagerTests
{
    [Theory]
    [InlineData("blue", "#3B82F6")]
    [InlineData("BLUE", "#3B82F6")]
    [InlineData("red", "#EF4444")]
    [InlineData("purple", "#6A42A0")]
    [InlineData("Cyan", "#06B6D4")]
    public void ResolveCanonicalHex_legacy_preset_codes_map_to_hex(string input, string expected)
    {
        AccentColorManager.ResolveCanonicalHex(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("#FF6B35", "#FF6B35")]
    [InlineData("#ff6b35", "#FF6B35")]
    [InlineData("FF6B35", "#FF6B35")]
    [InlineData("  #112233  ", "#112233")]
    public void ResolveCanonicalHex_accepts_full_hex_in_any_casing_or_padding(string input, string expected)
    {
        AccentColorManager.ResolveCanonicalHex(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData("#ABC")]            // shorthand explicitly rejected
    [InlineData("#GGGGGG")]         // non-hex digits
    [InlineData("#12345")]          // wrong length
    [InlineData("'; DROP TABLE--")] // injection-like garbage
    public void ResolveCanonicalHex_falls_back_to_default_for_invalid_input(string? input)
    {
        AccentColorManager.ResolveCanonicalHex(input).Should().Be(AccentColorManager.DefaultAccentCode);
    }

    [Fact]
    public void LightenInHsl_pure_black_yields_grey_not_white()
    {
        var result = AccentColorManager.LightenInHsl(Colors.Black, 0.08);

        // +8% lightness on black ≈ #141414, far from white but visibly lighter.
        result.R.Should().BeInRange(15, 25);
        result.R.Should().Be(result.G).And.Be(result.B);
    }

    [Fact]
    public void LightenInHsl_clamps_at_pure_white()
    {
        var result = AccentColorManager.LightenInHsl(Colors.White, 0.5);

        result.Should().Be(Colors.White);
    }

    [Fact]
    public void LightenInHsl_preserves_hue_for_saturated_blue()
    {
        // #3B82F6 (default accent) lightened should remain visibly blue, not purple/cyan.
        var input = Color.FromRgb(0x3B, 0x82, 0xF6);
        var result = AccentColorManager.LightenInHsl(input, 0.08);

        result.B.Should().BeGreaterThan(result.R);
        result.B.Should().BeGreaterThan(result.G);
        // Lightening must not darken any channel.
        result.R.Should().BeGreaterOrEqualTo(input.R);
        result.G.Should().BeGreaterOrEqualTo(input.G);
        result.B.Should().BeGreaterOrEqualTo(input.B);
    }

    [Fact]
    public void DefaultAccentCode_is_a_valid_canonical_hex()
    {
        AccentColorManager.DefaultAccentCode.Should().MatchRegex("^#[0-9A-F]{6}$");
        AccentColorManager.ResolveCanonicalHex(AccentColorManager.DefaultAccentCode)
            .Should().Be(AccentColorManager.DefaultAccentCode);
    }
}
