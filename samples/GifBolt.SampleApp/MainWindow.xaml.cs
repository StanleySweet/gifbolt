// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GifBolt;
using GifBolt.Internal;
using GifBolt.Wpf;
using Microsoft.Win32;

namespace GifBolt.SampleApp
{
    /// <summary>
    /// Main window for the GifBolt WPF sample application.
    /// Demonstrates both custom control and attached property usage.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            try
            {
                this.InitializeComponent();
                this.Loaded += this.OnWindowLoaded;
                LogToFile("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                LogToFile($"FATAL ERROR in MainWindow constructor: {ex}");
                MessageBox.Show($"Error: {ex.Message}\n\nSee gifbolt_load.log for details", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Update status
            this.UpdateStatus("GifBolt loaded - DirectX 11 backend active on Windows");

            // Load sample.gif as embedded resource (in-memory)
            this.LoadSampleGifFromResources();

            // Update version info
            this.UpdateVersionInfo();
        }

        private void LoadSampleGifFromResources()
        {
            try
            {
                // Try to find sample.gif in multiple locations
                string[] possiblePaths = new[]
                {
                    "sample.gif",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample.gif"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../tests/assets/sample.gif"),
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../tests/assets/sample.gif"))
                };

                string? foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath != null)
                {
                    // Load as in-memory bytes
                    byte[] gifData = File.ReadAllBytes(foundPath);
                    this.GifControl.LoadGif(gifData);

                    if (this.ImageBehaviorImage != null)
                    {
                        ImageBehavior.SetAnimatedSource(this.ImageBehaviorImage, foundPath);
                    }

                    this.UpdateStatus($"Loaded sample.gif from {Path.GetFileName(foundPath)} (in-memory)");
                }
                else
                {
                    this.UpdateStatus("sample.gif not found - use Load button to select a GIF");
                    LogToFile("sample.gif not found in any expected location");
                }
            }
            catch (Exception ex)
            {
                this.UpdateStatus($"Failed to load sample.gif: {ex.Message}");
                LogToFile($"Failed to load sample.gif: {ex}");
            }
        }

        private void UpdateVersionInfo()
        {
            try
            {
                var version = NativeVersion.VersionString;
                this.VersionInfo.Text = $"GifBolt.Native v{version}";

                var major = NativeVersion.Major;
                var minor = NativeVersion.Minor;
                var patch = NativeVersion.Patch;
                this.VersionDetails.Text = $"Version: {major}.{minor}.{patch} (Semantic Versioning 2.0.0)\n" +
                                          $"API Level: {NativeVersion.Version}\n" +
                                          "Compatible with version 1.0.0+";
            }
            catch (Exception ex)
            {
                this.VersionInfo.Text = $"Version unavailable: {ex.Message}";
            }
        }

        private static void LogToFile(string message)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gifbolt_load.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            this.GifControl.Play();
            this.UpdateStatus("Playing GIF animation");
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            this.GifControl.Pause();
            this.UpdateStatus("Paused");
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            this.GifControl.Stop();
            this.UpdateStatus("Stopped - Reset to first frame");
        }

        private void OnLoadGif(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "GIF Files (*.gif)|*.gif|All Files (*.*)|*.*",
                Title = "Select a GIF file"
            };
            if (dlg.ShowDialog() == true)
            {
                // Update both controls with the same loaded GIF
                this.GifControl.LoadGif(dlg.FileName);
                if (this.ImageBehaviorImage != null)
                {
                    ImageBehavior.SetAnimatedSource(this.ImageBehaviorImage, dlg.FileName);
                }
                this.UpdateStatus($"Loaded: {Path.GetFileName(dlg.FileName)}");
            }
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.GifControl == null || this.FilterComboBox == null)
            {
                return;
            }

            try
            {
                var selectedIndex = this.FilterComboBox.SelectedIndex;
                var filterType = (ScalingFilter)selectedIndex;
                this.GifControl.ScalingFilter = filterType;
                this.UpdateStatus($"Scaling filter changed to: {filterType}");
            }
            catch (Exception ex)
            {
                this.UpdateStatus($"Error changing filter: {ex.Message}");
            }
        }

        private void UpdateStatus(string message)
        {
            if (this.StatusText != null)
            {
                this.StatusText.Text = message;
            }
        }
    }
}
