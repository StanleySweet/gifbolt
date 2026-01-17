// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GifBolt.Avalonia;

namespace GifBolt.AvaloniaApp.Views;

/// <summary>
/// Main window for the GifBolt Avalonia sample application.
/// Demonstrates both custom control and attached property usage.
/// </summary>
public partial class MainWindow : Window
{
    private GifBoltControl? _gifControl;

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
        this._gifControl = this.FindControl<GifBoltControl>("gifControl");
        // Appliquer le délai minimal de 100ms (macOS style)
        if (this._gifControl != null)
        {
            this._gifControl.MinFrameDelayMs = 100;
        }
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
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        this._gifControl?.Play();
        this.UpdateStatus("Playing GIF animation");
    }

    private void OnPauseClick(object? sender, RoutedEventArgs e)
    {
        this._gifControl?.Pause();
        this.UpdateStatus("Paused");
    }

    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        this._gifControl?.Stop();
        this.UpdateStatus("Stopped - Reset to first frame");
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
            this._gifControl?.LoadGif(path);
            this.UpdateStatus($"Loaded: {System.IO.Path.GetFileName(path)}");
        }
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
