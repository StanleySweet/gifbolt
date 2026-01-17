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
            // ...existing code...
            /// <summary>
            /// Définit ou obtient le délai minimal d'affichage d'une frame GIF (en ms).
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

            if (this._bitmap != null)
            {
                var rect = new Rect(0, 0, this.Bounds.Width, this.Bounds.Height);
                context.DrawImage(this._bitmap, rect);
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
                // Get current frame pixels
                if (this._player.TryGetFramePixelsRgba32(this._player.CurrentFrame, out byte[] rgbaPixels))
                {
                    if (rgbaPixels.Length > 0)
                    {
                        // Log first frame for debugging
                        if (this._player.CurrentFrame == 0)
                        {
                            Console.WriteLine($"[GifBolt] Frame 0: {rgbaPixels.Length} bytes, expected {this._player.Width * this._player.Height * 4}");
                            Console.WriteLine($"[GifBolt] First 16 bytes (RGBA): {string.Join(",", rgbaPixels.Take(16))}");
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

                        using (var buffer = this._bitmap.Lock())
                        {
                            Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                        }
                    }
                }

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
            var startTime = DateTime.UtcNow;
            Console.WriteLine($"[GifBolt] Starting GIF load from: {source}");
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var player = new GifPlayer();
                    // Défaut : impose un délai minimal de 100ms par frame (Chrome/macOS/ezgif)
                    player.SetMinFrameDelayMs(100);

                    if (!player.Load(source))
                    {
                        player.Dispose();
                        Console.WriteLine($"[GifBolt] Failed to load GIF");
                        return;
                    }

                    var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                    Console.WriteLine($"[GifBolt] GIF loaded in {loadTime:F0}ms - {player.Width}x{player.Height}, {player.FrameCount} frames");

// Create bitmap for display with premultiplied alpha for proper composition
                    var bitmap = new WriteableBitmap(
                        new PixelSize(player.Width, player.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    Console.WriteLine($"[GifBolt] Bitmap created: {player.Width}x{player.Height}, format=Bgra8888, alpha=Premul");

                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        this._player = player;
                        this._bitmap = bitmap;
                        this._repeatCount = this.Loop ? -1 : 1;

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
