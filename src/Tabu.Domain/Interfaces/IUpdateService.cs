using Tabu.Domain.Entities;

namespace Tabu.Domain.Interfaces;

/// <summary>
/// Abstraction over the update distribution channel. Implementations are
/// responsible for resolving the latest published release and downloading
/// its installer payload to a local path.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Returns the latest available update if it is strictly newer than
    /// <paramref name="currentVersion"/>, otherwise <c>null</c>.
    /// </summary>
    Task<UpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the installer described by <paramref name="update"/> into
    /// <paramref name="destinationPath"/>. Existing files are overwritten.
    /// </summary>
    Task DownloadInstallerAsync(UpdateInfo update, string destinationPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}
