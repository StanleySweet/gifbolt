// SPDX-License-Identifier: MIT
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Attached properties pour animer des GIFs sur un contrôle Image standard.
    /// Compatible avec l'API de WpfAnimatedGif (drop-in replacement).
    /// </summary>
    public static class ImageBehavior
    {
        #region AnimatedSource (compatible WpfAnimatedGif)

        /// <summary>
        /// Source du GIF animé (Uri ou string).
        /// </summary>
        public static readonly DependencyProperty AnimatedSourceProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedSource",
                typeof(object),
                typeof(ImageBehavior),
                new PropertyMetadata(null, OnAnimatedSourceChanged));

        /// <summary>Obtient la source animée.</summary>
        public static object GetAnimatedSource(Image image) => image.GetValue(AnimatedSourceProperty);

        /// <summary>Définit la source animée.</summary>
        public static void SetAnimatedSource(Image image, object value) => image.SetValue(AnimatedSourceProperty, value);

        #endregion

        #region RepeatBehavior (compatible WpfAnimatedGif)

        /// <summary>
        /// Comportement de répétition (ex: "3x", "Forever", "0x" = utiliser métadonnées GIF).
        /// </summary>
        public static readonly DependencyProperty RepeatBehaviorProperty =
            DependencyProperty.RegisterAttached(
                "RepeatBehavior",
                typeof(string),
                typeof(ImageBehavior),
                new PropertyMetadata("0x", OnRepeatBehaviorChanged));

        /// <summary>Obtient le comportement de répétition.</summary>
        public static string GetRepeatBehavior(Image image) => (string)image.GetValue(RepeatBehaviorProperty);

        /// <summary>Définit le comportement de répétition.</summary>
        public static void SetRepeatBehavior(Image image, string value) => image.SetValue(RepeatBehaviorProperty, value);

        #endregion

        #region AutoStart

        /// <summary>
        /// Démarre automatiquement l'animation au chargement.
        /// </summary>
        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.RegisterAttached(
                "AutoStart",
                typeof(bool),
                typeof(ImageBehavior),
                new PropertyMetadata(true));

        /// <summary>Obtient si l'animation démarre automatiquement.</summary>
        public static bool GetAutoStart(Image image) => (bool)image.GetValue(AutoStartProperty);

        /// <summary>Définit si l'animation démarre automatiquement.</summary>
        public static void SetAutoStart(Image image, bool value) => image.SetValue(AutoStartProperty, value);

        #endregion

        #region AnimationController (internal state)

        private static readonly DependencyProperty AnimationControllerProperty =
            DependencyProperty.RegisterAttached(
                "AnimationController",
                typeof(GifAnimationController),
                typeof(ImageBehavior),
                new PropertyMetadata(null));

        private static GifAnimationController GetAnimationController(Image image) =>
            (GifAnimationController)image.GetValue(AnimationControllerProperty);

        private static void SetAnimationController(Image image, GifAnimationController value) =>
            image.SetValue(AnimationControllerProperty, value);

        #endregion

        #region Event Handlers

        private static void OnAnimatedSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Image image)) return;

            var controller = GetAnimationController(image);
            if (controller != null)
            {
                controller.Dispose();
                SetAnimationController(image, null);
            }

            if (e.NewValue == null) return;

            string path = GetPathFromSource(e.NewValue);
            if (string.IsNullOrWhiteSpace(path)) return;

            controller = new GifAnimationController(image, path);
            SetAnimationController(image, controller);

            var repeatBehavior = GetRepeatBehavior(image);
            controller.SetRepeatBehavior(repeatBehavior);

            if (GetAutoStart(image))
            {
                controller.Play();
            }

            image.Unloaded += OnImageUnloaded;
        }

        private static void OnRepeatBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Image image)) return;

            var controller = GetAnimationController(image);
            if (controller != null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Image image)
            {
                image.Unloaded -= OnImageUnloaded;
                var controller = GetAnimationController(image);
                if (controller != null)
                {
                    controller.Dispose();
                    SetAnimationController(image, null);
                }
            }
        }

        #endregion

        #region Helpers

        private static string GetPathFromSource(object source)
        {
            if (source is string str)
                return str;

            if (source is Uri uri)
                return uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;

            if (source is BitmapImage bitmap && bitmap.UriSource != null)
                return bitmap.UriSource.IsAbsoluteUri ? bitmap.UriSource.LocalPath : bitmap.UriSource.OriginalString;

            return null;
        }

        #endregion
    }

    /// <summary>
    /// Contrôleur interne gérant l'animation d'un GIF sur une Image.
    /// </summary>
    internal sealed class GifAnimationController : IDisposable
    {
        private readonly Image _image;
        private readonly GifPlayer _player;
        private WriteableBitmap _writeableBitmap;
        private bool _isPlaying;
        private int _repeatCount;

        public GifAnimationController(Image image, string path)
        {
            this._image = image;
            this._player = new GifPlayer();

            if (!this._player.Load(path))
            {
                this._player.Dispose();
                throw new InvalidOperationException($"Failed to load GIF: {path}");
            }

            this._writeableBitmap = new WriteableBitmap(
                this._player.Width,
                this._player.Height,
                96, 96,
                PixelFormats.Bgra32,
                null);

            this._image.Source = this._writeableBitmap;

            CompositionTarget.Rendering += this.OnRendering;
        }

        public void SetRepeatBehavior(string repeatBehavior)
        {
            if (string.IsNullOrWhiteSpace(repeatBehavior) || repeatBehavior == "0x")
            {
                // Utiliser les métadonnées du GIF
                this._repeatCount = this._player.IsLooping ? -1 : 1;
            }
            else if (repeatBehavior.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                this._repeatCount = -1;
            }
            else if (repeatBehavior.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var countStr = repeatBehavior.Substring(0, repeatBehavior.Length - 1);
                if (int.TryParse(countStr, out int count))
                {
                    this._repeatCount = count;
                }
            }
        }

        public void Play()
        {
            this._player.Play();
            this._isPlaying = true;
        }

        public void Pause()
        {
            this._player.Pause();
            this._isPlaying = false;
        }

        public void Stop()
        {
            this._player.Stop();
            this._isPlaying = false;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!this._isPlaying) return;

            if (this._player.TryGetFramePixelsRgba32(this._player.CurrentFrame, out byte[] pixels))
            {
                this._writeableBitmap.Lock();
                try
                {
                    unsafe
                    {
                        var buffer = (byte*)this._writeableBitmap.BackBuffer;
                        int stride = this._writeableBitmap.BackBufferStride;
                        int width = this._player.Width;
                        int height = this._player.Height;

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int srcIdx = (y * width + x) * 4;
                                int dstIdx = y * stride + x * 4;

                                // RGBA -> BGRA
                                buffer[dstIdx + 0] = pixels[srcIdx + 2]; // B
                                buffer[dstIdx + 1] = pixels[srcIdx + 1]; // G
                                buffer[dstIdx + 2] = pixels[srcIdx + 0]; // R
                                buffer[dstIdx + 3] = pixels[srcIdx + 3]; // A
                            }
                        }
                    }

                    this._writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, this._player.Width, this._player.Height));
                }
                finally
                {
                    this._writeableBitmap.Unlock();
                }
            }

            // Avance frame selon timing (simplification - dans une vraie impl, utiliser un timer précis)
            // TODO: Gérer le timing exact avec GetFrameDelayMs()
        }

        public void Dispose()
        {
            CompositionTarget.Rendering -= this.OnRendering;
            this._player?.Dispose();
        }
    }
}
