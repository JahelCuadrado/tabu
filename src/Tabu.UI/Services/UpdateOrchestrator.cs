using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;

namespace Tabu.UI.Services;

/// <summary>
/// Coordinates the end-to-end update workflow: query the channel for a newer
/// release, prompt the user, download the installer to a temp folder and
/// hand control over to the silent installer before exiting the running app.
/// </summary>
public sealed class UpdateOrchestrator
{
    private const string SilentInstallerArguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART";

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
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckAndPromptAsync().ConfigureAwait(false);
            }
            catch
            {
                // Updates are best-effort; never crash the host app.
            }
        });
    }

    private async Task CheckAndPromptAsync()
    {
        var current = GetCurrentVersion();
        var update = await _updateService.CheckForUpdateAsync(current).ConfigureAwait(false);
        if (update is null)
        {
            return;
        }

        var accepted = await PromptUserAsync(current, update).ConfigureAwait(false);
        if (!accepted)
        {
            return;
        }

        var destination = Path.Combine(Path.GetTempPath(), update.InstallerFileName);

        try
        {
            await _updateService.DownloadInstallerAsync(update, destination).ConfigureAwait(false);
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
            var result = MessageBox.Show(body, title, MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes);
            tcs.SetResult(result == MessageBoxResult.Yes);
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
            MessageBox.Show(body, title, MessageBoxButton.OK, icon);
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
                MessageBox.Show(
                    LocalizedString("Update_LaunchFailedBody", "Tabu could not launch the installer."),
                    LocalizedString("Update_LaunchFailedTitle", "Update failed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
