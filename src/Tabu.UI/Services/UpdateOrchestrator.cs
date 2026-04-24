using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Tabu.Domain.Updates;
using Tabu.UI.Views;

namespace Tabu.UI.Services;

/// <summary>
/// Coordinates the end-to-end update workflow: query the channel for a newer
/// release, prompt the user, download the installer to a temp folder and
/// hand control over to the silent installer before exiting the running app.
/// </summary>
public sealed class UpdateOrchestrator
{
    private const string SilentInstallerArguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART";

    /// <summary>
    /// Hard upper bound for the installer file size accepted from the
    /// update channel (250 MiB). A defensive guard against accidentally
    /// downloading something pathologically large advertised by a tampered
    /// release JSON; the genuine Tabu installer is &lt; 50 MiB.
    /// </summary>
    private const long MaxInstallerSizeBytes = 250L * 1024 * 1024;

    /// <summary>
    /// Age (in days) past which leftover Tabu installers in <c>%TEMP%</c>
    /// are deleted on the next update flow. Keeps the user's disk tidy
    /// without aggressively racing in-progress writes from a concurrent run.
    /// </summary>
    private const int StaleInstallerMaxAgeDays = 7;

    private readonly IUpdateService _updateService;

    public UpdateOrchestrator(IUpdateService updateService)
    {
        _updateService = updateService;
    }

    /// <summary>
    /// Runs a non-blocking update check. Any network or IO error is swallowed
    /// so that update problems never break application startup.
    /// </summary>
    public void RunInBackground()
    {
        // The work is already fully async I/O; wrapping it in Task.Run only
        // burns a thread-pool slot. Fire-and-forget directly and observe
        // the resulting Task to neutralise the unobserved-exception event.
        _ = ObserveAsync(CheckAndPromptAsync());

        static async Task ObserveAsync(Task work)
        {
            try { await work.ConfigureAwait(false); }
            catch { /* updates are best-effort; never crash the host app */ }
        }
    }

    /// <summary>
    /// User-initiated update check that always reports a result back to the
    /// user — either the standard "update available" prompt, an
    /// "you're up to date" notice, or a localized error dialog. Unlike
    /// <see cref="RunInBackground"/>, this method never silently swallows
    /// failures because the user explicitly asked for feedback.
    /// </summary>
    public void RunManualCheck()
    {
        _ = ObserveManualAsync();

        async Task ObserveManualAsync()
        {
            try
            {
                var current = GetCurrentVersion();
                var update = await _updateService.CheckForUpdateAsync(current).ConfigureAwait(false);
                if (update is null)
                {
                    await ShowMessageAsync(
                        LocalizedString("Update_UpToDateTitle", "Up to date"),
                        string.Format(
                            LocalizedString("Update_UpToDateBody", "You are running the latest version of Tabu (v{0})."),
                            current.ToString(3)),
                        MessageBoxImage.Information).ConfigureAwait(false);
                    return;
                }

                await ContinueWithUpdateAsync(current, update).ConfigureAwait(false);
            }
            catch
            {
                await ShowMessageAsync(
                    LocalizedString("Update_CheckFailedTitle", "Update check failed"),
                    LocalizedString("Update_CheckFailedBody", "Tabu could not reach the update server. Please check your connection and try again."),
                    MessageBoxImage.Warning).ConfigureAwait(false);
            }
        }
    }

    private async Task CheckAndPromptAsync()
    {
        var current = GetCurrentVersion();
        var update = await _updateService.CheckForUpdateAsync(current).ConfigureAwait(false);
        if (update is null)
        {
            return;
        }

        await ContinueWithUpdateAsync(current, update).ConfigureAwait(false);
    }

    private async Task ContinueWithUpdateAsync(Version current, UpdateInfo update)
    {
        var accepted = await PromptUserAsync(current, update).ConfigureAwait(false);
        if (!accepted)
        {
            return;
        }

        // Defensive size guard: the genuine installer fits well under the
        // limit; anything larger almost certainly means a tampered or
        // misconfigured release. Reject before allocating disk space.
        if (update.InstallerSizeBytes > MaxInstallerSizeBytes)
        {
            await ShowMessageAsync(
                LocalizedString("Update_DownloadFailedTitle", "Update failed"),
                LocalizedString("Update_DownloadFailedBody", "Tabu could not download the latest installer. Please try again later."),
                MessageBoxImage.Warning).ConfigureAwait(false);
            return;
        }

        // Strip any path segment that may have leaked into the asset name
        // ("subdir/..\\evil.exe") so the destination is always inside the
        // session's temp folder. Path.GetFileName(...) is the documented
        // Windows-safe way to discard traversal sequences.
        var safeFileName = Path.GetFileName(update.InstallerFileName);
        if (string.IsNullOrEmpty(safeFileName))
        {
            return;
        }

        // Best-effort housekeeping: previous failed runs may have left
        // half-downloaded installers behind. Doing this BEFORE the new
        // download avoids accumulating gigabytes over time.
        TryCleanupStaleInstallers(safeFileName);

        var destination = Path.Combine(Path.GetTempPath(), safeFileName);

        try
        {
            await _updateService.DownloadInstallerAsync(update, destination).ConfigureAwait(false);
        }
        catch (InstallerIntegrityException)
        {
            // Hard-fail with a dedicated dialog: a digest mismatch is a
            // strong signal of tampering or a corrupted CDN cache, not
            // just a transient network glitch. The user must be told
            // explicitly so they don't blindly retry.
            await ShowMessageAsync(
                LocalizedString("Update_IntegrityFailedTitle", "Update integrity check failed"),
                LocalizedString("Update_IntegrityFailedBody", "The downloaded installer did not match the expected SHA-256 digest and was deleted. This may indicate the file was tampered with in transit. Please try again later."),
                MessageBoxImage.Error).ConfigureAwait(false);
            return;
        }
        catch
        {
            await ShowMessageAsync(
                LocalizedString("Update_DownloadFailedTitle", "Update failed"),
                LocalizedString("Update_DownloadFailedBody", "Tabu could not download the latest installer. Please try again later."),
                MessageBoxImage.Warning).ConfigureAwait(false);
            return;
        }

        LaunchInstallerAndShutdown(destination);
    }

    /// <summary>
    /// Deletes Tabu installers in <c>%TEMP%</c> older than
    /// <see cref="StaleInstallerMaxAgeDays"/> days. The pattern is
    /// intentionally narrow (<c>Tabu*setup*.exe</c>, case-insensitive on
    /// NTFS) so unrelated user files are never touched. Errors are swallowed
    /// — this is opportunistic cleanup, not part of the update contract.
    /// </summary>
    private static void TryCleanupStaleInstallers(string currentFileName)
    {
        try
        {
            var temp = Path.GetTempPath();
            var threshold = DateTime.UtcNow.AddDays(-StaleInstallerMaxAgeDays);
            foreach (var path in Directory.EnumerateFiles(temp, "Tabu*setup*.exe", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(path), currentFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // never delete the file we're about to overwrite
                }

                try
                {
                    if (File.GetLastWriteTimeUtc(path) < threshold)
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // File in use or access denied — skip and try next time.
                }
            }
        }
        catch
        {
            // Temp folder enumeration is non-critical; never propagate.
        }
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version ?? new Version(0, 0, 0, 0);
    }

    private static Task<bool> PromptUserAsync(Version current, UpdateInfo update)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            tcs.SetResult(false);
            return tcs.Task;
        }

        dispatcher.BeginInvoke(() =>
        {
            var title = LocalizedString("Update_AvailableTitle", "Update available");
            var template = LocalizedString(
                "Update_AvailableBody",
                "A new version of Tabu is available.\n\nCurrent: {0}\nLatest: {1}\n\nDownload and install now?");
            var body = string.Format(template, current.ToString(3), update.Version.ToString(3));
            var yesText = LocalizedString("Dialog_Yes", "Yes");
            var noText = LocalizedString("Dialog_No", "Cancel");
            var result = TabuDialog.Show(
                System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
                body,
                title,
                TabuDialogVariant.Info,
                primaryText: yesText,
                secondaryText: noText);
            tcs.SetResult(result == TabuDialogResult.Yes);
        });

        return tcs.Task;
    }

    private static Task ShowMessageAsync(string title, string body, MessageBoxImage icon)
    {
        var tcs = new TaskCompletionSource();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            tcs.SetResult();
            return tcs.Task;
        }

        dispatcher.BeginInvoke(() =>
        {
            var variant = icon == MessageBoxImage.Warning ? TabuDialogVariant.Warning
                : icon == MessageBoxImage.Error ? TabuDialogVariant.Danger
                : TabuDialogVariant.Info;
            TabuDialog.Show(
                System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
                body,
                title,
                variant);
            tcs.SetResult();
        });

        return tcs.Task;
    }

    private static void LaunchInstallerAndShutdown(string installerPath)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = SilentInstallerArguments,
                    UseShellExecute = true
                });
            }
            catch
            {
                TabuDialog.Show(
                    System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
                    LocalizedString("Update_LaunchFailedBody", "Tabu could not launch the installer."),
                    LocalizedString("Update_LaunchFailedTitle", "Update failed"),
                    TabuDialogVariant.Warning);
                return;
            }

            System.Windows.Application.Current?.Shutdown();
        });
    }

    private static string LocalizedString(string key, string fallback)
    {
        var resource = System.Windows.Application.Current?.TryFindResource(key) as string;
        return string.IsNullOrEmpty(resource) ? fallback : resource;
    }
}
