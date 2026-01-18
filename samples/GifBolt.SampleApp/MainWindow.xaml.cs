// SPDX-License-Identifier: MIT
using System.Windows;
using System.Windows.Controls;
using GifBolt.Internal;
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

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.filterComboBox != null && this.filterComboBox.SelectedIndex >= 0 && this.filterComboBox.SelectedIndex < 4 && this.GifControl != null)
            {
                var filter = (ScalingFilter)this.filterComboBox.SelectedIndex;
                this.GifControl.ScalingFilter = filter;
            }
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            this.GifControl.Play();
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            this.GifControl.Pause();
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
            this.GifControl.Stop();
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
                this.GifControl.LoadGif(dlg.FileName);
            }
        }
    }
}
