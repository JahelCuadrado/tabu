using FluentAssertions;
using Tabu.Application.Services;
using Xunit;

namespace Tabu.Application.Tests.Services;

public sealed class BackdropPolicyTests
{
    private const int Win10_1809 = 17763;
    private const int Win10_22H2 = 19045;
    private const int Win11_22H2 = 22621;
    private const int Win11_24H2 = 26100;

    [Theory]
    [InlineData(null, BackdropMode.Acrylic)]
    [InlineData("", BackdropMode.Acrylic)]
    [InlineData("   ", BackdropMode.Acrylic)]
    [InlineData("acrylic", BackdropMode.Acrylic)]
    [InlineData("ACRYLIC", BackdropMode.Acrylic)]
    [InlineData(" Acrylic ", BackdropMode.Acrylic)]
    [InlineData("gaussian", BackdropMode.GaussianBlur)]
    [InlineData("GaussianBlur", BackdropMode.GaussianBlur)]
    [InlineData("blur", BackdropMode.GaussianBlur)]
    [InlineData("disabled", BackdropMode.Disabled)]
    [InlineData("off", BackdropMode.Disabled)]
    [InlineData("none", BackdropMode.Disabled)]
    [InlineData("garbage", BackdropMode.Acrylic)]
    public void ParseMode_NormalisesAllSupportedAliases(string? input, BackdropMode expected)
    {
        BackdropPolicy.ParseMode(input).Should().Be(expected);
    }

    [Fact]
    public void Resolve_ReturnsDisabled_WhenMasterToggleIsOff()
    {
        BackdropPolicy.Resolve(userEnabled: false, requestedMode: "Acrylic", osBuildNumber: Win11_24H2)
            .Should().Be(BackdropMode.Disabled);
    }

    [Fact]
    public void Resolve_ReturnsAcrylic_OnSupportedBuildWithDefaultPreference()
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: null, osBuildNumber: Win10_1809)
            .Should().Be(BackdropMode.Acrylic);
    }

    [Fact]
    public void Resolve_ReturnsGaussianBlur_WhenUserExplicitlyPicksFallback()
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: "Gaussian", osBuildNumber: Win11_22H2)
            .Should().Be(BackdropMode.GaussianBlur);
    }

    [Fact]
    public void Resolve_ReturnsDisabled_OnPre1809Build_EvenWithAcrylicRequested()
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: "Acrylic", osBuildNumber: 17134)
            .Should().Be(BackdropMode.Disabled);
    }

    [Fact]
    public void Resolve_ReturnsDisabled_WhenUserExplicitlyPicksDisabled()
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: "Disabled", osBuildNumber: Win11_24H2)
            .Should().Be(BackdropMode.Disabled);
    }

    [Theory]
    [InlineData(Win10_1809)]
    [InlineData(Win10_22H2)]
    [InlineData(Win11_22H2)]
    [InlineData(Win11_24H2)]
    public void Resolve_AcceptsAcrylic_AcrossAllSupportedBuilds(int build)
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: "Acrylic", osBuildNumber: build)
            .Should().Be(BackdropMode.Acrylic);
    }

    [Theory]
    [InlineData(Win10_1809)]
    [InlineData(Win10_22H2)]
    [InlineData(Win11_22H2)]
    [InlineData(Win11_24H2)]
    public void Resolve_AcceptsGaussianBlur_AcrossAllSupportedBuilds(int build)
    {
        BackdropPolicy.Resolve(userEnabled: true, requestedMode: "Gaussian", osBuildNumber: build)
            .Should().Be(BackdropMode.GaussianBlur);
    }
}
