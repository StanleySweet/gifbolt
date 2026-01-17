// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace GifBolt.Avalonia
{
    /// <summary>
    /// Attached properties for animating GIFs on standard Avalonia Image controls.
    /// Provides drop-in replacement compatibility with WpfAnimatedGif library.
    /// Cross-platform support for Windows, macOS, and Linux.
    /// </summary>
        public static class ImageBehavior
        {
            /// <summary>
            /// Définit le délai minimal d'affichage d'une frame GIF (en ms).
            /// </summary>
            public static readonly AttachedProperty<int> MinFrameDelayMsProperty =
                AvaloniaProperty.RegisterAttached<Image, int>(
                    "MinFrameDelayMs",
                    typeof(ImageBehavior),
                    defaultValue: 0);

            /// <summary>
            /// Obtient le délai minimal d'affichage d'une frame GIF (en ms).
            /// </summary>
            public static int GetMinFrameDelayMs(Image image) => image.GetValue(MinFrameDelayMsProperty);

            /// <summary>
            /// Définit le délai minimal d'affichage d'une frame GIF (en ms).
            /// </summary>
            public static void SetMinFrameDelayMs(Image image, int value) => image.SetValue(MinFrameDelayMsProperty, value);
        #region AnimatedSource (compatible WpfAnimatedGif)

        /// <summary>
        /// Defines the AnimatedSource attached property.
        /// Gets or sets the animated GIF source (Uri or string).
        /// Supports <see cref="string"/> file paths and <see cref="Uri"/> references.
        /// </summary>
        public static readonly AttachedProperty<object?> AnimatedSourceProperty =
            AvaloniaProperty.RegisterAttached<Image, object?>(
                "AnimatedSource",
                typeof(ImageBehavior),
                defaultValue: null);

        /// <summary>
        /// Gets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the animated source from.</param>
        /// <returns>The animated source value, or null if not set.</returns>
        public static object? GetAnimatedSource(Image image) => image.GetValue(AnimatedSourceProperty);

        /// <summary>
        /// Sets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the animated source on.</param>
        /// <param name="value">The animated source value (string path or Uri).</param>
        public static void SetAnimatedSource(Image image, object? value) => image.SetValue(AnimatedSourceProperty, value);

        #endregion

        #region RepeatBehavior (compatible WpfAnimatedGif)

        /// <summary>
        /// Defines the RepeatBehavior attached property.
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly AttachedProperty<string> RepeatBehaviorProperty =
            AvaloniaProperty.RegisterAttached<Image, string>(
                "RepeatBehavior",
                typeof(ImageBehavior),
                defaultValue: "0x");

        /// <summary>
        /// Gets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the repeat behavior from.</param>
        /// <returns>The repeat behavior string.</returns>
        public static string GetRepeatBehavior(Image image) => image.GetValue(RepeatBehaviorProperty);

        /// <summary>
        /// Sets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the repeat behavior on.</param>
        /// <param name="value">The repeat behavior string.</param>
        public static void SetRepeatBehavior(Image image, string value) => image.SetValue(RepeatBehaviorProperty, value);

        #endregion

        #region AutoStart

        /// <summary>
        /// Defines the AutoStart attached property.
        /// Gets or sets whether animation starts automatically upon loading.
        /// </summary>
        public static readonly AttachedProperty<bool> AutoStartProperty =
            AvaloniaProperty.RegisterAttached<Image, bool>(
                "AutoStart",
                typeof(ImageBehavior),
                defaultValue: true);

        /// <summary>
        /// Gets whether auto-start is enabled for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to query.</param>
        /// <returns>true if animation starts automatically; otherwise false.</returns>
        public static bool GetAutoStart(Image image) => image.GetValue(AutoStartProperty);

        /// <summary>
        /// Sets whether animation starts automatically for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to configure.</param>
        /// <param name="value">true to start animation automatically; otherwise false.</param>
        public static void SetAutoStart(Image image, bool value) => image.SetValue(AutoStartProperty, value);

        #endregion

        #region AnimationController (internal state)

        private static readonly AttachedProperty<GifAnimationController?> AnimationControllerProperty =
            AvaloniaProperty.RegisterAttached<Image, GifAnimationController?>(
                "AnimationController",
                typeof(ImageBehavior),
                defaultValue: null);

        private static GifAnimationController? GetAnimationController(Image image) =>
            image.GetValue(AnimationControllerProperty);

        private static void SetAnimationController(Image image, GifAnimationController? value) =>
            image.SetValue(AnimationControllerProperty, value);

        #endregion

        static ImageBehavior()
        {
            AnimatedSourceProperty.Changed.AddClassHandler<Image>(OnAnimatedSourceChanged);
            RepeatBehaviorProperty.Changed.AddClassHandler<Image>(OnRepeatBehaviorChanged);
            MinFrameDelayMsProperty.Changed.AddClassHandler<Image>(OnMinFrameDelayMsChanged);
        }

        private static void OnMinFrameDelayMsChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null && e.NewValue is int minDelayMs)
            {
                controller.SetMinFrameDelayMs(minDelayMs);
            }
        }

        private static void OnAnimatedSourceChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null)
            {
                controller.Dispose();
                SetAnimationController(image, null);
                image.DetachedFromVisualTree -= OnImageDetached;
                image.AttachedToVisualTree -= OnImageAttached;
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

            GifAnimationController? asyncController = null;
            asyncController = new GifAnimationController(image, path,
                onLoaded: () =>
                {
                    SetAnimationController(image, asyncController);

                    var repeatBehavior = GetRepeatBehavior(image);
                    asyncController.SetRepeatBehavior(repeatBehavior);

                    int minDelayMs = GetMinFrameDelayMs(image);
                    if (minDelayMs > 0)
                    {
                        asyncController.SetMinFrameDelayMs(minDelayMs);
                    }

                    if (GetAutoStart(image))
                    {
                        asyncController.Play();
                    }

                    if (image != null)
                    {
                        image.DetachedFromVisualTree += OnImageDetached;
                        image.AttachedToVisualTree += OnImageAttached;
                    }
                },
                onError: (ex) =>
                {
                    Console.WriteLine($"[GifBolt] ERROR: Failed to load GIF: {ex.Message}");
                    Console.WriteLine($"[GifBolt] Stack trace: {ex.StackTrace}");
                });
        }

        private static void OnRepeatBehaviorChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        private static void OnImageAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Image image)
            {
                var controller = GetAnimationController(image);
                if (controller != null && GetAutoStart(image))
                {
                    controller.Play();
                }
            }
        }

        private static void OnImageDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Image image)
            {
                var controller = GetAnimationController(image);
                if (controller != null)
                {
                    controller.Pause();
                }
            }
        }

        private static string? GetPathFromSource(object source)
        {
            if (source is string str)
            {
                // Handle Avalonia asset paths (avares://)
                if (str.StartsWith("/Assets/") || str.StartsWith("Assets/"))
                {
                    // Try to resolve as application asset
                    var assetUri = new Uri($"avares://GifBolt.AvaloniaApp{(str.StartsWith("/") ? "" : "/")}{str}");
                    try
                    {
                        var assets = global::Avalonia.Platform.AssetLoader.Open(assetUri);
                        // For assets, we need to copy to temp file since native decoder needs file path
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gifbolt_{Guid.NewGuid()}.gif");
                        using (var fileStream = System.IO.File.Create(tempPath))
                        {
                            assets.CopyTo(fileStream);
                        }
                        Console.WriteLine($"[GifBolt] Asset extracted to: {tempPath}");
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GifBolt] Failed to load asset: {ex.Message}");
                        return null;
                    }
                }
                // Try as absolute path or expand relative path
                if (System.IO.Path.IsPathRooted(str))
                {
                    return str;
                }
                // Try relative to current directory
                var fullPath = System.IO.Path.GetFullPath(str);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
                return str;
            }

            if (source is Uri uri)
            {
                if (uri.Scheme == "avares")
                {
                    // Extract Avalonia asset to temp file
                    try
                    {
                        var assets = global::Avalonia.Platform.AssetLoader.Open(uri);
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gifbolt_{Guid.NewGuid()}.gif");
                        using (var fileStream = System.IO.File.Create(tempPath))
                        {
                            assets.CopyTo(fileStream);
                        }
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GifBolt] Failed to load asset URI: {ex.Message}");
                        return null;
                    }
                }
                return uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
            }

            return null;
        }
    }
}

    /// <summary>
    /// Internal controller managing GIF animation on an Avalonia Image control.
    /// Handles frame decoding, timing, and pixel updates to the display.
    /// </summary>
    internal sealed class GifAnimationController : IDisposable
    {
        private readonly Image _image;
        private readonly GifBolt.GifPlayer _player;
        private WriteableBitmap? _writeableBitmap;
        private DispatcherTimer? _renderTimer;
        private bool _isPlaying;
        private int _repeatCount;
        /// <summary>
        /// Stocke le timestamp de la dernière frame rendue (pour debug timing réel).
        /// </summary>
        private DateTime _lastFrameTimestamp = default;

        public int Width => this._player.Width;
        public int Height => this._player.Height;
        public int FrameCount => this._player.FrameCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
        /// <param name="onError">Callback invoked when loading fails.</param>
        public GifAnimationController(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null)
        {
            this._image = image;
            this._player = new GifBolt.GifPlayer();
            // Défaut : impose un délai minimal de 100ms par frame (Chrome/macOS/ezgif)
            this._player.SetMinFrameDelayMs(100);

            // Chargement du GIF en tâche de fond pour éviter le freeze UI
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (!this._player.Load(path))
                    {
                        var error = new InvalidOperationException($"Failed to load GIF from path: {path}. File may not exist or be corrupt.");
                        this._player.Dispose();
                        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => onError?.Invoke(error));
                        return;
                    }

                    var wb = new WriteableBitmap(
                        new PixelSize(this._player.Width, this._player.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    // Affectation du bitmap et initialisation du timer sur le thread UI
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        this._writeableBitmap = wb;
                        this._image.Source = this._writeableBitmap;
                        this._renderTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(16)
                        };
                        this._renderTimer.Tick += this.OnRenderTick;
                        onLoaded?.Invoke();
                    });
                }
                catch (Exception ex)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => onError?.Invoke(ex));
                }
            });
        }


        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        public void SetRepeatBehavior(string repeatBehavior)
        {
            if (string.IsNullOrWhiteSpace(repeatBehavior) || repeatBehavior == "0x")
            {
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
        /// Sets the minimum frame delay (in milliseconds).
        /// </summary>
        /// <param name="minDelayMs">The minimum frame delay.</param>
        public void SetMinFrameDelayMs(int minDelayMs)
        {
            if (this._player != null)
            {
                this._player.SetMinFrameDelayMs(minDelayMs);
            }
        }

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        public void Play()
        {
            this._player.Play();
            this._isPlaying = true;
            if (this._renderTimer != null)
            {
                // Démarre le timer avec le délai de la première frame
                int initialDelay = this._player.GetFrameDelayMs(this._player.CurrentFrame);
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(initialDelay, 16));
                this._renderTimer.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public void Pause()
        {
            this._player.Pause();
            this._isPlaying = false;
            this._renderTimer?.Stop();
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            this._player.Stop();
            this._isPlaying = false;
            this._renderTimer?.Stop();
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            if (this._player == null || !this._isPlaying || this._writeableBitmap == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                int frameDelay = this._player.GetFrameDelayMs(this._player.CurrentFrame);
                var now = DateTime.UtcNow;
                double elapsedMs = 0;
                if (this._lastFrameTimestamp != default)
                {
                    elapsedMs = (now - this._lastFrameTimestamp).TotalMilliseconds;
                }
                this._lastFrameTimestamp = now;

                if (this._player.TryGetFramePixelsRgba32(this._player.CurrentFrame, out byte[] rgbaPixels))
                {
                    if (rgbaPixels.Length == 0)
                    {
                        return;
                    }

                    // Convert RGBA to BGRA with premultiplied alpha for Avalonia
                    byte[] bgraPixels = new byte[rgbaPixels.Length];
                    for (int i = 0; i < rgbaPixels.Length; i += 4)
                    {
                        byte r = rgbaPixels[i];
                        byte g = rgbaPixels[i + 1];
                        byte b = rgbaPixels[i + 2];
                        byte a = rgbaPixels[i + 3];

                        // For premultiplied alpha: if alpha=0, RGB MUST be 0 to avoid color bleed
                        if (a == 0)
                        {
                            bgraPixels[i] = 0;     // B
                            bgraPixels[i + 1] = 0; // G
                            bgraPixels[i + 2] = 0; // R
                            bgraPixels[i + 3] = 0; // A
                        }
                        else if (a < 255)
                        {
                            // Premultiply alpha for proper composition
                            float alpha = a / 255f;
                            bgraPixels[i] = (byte)(b * alpha);     // B
                            bgraPixels[i + 1] = (byte)(g * alpha); // G
                            bgraPixels[i + 2] = (byte)(r * alpha); // R
                            bgraPixels[i + 3] = a;                 // A
                        }
                        else
                        {
                            // Fully opaque, no premultiplication needed
                            bgraPixels[i] = b;     // B
                            bgraPixels[i + 1] = g; // G
                            bgraPixels[i + 2] = r; // R
                            bgraPixels[i + 3] = a; // A
                        }
                    }

                    using (var buffer = this._writeableBitmap.Lock())
                    {
                        Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                    }
                    this._image.InvalidateVisual();
                }
                else
                {
                }

                // Avancer à la frame suivante
                int nextFrame = this._player.CurrentFrame + 1;
                if (nextFrame >= this._player.FrameCount)
                {
                    if (this._repeatCount == -1 || this._repeatCount > 0)
                    {
                        nextFrame = 0;
                        if (this._repeatCount > 0)
                        {
                            this._repeatCount--;
                        }
                    }
                    else
                    {
                        this.Stop();
                        return;
                    }
                }

                // Mettre à jour la frame courante
                this._player.CurrentFrame = nextFrame;

                // Mettre à jour dynamiquement l'intervalle du timer pour la prochaine frame
                int nextDelay = this._player.GetFrameDelayMs(nextFrame);
                if (this._renderTimer != null)
                {
                    this._renderTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(nextDelay, 16));
                }
            }
            catch
            {
                // Swallow render errors
            }
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public void Dispose()
        {
            this._renderTimer?.Stop();
            this._renderTimer = null;
            this._player?.Dispose();
            this._writeableBitmap = null;
            this._lastFrameTimestamp = default;
        }
    }
