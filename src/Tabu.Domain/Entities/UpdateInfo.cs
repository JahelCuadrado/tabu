namespace Tabu.Domain.Entities;

/// <summary>
/// Information about an available newer release published on the upstream
/// distribution channel (e.g. GitHub Releases).
/// </summary>
public sealed class UpdateInfo
{
    public required Version Version { get; init; }
    public required string Tag { get; init; }
    public required string InstallerDownloadUrl { get; init; }
    public required string InstallerFileName { get; init; }
    public long InstallerSizeBytes { get; init; }
    public string? ReleaseNotesUrl { get; init; }
    public string? ReleaseName { get; init; }

    /// <summary>
    /// Lower-case hexadecimal SHA-256 of the installer asset, parsed from a
    /// <c>SHA256SUMS.txt</c> sibling asset published in the same release.
    /// When present, the update orchestrator MUST refuse to launch any
    /// downloaded artifact whose computed digest does not match this value.
    /// <c>null</c> for legacy releases that predate the integrity manifest;
    /// callers should treat that case according to their own policy.
    /// </summary>
    public string? ExpectedSha256 { get; init; }
}
