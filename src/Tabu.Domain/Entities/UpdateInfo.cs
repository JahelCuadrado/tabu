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
}
