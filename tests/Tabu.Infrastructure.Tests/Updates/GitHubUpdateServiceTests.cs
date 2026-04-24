using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Tabu.Domain.Entities;
using Tabu.Infrastructure.Updates;
using Xunit;

namespace Tabu.Infrastructure.Tests.Updates;

/// <summary>
/// Unit tests for the GitHub-backed update resolver. Network I/O is
/// stubbed via a custom <see cref="HttpMessageHandler"/> so tests are
/// deterministic and offline.
/// </summary>
public sealed class GitHubUpdateServiceTests
{
    private const string SampleAssetName = "TabuSetup-v1.4.0-win-x64.exe";
    private const string SampleAssetUrl =
        "https://github.com/JahelCuadrado/Tabu/releases/download/v1.4.0/" + SampleAssetName;

    [Fact]
    public async Task CheckForUpdate_ReturnsUpdateInfo_WhenLatestReleaseIsNewerAndHasInstaller()
    {
        var json = BuildReleaseJson(tag: "v1.4.0", assets:
        [
            (SampleAssetName, SampleAssetUrl, 12345)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.3.2"));

        result.Should().NotBeNull();
        result!.Version.Should().Be(new Version("1.4.0"));
        result.Tag.Should().Be("v1.4.0");
        result.InstallerFileName.Should().Be(SampleAssetName);
        result.InstallerDownloadUrl.Should().Be(SampleAssetUrl);
        result.InstallerSizeBytes.Should().Be(12345);
    }

    [Fact]
    public async Task CheckForUpdate_StripsLeadingV_WhenParsingTag()
    {
        var json = BuildReleaseJson(tag: "V2.0.0", assets:
        [
            (SampleAssetName, SampleAssetUrl, 1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().NotBeNull();
        result!.Version.Should().Be(new Version("2.0.0"));
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenLatestReleaseIsSameVersion()
    {
        var json = BuildReleaseJson(tag: "v1.3.2", assets:
        [
            (SampleAssetName, SampleAssetUrl, 1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.3.2"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenLatestReleaseIsOlderVersion()
    {
        var json = BuildReleaseJson(tag: "v1.0.0", assets:
        [
            (SampleAssetName, SampleAssetUrl, 1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.3.2"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenTagCannotBeParsedAsVersion()
    {
        var json = BuildReleaseJson(tag: "nightly-build", assets:
        [
            (SampleAssetName, SampleAssetUrl, 1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenNoAssetMatchesInstallerPattern()
    {
        var json = BuildReleaseJson(tag: "v2.0.0", assets:
        [
            ("Tabu-portable.zip",
             "https://github.com/JahelCuadrado/Tabu/releases/download/v2.0.0/Tabu-portable.zip",
             1),
            ("checksums.txt",
             "https://github.com/JahelCuadrado/Tabu/releases/download/v2.0.0/checksums.txt",
             1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().BeNull("no asset matches the *setup*.exe pattern");
    }

    [Fact]
    public async Task CheckForUpdate_PicksFirstSetupExeAsset_WhenMultipleAssetsArePresent()
    {
        // Asset URLs MUST live on the GitHub-trusted host whitelist
        // (github.com / objects.githubusercontent.com) — the audit
        // hardening rejects anything else as a defence against a tampered
        // release JSON pointing the auto-updater at an attacker host.
        var json = BuildReleaseJson(tag: "v2.0.0", assets:
        [
            ("Tabu-v2.0.0-portable.zip", "https://github.com/JahelCuadrado/Tabu/releases/download/v2.0.0/Tabu-v2.0.0-portable.zip", 1),
            ("TabuSetup-v2.0.0-win-x64.exe", "https://github.com/JahelCuadrado/Tabu/releases/download/v2.0.0/TabuSetup-v2.0.0-win-x64.exe", 99),
            ("checksums.txt", "https://github.com/JahelCuadrado/Tabu/releases/download/v2.0.0/checksums.txt", 1)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().NotBeNull();
        result!.InstallerFileName.Should().Be("TabuSetup-v2.0.0-win-x64.exe");
        result.InstallerSizeBytes.Should().Be(99);
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_OnHttpFailure()
    {
        var sut = SutWith(throwException: new HttpRequestException("offline"));

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_OnTimeoutOrCancellation()
    {
        var sut = SutWith(throwException: new TaskCanceledException("deadline"));

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdate_Throws_WhenCurrentVersionIsNull()
    {
        var sut = SutWith(HttpStatusCode.OK, "{}");

        await FluentActions.Invoking(() => sut.CheckForUpdateAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void InstallerAssetPattern_MatchesCanonicalSetupName_AndIsCaseInsensitive()
    {
        GitHubUpdateService.InstallerAssetPattern.IsMatch("TabuSetup-v1.4.0-win-x64.exe").Should().BeTrue();
        GitHubUpdateService.InstallerAssetPattern.IsMatch("tabusetup.exe").Should().BeTrue();
        GitHubUpdateService.InstallerAssetPattern.IsMatch("setup-anything.exe").Should().BeTrue();
        GitHubUpdateService.InstallerAssetPattern.IsMatch("Tabu-portable.zip").Should().BeFalse();
        GitHubUpdateService.InstallerAssetPattern.IsMatch("Tabu.exe").Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // Security hardening (audit v1.6): host whitelist + integrity manifest
    // -------------------------------------------------------------------

    [Theory]
    [InlineData("https://github.com/JahelCuadrado/Tabu/releases/download/v1.0.0/x.exe", true)]
    [InlineData("https://objects.githubusercontent.com/asset/abc", true)]
    [InlineData("https://release-assets.githubusercontent.com/asset/abc", true)]
    [InlineData("http://github.com/JahelCuadrado/Tabu/releases/x.exe", false)]
    [InlineData("https://evil.example.com/setup.exe", false)]
    [InlineData("ftp://github.com/x.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrustedDownloadUrl_OnlyAcceptsHttpsOnGitHubAssetHosts(string? url, bool expected)
    {
        GitHubUpdateService.IsTrustedDownloadUrl(url).Should().Be(expected);
    }

    [Fact]
    public async Task CheckForUpdate_ReturnsNull_WhenInstallerUrlIsNotOnHostWhitelist()
    {
        var json = BuildReleaseJson(tag: "v2.0.0", assets:
        [
            ("TabuSetup-v2.0.0-win-x64.exe", "https://evil.example.com/setup.exe", 99)
        ]);
        var sut = SutWith(HttpStatusCode.OK, json);

        var result = await sut.CheckForUpdateAsync(new Version("1.0.0"));

        result.Should().BeNull("an installer hosted off the GitHub asset CDN must never reach the orchestrator");
    }

    [Fact]
    public async Task DownloadInstallerAsync_Throws_WhenUrlIsNotTrusted()
    {
        var sut = SutWith(HttpStatusCode.OK, "");
        var update = new UpdateInfo
        {
            Version = new Version("2.0.0"),
            Tag = "v2.0.0",
            InstallerDownloadUrl = "https://evil.example.com/setup.exe",
            InstallerFileName = "setup.exe"
        };
        var dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");

        await FluentActions.Invoking(() => sut.DownloadInstallerAsync(update, dest))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*trusted GitHub asset host whitelist*");
    }

    [Fact]
    public void ExtractDigest_ReturnsLowercaseHash_ForMatchingFileName()
    {
        const string hash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
        var manifest =
            $"{hash} *TabuSetup-v2.0.0-win-x64.exe\n" +
            "0000000000000000000000000000000000000000000000000000000000000000  other.zip\n";

        var result = GitHubUpdateService.ExtractDigest(manifest, "TabuSetup-v2.0.0-win-x64.exe");

        result.Should().Be(hash.ToLowerInvariant());
    }

    [Fact]
    public void ExtractDigest_ReturnsNull_WhenFileNotListed()
    {
        var manifest = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789  other.exe\n";

        GitHubUpdateService.ExtractDigest(manifest, "TabuSetup.exe").Should().BeNull();
    }

    [Fact]
    public void ExtractDigest_ReturnsNull_OnEmptyOrNullInput()
    {
        GitHubUpdateService.ExtractDigest("", "x.exe").Should().BeNull();
        GitHubUpdateService.ExtractDigest("anything", "").Should().BeNull();
    }

    private static GitHubUpdateService SutWith(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        return new GitHubUpdateService(new HttpClient(handler));
    }

    private static GitHubUpdateService SutWith(Exception throwException)
    {
        var handler = new StubHandler(_ => throw throwException);
        return new GitHubUpdateService(new HttpClient(handler));
    }

    private static string BuildReleaseJson(string tag, IReadOnlyList<(string name, string url, long size)> assets)
    {
        var assetEntries = assets.Select(a =>
            $$"""{ "name": "{{a.name}}", "size": {{a.size}}, "browser_download_url": "{{a.url}}" }""");
        return $$"""
        {
            "tag_name": "{{tag}}",
            "name": "Test release {{tag}}",
            "html_url": "https://github.com/test/release/{{tag}}",
            "assets": [{{string.Join(",", assetEntries)}}]
        }
        """;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
