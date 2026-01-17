// SPDX-License-Identifier: MIT
using System.Windows;
using Microsoft.Win32;

namespace GifBolt.SampleApp
{
    /// <summary>
    /// Main window showcasing GifBoltControl.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            // Control's internal Play() will be exposed via native layer
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            // Control's internal Pause() will be exposed via native layer
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            // Control's internal Stop() will be exposed via native layer
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
                this.GifControl.Source = dlg.FileName;
            }
        }
    }
}
