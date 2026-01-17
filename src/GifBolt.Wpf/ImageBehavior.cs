// SPDX-License-Identifier: MIT
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Attached properties for animating GIFs on standard WPF Image controls.
    /// Provides drop-in replacement compatibility with WpfAnimatedGif library.
    /// </summary>
    public static class ImageBehavior
    {
        #region AnimatedSource (compatible WpfAnimatedGif)

        /// <summary>
        /// Gets or sets the animated GIF source (Uri or string).
        /// Supports <see cref="string"/> file paths and <see cref="Uri"/> references.
        /// </summary>
        public static readonly DependencyProperty AnimatedSourceProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedSource",
                typeof(object),
                typeof(ImageBehavior),
                new PropertyMetadata(null, OnAnimatedSourceChanged));

        /// <summary>
        /// Gets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the animated source from.</param>
        /// <returns>The animated source value, or null if not set.</returns>
        public static object GetAnimatedSource(Image image) => image.GetValue(AnimatedSourceProperty);

        /// <summary>
        /// Sets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the animated source on.</param>
        /// <param name="value">The animated source value (string path or Uri).</param>
        public static void SetAnimatedSource(Image image, object value) => image.SetValue(AnimatedSourceProperty, value);

        #endregion

        #region RepeatBehavior (compatible WpfAnimatedGif)

        /// <summary>
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly DependencyProperty RepeatBehaviorProperty =
            DependencyProperty.RegisterAttached(
                "RepeatBehavior",
                typeof(string),
                typeof(ImageBehavior),
                new PropertyMetadata("0x", OnRepeatBehaviorChanged));

        /// <summary>
        /// Gets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the repeat behavior from.</param>
        /// <returns>The repeat behavior string.</returns>
        public static string GetRepeatBehavior(Image image) => (string)image.GetValue(RepeatBehaviorProperty);

        /// <summary>
        /// Sets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the repeat behavior on.</param>
        /// <param name="value">The repeat behavior string.</param>
        public static void SetRepeatBehavior(Image image, string value) => image.SetValue(RepeatBehaviorProperty, value);

        #endregion

        #region AutoStart

        /// <summary>
        /// Gets or sets whether animation starts automatically upon loading.
        /// </summary>
        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.RegisterAttached(
                "AutoStart",
                typeof(bool),
                typeof(ImageBehavior),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets whether auto-start is enabled for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to query.</param>
        /// <returns>true if animation starts automatically; otherwise false.</returns>
        public static bool GetAutoStart(Image image) => (bool)image.GetValue(AutoStartProperty);

        /// <summary>
        /// Sets whether animation starts automatically for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to configure.</param>
        /// <param name="value">true to start animation automatically; otherwise false.</param>
        public static void SetAutoStart(Image image, bool value) => image.SetValue(AutoStartProperty, value);

        #endregion

        #region AnimationController (internal state)

        private static readonly DependencyProperty _animationControllerProperty =
            DependencyProperty.RegisterAttached(
                "AnimationController",
                typeof(GifAnimationController),
                typeof(ImageBehavior),
                new PropertyMetadata(null));

        private static GifAnimationController? GetAnimationController(Image image) =>
            (GifAnimationController?)image.GetValue(_animationControllerProperty);

        private static void SetAnimationController(Image image, GifAnimationController? value) =>
            image.SetValue(_animationControllerProperty, value);

        #endregion

        #region Event Handlers

        private static void OnAnimatedSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller != null)
            {
                controller.Dispose();
                SetAnimationController(image, (GifAnimationController?)null);
            }

            if (e.NewValue == null)
            {
                return;
            }

            string? path = GetPathFromSource(e.NewValue);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            controller = new GifAnimationController(image, path!);
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
            if (d is not Image image)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller is not null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Image image)
            {
                return;
            }

            image.Unloaded -= OnImageUnloaded;
            var controller = GetAnimationController(image);
            if (controller is not null)
            {
                controller.Dispose();
                SetAnimationController(image, null);
            }
        }

        #endregion

        #region Helpers

        private static string? GetPathFromSource(object source)
        {
            if (source is string str)
            {
                return str;
            }

            if (source is Uri uri)
            {
                return uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
            }

            if (source is BitmapImage bitmap && bitmap.UriSource != null)
            {
                return bitmap.UriSource.IsAbsoluteUri ? bitmap.UriSource.LocalPath : bitmap.UriSource.ToString();
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// Internal controller managing GIF animation on a WPF Image control.
    /// Handles frame decoding, timing, and pixel updates to the display.
    /// </summary>
    internal sealed class GifAnimationController : IDisposable
    {
        private readonly Image _image;
        private readonly GifBolt.GifPlayer _player;
        private WriteableBitmap _writeableBitmap;
        private bool _isPlaying;
        private int _repeatCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <exception cref="InvalidOperationException">Thrown if the GIF cannot be loaded.</exception>
        public GifAnimationController(Image image, string path)
        {
            this._image = image;
            this._player = new GifBolt.GifPlayer();

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

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
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

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        public void Play()
        {
            this._player.Play();
            this._isPlaying = true;
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public void Pause()
        {
            this._player.Pause();
            this._isPlaying = false;
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            this._player.Stop();
            this._isPlaying = false;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!this._isPlaying)
            {
                return;
            }

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

            // NOTE: Frame timing is managed by the native layer via GetFrameDelayMs().
            // Current implementation uses CompositionTarget.Rendering for frame updates.
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public void Dispose()
        {
            CompositionTarget.Rendering -= this.OnRendering;
            this._player?.Dispose();
        }
    }
}
