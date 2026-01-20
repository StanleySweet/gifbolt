// SPDX-License-Identifier: MIT
using GifBolt;
using GifBolt.Wpf;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GifBolt.SampleApp
{
    /// <summary>
    /// Main window for the GifBolt WPF sample application.
    /// Demonstrates AnimationBehavior attached property usage with scaling filters.
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

            // Update version info
            this.UpdateVersionInfo();

            // Load a default GIF if available
            string sampleGif = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample.gif");
            if (File.Exists(sampleGif))
            {
                if (this.GifImageScaling != null)
                {
                    AnimationBehavior.SetSourceUri(this.GifImageScaling, sampleGif);
                }

                if (this.ImageBehaviorImage != null)
                {
                    AnimationBehavior.SetSourceUri(this.ImageBehaviorImage, sampleGif);
                }

                this.UpdateStatus($"Loaded sample GIF: {Path.GetFileName(sampleGif)}");
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

        private void OnLoadGif(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "GIF Files (*.gif)|*.gif|All Files (*.*)|*.*",
                Title = "Select a GIF file"
            };
            if (dlg.ShowDialog() == true)
            {
                // Update both Image controls with AnimationBehavior.SourceUri
                if (this.GifImageScaling != null)
                {
                    AnimationBehavior.SetSourceUri(this.GifImageScaling, dlg.FileName);
                }

                if (this.ImageBehaviorImage != null)
                {
                    AnimationBehavior.SetSourceUri(this.ImageBehaviorImage, dlg.FileName);
                }

                this.UpdateStatus($"Loaded: {Path.GetFileName(dlg.FileName)}");
            }
        }

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.GifImageScaling == null || this.FilterComboBox == null)
            {
                return;
            }

            try
            {
                var selectedIndex = this.FilterComboBox.SelectedIndex;
                // Map ComboBox index to ScalingFilter enum: None=-1, Nearest=0, Bilinear=1, Bicubic=2, Lanczos=3
                var filterType = selectedIndex == 0 ? ScalingFilter.None : (ScalingFilter)(selectedIndex - 1);
                AnimationBehavior.SetScalingFilter(this.GifImageScaling, filterType);
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
