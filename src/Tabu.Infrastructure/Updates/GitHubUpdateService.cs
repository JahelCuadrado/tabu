using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Tabu.Domain.Updates;

namespace Tabu.Infrastructure.Updates;

/// <summary>
/// Resolves Tabu releases through the public GitHub Releases API and streams
/// installer downloads to disk. The implementation is intentionally tiny: a
/// single shared <see cref="HttpClient"/> is used per process and no auth
/// token is required because the source repository is public.
/// </summary>
/// <remarks>
/// Security model (audit hardening, v1.6+):
/// <list type="bullet">
///   <item>Every download URL is whitelisted against the GitHub asset CDN
///   and forced to HTTPS, preventing a tampered release JSON from
///   redirecting the auto-updater to an attacker-controlled host.</item>
///   <item>When the release ships a <c>SHA256SUMS.txt</c> asset, the
///   installer's digest is captured into <see cref="UpdateInfo.ExpectedSha256"/>
///   and verified after streaming completes. A mismatch deletes the file
///   and throws so the orchestrator never executes a corrupted artifact.</item>
/// </list>
/// </remarks>
public sealed class GitHubUpdateService : IUpdateService
{
    private const string LatestReleaseEndpoint =
        "https://api.github.com/repos/JahelCuadrado/Tabu/releases/latest";

    /// <summary>
    /// Whitelist of hosts allowed to serve downloadable assets. GitHub
    /// resolves <c>browser_download_url</c> through a 302 redirect to the
    /// signed CDN, so both names must be accepted.
    /// </summary>
    private static readonly HashSet<string> AllowedDownloadHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com",
            "objects.githubusercontent.com",
            "release-assets.githubusercontent.com"
        };

    /// <summary>
    /// Matches the installer asset name (e.g. <c>TabuSetup-v1.3.2-win-x64.exe</c>).
    /// Exposed for tests to assert against the same canonical pattern.
    /// </summary>
    internal static readonly Regex InstallerAssetPattern =
        new(@"setup.*\.exe$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Matches a single line of the integrity manifest: <c>{hex} *{filename}</c>
    /// or <c>{hex}  {filename}</c> (the optional <c>*</c> marker is the
    /// classic <c>sha256sum -b</c> binary-mode prefix).
    /// </summary>
    private static readonly Regex ChecksumLinePattern =
        new(@"^(?<hash>[0-9A-Fa-f]{64})\s+\*?(?<file>.+?)\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

    private const string ChecksumAssetName = "SHA256SUMS.txt";
    private const int DownloadBufferSize = 81920;

    private static readonly HttpClient SharedHttpClient = CreateClient();

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Production constructor: uses a process-wide shared
    /// <see cref="HttpClient"/> to avoid socket exhaustion.
    /// </summary>
    public GitHubUpdateService()
        : this(SharedHttpClient) { }

    /// <summary>
    /// Test/advanced constructor: lets callers inject a pre-configured
    /// <see cref="HttpClient"/> backed by a custom message handler so
    /// the GitHub API can be stubbed out in unit tests.
    /// </summary>
    public GitHubUpdateService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        GitHubRelease? release;
        try
        {
            release = await _httpClient
                .GetFromJsonAsync<GitHubRelease>(LatestReleaseEndpoint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }

        if (release is null || string.IsNullOrWhiteSpace(release.TagName) || release.Assets is null)
        {
            return null;
        }

        if (!TryParseTag(release.TagName, out var latestVersion) || latestVersion <= currentVersion)
        {
            return null;
        }

        var installer = release.Assets
            .FirstOrDefault(a => !string.IsNullOrEmpty(a.Name)
                && InstallerAssetPattern.IsMatch(a.Name)
                && !string.IsNullOrEmpty(a.BrowserDownloadUrl));

        if (installer is null || !IsTrustedDownloadUrl(installer.BrowserDownloadUrl))
        {
            return null;
        }

        var checksumAsset = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, ChecksumAssetName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(a.BrowserDownloadUrl));

        string? expectedSha = null;
        if (checksumAsset is not null && IsTrustedDownloadUrl(checksumAsset.BrowserDownloadUrl))
        {
            expectedSha = await TryFetchChecksumAsync(
                checksumAsset.BrowserDownloadUrl!,
                installer.Name!,
                cancellationToken).ConfigureAwait(false);
        }

        return new UpdateInfo
        {
            Version = latestVersion,
            Tag = release.TagName!,
            InstallerDownloadUrl = installer.BrowserDownloadUrl!,
            InstallerFileName = installer.Name!,
            InstallerSizeBytes = installer.Size,
            ReleaseNotesUrl = release.HtmlUrl,
            ReleaseName = release.Name,
            ExpectedSha256 = expectedSha
        };
    }

    public async Task DownloadInstallerAsync(UpdateInfo update, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        if (!IsTrustedDownloadUrl(update.InstallerDownloadUrl))
        {
            throw new InvalidOperationException(
                "Update download rejected: installer URL is not on the trusted GitHub asset host whitelist.");
        }

        using var response = await _httpClient
            .GetAsync(update.InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // GitHub serves the asset via a 302 to a signed CDN URL. HttpClient
        // follows it transparently, so the final response URI is what we
        // must re-validate against the host whitelist.
        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null && !IsTrustedHost(finalUri))
        {
            throw new InvalidOperationException(
                $"Update download rejected: redirected to untrusted host '{finalUri.Host}'.");
        }

        var totalBytes = response.Content.Headers.ContentLength ?? update.InstallerSizeBytes;
        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, useAsync: true))
        {
            var buffer = new byte[DownloadBufferSize];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                if (progress is not null && totalBytes > 0)
                {
                    progress.Report((double)received / totalBytes);
                }
            }
        }

        if (!string.IsNullOrEmpty(update.ExpectedSha256))
        {
            await VerifyDigestOrThrowAsync(destinationPath, update.ExpectedSha256, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Downloads the integrity manifest and extracts the SHA-256 entry that
    /// matches <paramref name="installerFileName"/>. Returns <c>null</c> on
    /// any failure so the caller can decide whether to fall back to legacy
    /// behaviour or refuse the update — keeping this method total simplifies
    /// the call site.
    /// </summary>
    private async Task<string?> TryFetchChecksumAsync(string url, string installerFileName, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var manifest = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ExtractDigest(manifest, installerFileName);
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    /// <summary>
    /// Internal parser exposed to tests. Returns the lower-case 64-char
    /// digest matching <paramref name="installerFileName"/> (case-insensitive)
    /// or <c>null</c> when the file is not listed.
    /// </summary>
    internal static string? ExtractDigest(string manifest, string installerFileName)
    {
        if (string.IsNullOrEmpty(manifest) || string.IsNullOrEmpty(installerFileName))
        {
            return null;
        }

        foreach (Match match in ChecksumLinePattern.Matches(manifest))
        {
            var file = match.Groups["file"].Value.Trim();
            if (string.Equals(file, installerFileName, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["hash"].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    private static async Task VerifyDigestOrThrowAsync(string filePath, string expectedHex, CancellationToken cancellationToken)
    {
        string actual;
        await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, DownloadBufferSize, useAsync: true))
        {
            var bytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            actual = Convert.ToHexString(bytes).ToLowerInvariant();
        }

        if (!string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase))
        {
            // Best-effort: remove the corrupted/tampered file so it can
            // never be executed by mistake on a later code path. Only
            // IO/access errors are expected; anything else propagates
            // so the caller's CrashLogger boundary records it.
            try { File.Delete(filePath); }
            catch (IOException) { /* file in use by AV scanner */ }
            catch (UnauthorizedAccessException) { /* read-only or ACL */ }

            throw new InstallerIntegrityException(expectedHex, actual);
        }
    }

    /// <summary>
    /// Validates a candidate download URL against the HTTPS + host whitelist
    /// policy. <c>false</c> for null/empty input or any URL that is not
    /// served from a trusted GitHub asset host.
    /// </summary>
    internal static bool IsTrustedDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return IsTrustedHost(uri);
    }

    private static bool IsTrustedHost(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
           && AllowedDownloadHosts.Contains(uri.Host);

    private static bool TryParseTag(string tag, out Version version)
    {
        var trimmed = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(trimmed, out version!);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Tabu-Updater/1.0 (+https://github.com/JahelCuadrado/Tabu)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
