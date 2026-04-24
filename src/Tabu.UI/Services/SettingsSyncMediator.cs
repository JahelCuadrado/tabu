using System.Windows;
using System.Windows.Media;
using Tabu.Domain.Entities;
using Tabu.Domain.Interfaces;
using Tabu.UI.Helpers;
using Tabu.UI.ViewModels;

namespace Tabu.UI.Services;

/// <summary>
/// Bridges the primary <see cref="MainViewModel"/> with the two cross-
/// cutting side-effects every settings handler used to perform inline:
/// (1) snapshotting the view-model into a <see cref="UserSettings"/> DTO
/// and persisting it off the UI thread, and (2) re-evaluating the global
/// WPF resource brushes (notification dot + active tab) so theme/accent
/// changes propagate to every visual binding.
/// </summary>
/// <remarks>
/// Extracted from the v1.5.x <c>App</c> God Object as part of the
/// architectural audit (issue #12). Persistence failures are swallowed
/// and logged because <c>settings.json</c> being temporarily locked by
/// AV/backup software is not a fatal condition; the next user change
/// will retry.
/// </remarks>
internal sealed class SettingsSyncMediator
{
    private const double OpacityPercentToByte = 2.55;
    private const string AccentBrushKey = "AccentBrush";
    private const string NotificationDotBrushKey = "NotificationDotBrush";
    private const string ActiveTabBrushKey = "ActiveTabBackgroundBrush";
    private const string ActiveTabBlurBrushKey = "ActiveTabBackgroundBlurBrush";

    private readonly ISettingsRepository _repository;
    private readonly MainViewModel _viewModel;

    public SettingsSyncMediator(ISettingsRepository repository, MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(viewModel);
        _repository = repository;
        _viewModel = viewModel;
    }

    /// <summary>
    /// Builds the persistence DTO from the current view-model state and
    /// fires-and-forgets the write on a background thread. Safe to call
    /// after every user-driven change; the repository itself debounces
    /// nothing, but the JSON file is small enough that contention is a
    /// non-issue.
    /// </summary>
    public void Persist()
    {
        var snapshot = SnapshotViewModel();

        Task.Run(() =>
        {
            try
            {
                _repository.Save(snapshot);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("SettingsSync.Persist", ex);
            }
        });
    }

    /// <summary>
    /// Updates the global <c>NotificationDotBrush</c> resource. When
    /// <paramref name="hex"/> is empty or invalid the brush mirrors the
    /// current <c>AccentBrush</c>; otherwise the parsed color wins.
    /// </summary>
    public static void ApplyNotificationDotBrush(string hex)
    {
        var resources = System.Windows.Application.Current.Resources;
        Brush brush;
        if (!string.IsNullOrWhiteSpace(hex) && ColorParser.TryParse(hex, out var color))
        {
            brush = new SolidColorBrush(color);
        }
        else if (resources[AccentBrushKey] is Brush accent)
        {
            brush = accent;
        }
        else
        {
            brush = Brushes.DodgerBlue;
        }
        resources[NotificationDotBrushKey] = brush;
    }

    /// <summary>
    /// Updates the global <c>ActiveTabBackgroundBrush</c> (and its blur
    /// counterpart). Alpha derives from the opacity slider (0–100); empty
    /// hex falls back to the current accent, mirroring legacy behaviour.
    /// </summary>
    public static void ApplyActiveTabBrush(string hex, double opacityPercent)
    {
        var resources = System.Windows.Application.Current.Resources;
        var alpha = (byte)Math.Clamp((int)Math.Round(opacityPercent * OpacityPercentToByte), 0, 255);

        Color baseColor;
        if (!string.IsNullOrWhiteSpace(hex) && ColorParser.TryParse(hex, out var parsed))
        {
            baseColor = parsed;
        }
        else if (resources[AccentBrushKey] is SolidColorBrush accent)
        {
            baseColor = accent.Color;
        }
        else
        {
            baseColor = Colors.DodgerBlue;
        }

        var finalColor = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        var brush = new SolidColorBrush(finalColor);
        resources[ActiveTabBrushKey] = brush;
        // Blur mode uses a dedicated brush so the selected tab keeps a
        // legible contrast over the acrylic backdrop. Mirror the user's
        // tint there as well.
        resources[ActiveTabBlurBrushKey] = brush;
    }

    private UserSettings SnapshotViewModel() => new()
    {
        IsBarOnAllMonitors = _viewModel.IsBarOnAllMonitors,
        IsDetectSameScreenOnly = _viewModel.IsDetectSameScreenOnly,
        AppTheme = _viewModel.AppTheme.ToString(),
        BarOpacity = _viewModel.BarOpacity,
        UseFixedTabWidth = _viewModel.UseFixedTabWidth,
        ShowBranding = _viewModel.ShowBranding,
        Language = _viewModel.Language,
        AccentColor = _viewModel.AccentColor,
        AutoHideBar = _viewModel.AutoHideBar,
        LaunchAtStartup = _viewModel.LaunchAtStartup,
        ShowClock = _viewModel.ShowClock,
        ClockSize = _viewModel.ClockSize.ToString(),
        ShowNotificationBadges = _viewModel.ShowNotificationBadges,
        NotificationDotSize = (int)_viewModel.NotificationDotSize,
        NotificationDotColor = _viewModel.NotificationDotColor,
        BarSize = _viewModel.BarSize.ToString(),
        UseBlurEffect = _viewModel.UseBlurEffect,
        BlurMode = _viewModel.BlurMode,
        AutoCheckUpdates = _viewModel.AutoCheckUpdates,
        ActiveTabColor = _viewModel.ActiveTabColor,
        ActiveTabOpacity = (int)_viewModel.ActiveTabOpacity
    };
}
