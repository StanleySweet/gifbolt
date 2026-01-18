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
using GifBolt;

namespace GifBolt.Avalonia
{
    /// <summary>
    /// Avalonia control for displaying animated GIFs using GifBolt.
    /// Exposes styled properties: Source, AutoStart, Loop.
    /// Cross-platform support for Windows (D3D11), macOS (Metal), and Linux (future OpenGL).
    /// </summary>
    public sealed class GifBoltControl : Control
    {
        /// <summary>
        /// Gets or sets the minimum frame delay in milliseconds for GIF playback.
        /// </summary>
        public int MinFrameDelayMs
        {
            get => this._player?.GetMinFrameDelayMs() ?? 0;
            set
            {
                if (this._player != null)
                {
                    this._player.SetMinFrameDelayMs(value);
                }
            }
        }
        private GifPlayer? _player;
        private WriteableBitmap? _bitmap;
        private bool _isLoaded;
        private DispatcherTimer? _renderTimer;
        private bool _isPlaying;
        private int _repeatCount = -1;

        #region Styled Properties
        /// <summary>
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> SourceProperty =
            AvaloniaProperty.Register<GifBoltControl, string?>(
                nameof(Source),
                defaultValue: null);

        /// <summary>
        /// Gets or sets the path or URI to the GIF image source.
        /// Setting this property automatically triggers loading if the control is loaded.
        /// </summary>
        public string? Source
        {
            get => this.GetValue(SourceProperty);
            set => this.SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AutoStart"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoStartProperty =
            AvaloniaProperty.Register<GifBoltControl, bool>(
                nameof(AutoStart),
                defaultValue: true);

        /// <summary>
        /// Gets or sets a value indicating whether playback starts automatically when a GIF is loaded.
        /// </summary>
        public bool AutoStart
        {
            get => this.GetValue(AutoStartProperty);
            set => this.SetValue(AutoStartProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Loop"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> LoopProperty =
            AvaloniaProperty.Register<GifBoltControl, bool>(
                nameof(Loop),
                defaultValue: true);

        /// <summary>
        /// Gets or sets a value indicating whether the GIF loops indefinitely.
        /// </summary>
        public bool Loop
        {
            get => this.GetValue(LoopProperty);
            set => this.SetValue(LoopProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Stretch"/> property.
        /// </summary>
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<GifBoltControl, Stretch>(
                nameof(Stretch),
                defaultValue: Stretch.Fill);

        /// <summary>
        /// Gets or sets how the GIF is stretched to fill the control bounds.
        /// </summary>
        public Stretch Stretch
        {
            get => this.GetValue(StretchProperty);
            set => this.SetValue(StretchProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ScalingFilter"/> property.
        /// </summary>
        public static readonly StyledProperty<GifBolt.Internal.ScalingFilter> ScalingFilterProperty =
            AvaloniaProperty.Register<GifBoltControl, GifBolt.Internal.ScalingFilter>(
                nameof(ScalingFilter),
                defaultValue: GifBolt.Internal.ScalingFilter.Lanczos);

        /// <summary>
        /// Gets or sets the scaling filter used when resizing GIF frames (Nearest, Bilinear, Bicubic, Lanczos).
        /// </summary>
        public GifBolt.Internal.ScalingFilter ScalingFilter
        {
            get => this.GetValue(ScalingFilterProperty);
            set => this.SetValue(ScalingFilterProperty, value);
        }
        #endregion

        static GifBoltControl()
        {
            SourceProperty.Changed.AddClassHandler<GifBoltControl>((control, e) =>
            {
                control.LoadGifIfReady();
            });

            LoopProperty.Changed.AddClassHandler<GifBoltControl>((control, e) =>
            {
                if (control._player != null)
                {
                    control._repeatCount = ((bool)e.NewValue!) ? -1 : 1;
                }
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifBoltControl"/> class.
        /// </summary>
        public GifBoltControl()
        {
            this.AttachedToVisualTree += this.OnAttachedToVisualTree;
            this.DetachedFromVisualTree += this.OnDetachedFromVisualTree;

            this._renderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            this._renderTimer.Tick += this.OnRenderTick;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            this._isLoaded = true;
            this.LoadGifIfReady();
            this._renderTimer?.Start();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            this._isLoaded = false;
            this._renderTimer?.Stop();
            this._player?.Dispose();
            this._player = null;
            this._bitmap = null;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (this._bitmap != null && this._player != null)
            {
                var sourceSize = new Size(this._bitmap.PixelSize.Width, this._bitmap.PixelSize.Height);
                var viewportSize = this.Bounds.Size;
                var destRect = this.CalculateStretchRect(sourceSize, viewportSize);

                // If we're scaling, request the scaled frame from C++
                var targetWidth = (int)destRect.Width;
                var targetHeight = (int)destRect.Height;

                if (targetWidth > 0 && targetHeight > 0 &&
                    (targetWidth != this._bitmap.PixelSize.Width || targetHeight != this._bitmap.PixelSize.Height))
                {
                    // Request scaled frame from C++ with selected filter
                    if (this._player.TryGetFramePixelsBgra32PremultipliedScaled(
                        this._player.CurrentFrame, targetWidth, targetHeight,
                        out byte[] scaledPixels, out int actualWidth, out int actualHeight,
                        this.ScalingFilter))
                    {
                        if (scaledPixels.Length > 0 && actualWidth > 0 && actualHeight > 0)
                        {
                            // Create temporary bitmap for scaled pixels
                            var scaledBitmap = new WriteableBitmap(
                                new PixelSize(actualWidth, actualHeight),
                                new Vector(96, 96),
                                PixelFormat.Bgra8888,
                                AlphaFormat.Premul);

                            using (var buffer = scaledBitmap.Lock())
                            {
                                Marshal.Copy(scaledPixels, 0, buffer.Address, scaledPixels.Length);
                            }

                            // Draw scaled bitmap (no further scaling needed)
                            context.DrawImage(scaledBitmap, new Rect(destRect.X, destRect.Y, actualWidth, actualHeight));
                            return;
                        }
                    }
                }

                // Fallback: draw original bitmap with Avalonia scaling
                context.DrawImage(this._bitmap, destRect);
            }
        }

        /// <summary>
        /// Calculates the destination rectangle based on the Stretch mode.
        /// </summary>
        /// <param name="sourceSize">The size of the source image.</param>
        /// <param name="viewportSize">The size of the viewport.</param>
        /// <returns>The calculated destination rectangle.</returns>
        private Rect CalculateStretchRect(Size sourceSize, Size viewportSize)
        {
            if (sourceSize.Width == 0 || sourceSize.Height == 0)
            {
                return new Rect(0, 0, viewportSize.Width, viewportSize.Height);
            }

            switch (this.Stretch)
            {
                case Stretch.None:
                    return new Rect(0, 0, sourceSize.Width, sourceSize.Height);

                case Stretch.Fill:
                    return new Rect(0, 0, viewportSize.Width, viewportSize.Height);

                case Stretch.Uniform:
                    {
                        var scale = Math.Min(viewportSize.Width / sourceSize.Width, viewportSize.Height / sourceSize.Height);
                        var scaledWidth = sourceSize.Width * scale;
                        var scaledHeight = sourceSize.Height * scale;
                        var x = (viewportSize.Width - scaledWidth) / 2;
                        var y = (viewportSize.Height - scaledHeight) / 2;
                        return new Rect(x, y, scaledWidth, scaledHeight);
                    }

                case Stretch.UniformToFill:
                    {
                        var scale = Math.Max(viewportSize.Width / sourceSize.Width, viewportSize.Height / sourceSize.Height);
                        var scaledWidth = sourceSize.Width * scale;
                        var scaledHeight = sourceSize.Height * scale;
                        var x = (viewportSize.Width - scaledWidth) / 2;
                        var y = (viewportSize.Height - scaledHeight) / 2;
                        return new Rect(x, y, scaledWidth, scaledHeight);
                    }

                default:
                    return new Rect(0, 0, viewportSize.Width, viewportSize.Height);
            }
        }

        private void RenderCurrentFrame()
        {
            if (this._player == null || this._bitmap == null)
            {
                return;
            }

            // Get current frame pixels directly in BGRA format with premultiplied alpha (C++ optimized)
            if (this._player.TryGetFramePixelsBgra32Premultiplied(this._player.CurrentFrame, out byte[] bgraPixels))
            {
                if (bgraPixels.Length > 0)
                {
                    // Direct copy - no conversion needed as C++ already did the work
                    using (var buffer = this._bitmap.Lock())
                    {
                        Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                    }
                }
            }
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            if (this._player == null || !this._isPlaying || this._bitmap == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                this.RenderCurrentFrame();

                // Advance to next frame
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

                this._player.CurrentFrame = nextFrame;

                // Update timer interval dynamically for next frame
                int nextDelay = this._player.GetFrameDelayMs(nextFrame);
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(nextDelay, 16));

                this.InvalidateVisual();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Render error: {ex.Message}");
            }
        }

        private void LoadGifPlayer()
        {
            if (this._player != null)
            {
                this._player.Dispose();
                this._player = null;
            }

            if (string.IsNullOrWhiteSpace(this.Source))
            {
                this._bitmap = null;
                return;
            }

            var source = this.Source;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var player = new GifPlayer();
                    // Default: enforce minimum delay per Chrome/macOS/ezgif standard
                    player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                    if (!player.Load(source))
                    {
                        player.Dispose();
                        return;
                    }

                    // Create bitmap for display with premultiplied alpha for proper composition
                    var bitmap = new WriteableBitmap(
                        new PixelSize(player.Width, player.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        this._player = player;
                        this._bitmap = bitmap;
                        this._repeatCount = this.Loop ? -1 : 1;

                        // Render first frame immediately
                        this.RenderCurrentFrame();
                        this.InvalidateVisual();

                        if (this.AutoStart)
                        {
                            this.Play();
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load GIF: {ex.Message}");
                }
            });
        }

        private void LoadGifIfReady()
        {
            if (!this._isLoaded) return;
            if (Design.IsDesignMode) return;
            if (string.IsNullOrWhiteSpace(this.Source)) return;

            this.LoadGifPlayer();
        }

        #region Public Control Methods
        /// <summary>
        /// Starts playback of the GIF.
        /// </summary>
        public void Play()
        {
            if (this._player != null && !this._isPlaying)
            {
                this._player.Play();
                this._isPlaying = true;
                this._renderTimer?.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the GIF.
        /// </summary>
        public void Pause()
        {
            if (this._player != null && this._isPlaying)
            {
                this._player.Pause();
                this._isPlaying = false;
                this._renderTimer?.Stop();
            }
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            if (this._player != null)
            {
                this._player.Stop();
                this._isPlaying = false;
                this._renderTimer?.Stop();
                this._player.CurrentFrame = 0;
                this.InvalidateVisual();
            }
        }

        /// <summary>
        /// Loads a new GIF from the specified file path.
        /// </summary>
        /// <param name="path">The file path to the GIF image.</param>
        public void LoadGif(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            this.Source = path;
        }
        #endregion
    }
}
