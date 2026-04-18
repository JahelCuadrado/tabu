using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tabu.Domain.Entities;
using Tabu.UI.Helpers;

namespace Tabu.UI.ViewModels;

public sealed class TabViewModel : ObservableObject
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private string _displayName = string.Empty;
    private bool _isActive;
    private ImageSource? _icon;

    public TrackedWindow Model { get; }
    public IntPtr Handle => Model.Handle;

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public TabViewModel(TrackedWindow model)
    {
        Model = model;
        _displayName = Truncate(model.Title, 28);
        _isActive = model.IsActive;
        LoadIcon(model.ExecutablePath);
    }

    public void UpdateFrom(TrackedWindow updated)
    {
        DisplayName = Truncate(updated.Title, 28);
        IsActive = updated.IsActive;
    }

    private void LoadIcon(string executablePath)
    {
        // The HICON returned by ExtractAssociatedIcon owns an unmanaged GDI
        // handle that WPF's CreateBitmapSourceFromHIcon does NOT take
        // ownership of, so we must explicitly dispose the managed wrapper
        // and call DestroyIcon afterwards. Failing to do so leaks one GDI
        // handle per tracked window and eventually crashes the process
        // (default per-process limit is 10,000).
        try
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath)) return;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null) return;

            var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            Icon = bitmap;

            DestroyIcon(icon.Handle);
        }
        catch
        {
            // Icon extraction is best-effort and must never break tab tracking.
        }
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Window";
        return text.Length > maxLength ? string.Concat(text.AsSpan(0, maxLength - 3), "...") : text;
    }
}
