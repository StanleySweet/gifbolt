// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private global::Avalonia.Controls.TextBlock? _fpsDisplay;
    private DispatcherTimer? _fpsTimer;

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
        this._fpsDisplay = this.FindControl<global::Avalonia.Controls.TextBlock>("fpsDisplay");

        // Setup FPS update timer
        this._fpsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        this._fpsTimer.Tick += (s, e) => this.UpdateFpsDisplay();
        this._fpsTimer.Start();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Update status
        this.UpdateStatus("GifBolt loaded - Metal backend active on macOS");

        // Update backend info with native texture support
        this.UpdateBackendInfo();
        this.UpdateMainViewBackendInfo();

        // Update version info
        this.UpdateVersionInfo();
    }

    private void UpdateBackendInfo()
    {
        try
        {
            var backendInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("backendInfo");
            if (backendInfo != null)
            {
                // Create a temporary player to check backend
                using var player = new GifBolt.GifPlayer();
                var testGif = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";
                if (player.Load(testGif))
                {
                    var backend = player.GetBackend();
                    var backendName = backend switch
                    {
                        0 => "Dummy (Software)",
                        1 => "DirectX 11 (Windows)",
                        2 => "Metal (macOS)",
                        _ => "Unknown"
                    };

                    // Check if native texture is available
                    var texturePtr = player.GetNativeTexturePtr(0);
                    var gpuStatus = texturePtr != IntPtr.Zero ? "✅ GPU textures available" : "❌ GPU textures unavailable";

                    backendInfo.Text = $"Backend: {backendName} (ID: {backend})\n{gpuStatus}\nNative Texture Ptr: 0x{texturePtr:X}";
                }
                else
                {
                    backendInfo.Text = "Backend: Unable to detect (test file not loaded)";
                }
            }
        }
        catch (Exception ex)
        {
            var backendInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("backendInfo");
            if (backendInfo != null)
            {
                backendInfo.Text = $"Backend detection failed: {ex.Message}";
            }
        }
    }

    private void UpdateMainViewBackendInfo()
    {
        try
        {
            var mainBackendInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("mainBackendInfo");
            if (mainBackendInfo != null)
            {
                using var player = new GifBolt.GifPlayer();
                var testGif = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";
                if (player.Load(testGif))
                {
                    var backend = player.GetBackend();
                    var backendName = backend switch
                    {
                        0 => "Dummy",
                        1 => "DirectX 11",
                        2 => "Metal",
                        _ => "Unknown"
                    };

                    var texturePtr = player.GetNativeTexturePtr(0);
                    var frameCount = player.FrameCount;

                    if (texturePtr != IntPtr.Zero)
                    {
                        mainBackendInfo.Text = $"Backend: {backendName}\n✅ GPU Accelerated | {frameCount} frames\nNative Texture: 0x{texturePtr:X}";
                    }
                    else
                    {
                        mainBackendInfo.Text = $"Backend: {backendName}\n⚠️ Software Rendering | {frameCount} frames";
                    }
                }
                else
                {
                    mainBackendInfo.Text = "Backend: Detection failed";
                }
            }
        }
        catch (Exception ex)
        {
            var mainBackendInfo = this.FindControl<global::Avalonia.Controls.TextBlock>("mainBackendInfo");
            if (mainBackendInfo != null)
            {
                mainBackendInfo.Text = $"Backend: Error - {ex.Message}";
            }
        }
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
            this.UpdateMainViewBackendInfo();
        }
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedIndex < 0)
        {
            return;
        }

        var filterItem = comboBox.SelectedItem as ComboBoxItem;
        if (filterItem == null)
        {
            return;
        }

        var filterName = filterItem.Content?.ToString() ?? "Unknown";
        this.UpdateStatus($"Scaling filter: {filterName}");

        // Apply the filter to the currently loaded animation
        if (this._gifControl != null)
        {
            var controller = GifBolt.Avalonia.AnimationBehavior.GetAnimationController(this._gifControl);
            if (controller != null)
            {
                // Convert filter name to ScalingFilter enum
                if (Enum.TryParse<GifBolt.ScalingFilter>(filterName, out var filter))
                {
                    controller.SetScalingFilter(filter);
                }
            }
        }
    }

    private void UpdateFpsDisplay()
    {
        if (this._fpsDisplay == null || this._gifControl == null)
        {
            return;
        }

        var fpsText = AnimationBehavior.GetFpsText(this._gifControl);
        if (!string.IsNullOrEmpty(fpsText))
        {
            this._fpsDisplay.Text = fpsText;
        }
    }

    private void UpdateStatus(string message)
    {
        // Defer to dispatcher to ensure control tree is fully initialized

        try
        {
            var statusTextControl = this.FindControl<global::Avalonia.Controls.TextBlock>("statusText");
            if (statusTextControl != null)
            {
                statusTextControl.Text = message;
            }
        }
        catch
        {
            // Ignore errors if control isn't available
        }
    }
}
