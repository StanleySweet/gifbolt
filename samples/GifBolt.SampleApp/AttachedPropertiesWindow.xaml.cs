// SPDX-License-Identifier: MIT
using System.Windows;
using Microsoft.Win32;
using GifBolt.Wpf;

namespace GifBolt.SampleApp
{
    /// <summary>
    /// Démontre l'utilisation des attached properties (compatible WpfAnimatedGif).
    /// </summary>
    public sealed partial class AttachedPropertiesWindow : Window
    {
        public AttachedPropertiesWindow()
        {
            this.InitializeComponent();
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
                // Démontre l'API compatible WpfAnimatedGif
                ImageBehavior.SetAnimatedSource(this.Image1, dlg.FileName);
                ImageBehavior.SetAnimatedSource(this.Image3, dlg.FileName);
                ImageBehavior.SetAnimatedSource(this.ImageRepeat3x, dlg.FileName);
                ImageBehavior.SetAnimatedSource(this.ImageManual, dlg.FileName);
            }
        }
    }
}
