using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.Infrastructure.Updates;

/// <summary>
/// Resolves Tabu releases through the public GitHub Releases API and streams
/// installer downloads to disk. The implementation is intentionally tiny: a
/// single shared <see cref="HttpClient"/> is used per process and no auth
/// token is required because the source repository is public.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    private const string LatestReleaseEndpoint =
        "https://api.github.com/repos/JahelCuadrado/tabu/releases/latest";

    /// <summary>
    /// Matches the installer asset name (e.g. <c>TabuSetup-v1.3.2-win-x64.exe</c>).
    /// Exposed for tests to assert against the same canonical pattern.
    /// </summary>
    internal static readonly Regex InstallerAssetPattern =
        new(@"setup.*\.exe$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        if (installer is null)
        {
            return null;
        }

        return new UpdateInfo
        {
            Version = latestVersion,
            Tag = release.TagName!,
            InstallerDownloadUrl = installer.BrowserDownloadUrl!,
            InstallerFileName = installer.Name!,
            InstallerSizeBytes = installer.Size,
            ReleaseNotesUrl = release.HtmlUrl,
            ReleaseName = release.Name
        };
    }

    public async Task DownloadInstallerAsync(UpdateInfo update, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrEmpty(destinationPath);

        using var response = await _httpClient
            .GetAsync(update.InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.InstallerSizeBytes;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Tabu-Updater/1.0 (+https://github.com/JahelCuadrado/tabu)");
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
