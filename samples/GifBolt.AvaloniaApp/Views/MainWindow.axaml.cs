// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GifBolt;
using GifBolt.Avalonia;

namespace GifBolt.AvaloniaApp.Views;

/// <summary>
/// Main window for the GifBolt Avalonia sample application.
/// Demonstrates both custom control and attached property usage.
/// </summary>
public partial class MainWindow : Window
{
    private Image? _gifControl;
    private Image? _imageBehavior;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        this.InitializeComponent();
        this.Opened += this.OnWindowOpened;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this._gifControl = this.FindControl<Image>("gifControl");
        this._imageBehavior = this.FindControl<Image>("imageBehaviorImage");
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Update status
        var statusText = this.FindControl<global::Avalonia.Controls.TextBlock>("statusText");
        if (statusText != null)
        {
            statusText.Text = "GifBolt loaded - Metal backend active on macOS";
        }

        // Update backend info
        var backendInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("backendInfo");
        if (backendInfo != null)
        {
            backendInfo.Text = "Backend: Metal (macOS GPU-accelerated)";
        }

        // Update version info
        this.UpdateVersionInfo();
    }

    private void UpdateVersionInfo()
    {
        try
        {
            var versionInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("versionInfo");
            var versionDetails = this.FindControl<global::Avalonia.Controls.TextBlock>("versionDetails");

            if (versionInfo != null)
            {
                var version = GifBolt.NativeVersion.VersionString;
                versionInfo.Text = $"GifBolt.Native v{version}";
            }

            if (versionDetails != null)
            {
                var major = GifBolt.NativeVersion.Major;
                var minor = GifBolt.NativeVersion.Minor;
                var patch = GifBolt.NativeVersion.Patch;
                versionDetails.Text = $"Version: {major}.{minor}.{patch} (Semantic Versioning 2.0.0)\n" +
                                     $"API Level: {GifBolt.NativeVersion.Version}\n" +
                                     "Compatible with version 1.0.0+";
            }
        }
        catch (Exception ex)
        {
            var versionInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("versionInfo");
            if (versionInfo != null)
            {
                versionInfo.Text = $"Version unavailable: {ex.Message}";
            }
        }
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
    }

    private async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        var files = await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select GIF File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("GIF Images")
                {
                    Patterns = new[] { "*.gif" }
                }
            }
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            // Update both controls with the same loaded GIF
            if (this._gifControl != null)
            {
                AnimationBehavior.SetSourceUri(this._gifControl, path);
            }
            if (this._imageBehavior != null)
            {
                AnimationBehavior.SetSourceUri(this._imageBehavior, path);
            }
            this.UpdateStatus($"Loaded: {System.IO.Path.GetFileName(path)}");
        }
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void UpdateStatus(string message)
    {
        var statusText = this.FindControl<global::Avalonia.Controls.TextBlock>("statusText");
        if (statusText != null)
        {
            statusText.Text = message;
        }
    }
}
