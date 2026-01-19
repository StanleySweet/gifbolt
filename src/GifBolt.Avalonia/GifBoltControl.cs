// <copyright file="GifBoltControl.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Diagnostics;
using System.IO;
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
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> SourceProperty =
            AvaloniaProperty.Register<GifBoltControl, string?>(
                nameof(Source),
                defaultValue: null);

        /// <summary>
        /// Defines the <see cref="AutoStart"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoStartProperty =
            AvaloniaProperty.Register<GifBoltControl, bool>(
                nameof(AutoStart),
                defaultValue: true);

        /// <summary>
        /// Defines the <see cref="Loop"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> LoopProperty =
            AvaloniaProperty.Register<GifBoltControl, bool>(
                nameof(Loop),
                defaultValue: true);

        /// <summary>
        /// Defines the <see cref="Stretch"/> property.
        /// </summary>
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<GifBoltControl, Stretch>(
                nameof(Stretch),
                defaultValue: Stretch.Fill);

        /// <summary>
        /// Defines the <see cref="ScalingFilter"/> property.
        /// </summary>
        public static readonly StyledProperty<GifBolt.Internal.ScalingFilter> ScalingFilterProperty =
            AvaloniaProperty.Register<GifBoltControl, GifBolt.Internal.ScalingFilter>(
                nameof(ScalingFilter),
                defaultValue: GifBolt.Internal.ScalingFilter.Lanczos);

        /// <summary>
        /// Defines the <see cref="DiagnosticsEnabled"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> DiagnosticsEnabledProperty =
            AvaloniaProperty.Register<GifBoltControl, bool>(
                nameof(DiagnosticsEnabled),
                defaultValue: false);

        private GifPlayer? _player;
        private WriteableBitmap? _bitmap;
        private bool _isLoaded;
        private DispatcherTimer? _renderTimer;
        private bool _isPlaying;
        private int _repeatCount = -1;
        private double _cachedViewportWidth = -1;
        private double _cachedViewportHeight = -1;
        private bool _hasRenderedOnce;
        private string? _tempFilePath;
        private byte[]? _sourceBytes;

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
        /// Gets or sets a value indicating whether playback starts automatically when a GIF is loaded.
        /// </summary>
        public bool AutoStart
        {
            get => this.GetValue(AutoStartProperty);
            set => this.SetValue(AutoStartProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the GIF loops indefinitely.
        /// </summary>
        public bool Loop
        {
            get => this.GetValue(LoopProperty);
            set => this.SetValue(LoopProperty, value);
        }

        /// <summary>
        /// Gets or sets how the GIF is stretched to fill the control bounds.
        /// </summary>
        public Stretch Stretch
        {
            get => this.GetValue(StretchProperty);
            set => this.SetValue(StretchProperty, value);
        }

        /// <summary>
        /// Gets or sets the scaling filter used when resizing GIF frames (Nearest, Bilinear, Bicubic, Lanczos).
        /// </summary>
        public GifBolt.Internal.ScalingFilter ScalingFilter
        {
            get => this.GetValue(ScalingFilterProperty);
            set => this.SetValue(ScalingFilterProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether diagnostics logs are enabled.
        /// When enabled, the control prints timing information to help diagnose delays.
        /// </summary>
        public bool DiagnosticsEnabled
        {
            get => this.GetValue(DiagnosticsEnabledProperty);
            set => this.SetValue(DiagnosticsEnabledProperty, value);
        }

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
                Interval = TimeSpan.FromMilliseconds(16),
            };
            this._renderTimer.Tick += this.OnRenderTick;
        }

        /// <summary>
        /// Starts playback of the GIF.
        /// </summary>
        public void Play()
        {
            if (this._player != null && !this._isPlaying)
            {
                this._player.Play();
                this._isPlaying = true;

                // Render first frame immediately to avoid delay on Play
                this.RenderCurrentFrame();
                this.InvalidateVisual();

                // Start timer; interval will update on first tick.
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            this._sourceBytes = null;
            this.Source = path;
        }

        /// <summary>
        /// Loads a new GIF from an in-memory buffer.
        /// </summary>
        /// <param name="data">The GIF data buffer.</param>
        public void LoadGif(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            this._sourceBytes = data;
            this.Source = null;
            this.LoadGifIfReady();
        }

        /// <summary>
        /// Renders the current frame of the animated GIF using Avalonia's drawing context.
        /// </summary>
        /// <param name="context">The drawing context for rendering.</param>
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (this._bitmap != null && this._player != null)
            {
                if (!this._hasRenderedOnce)
                {
                    this._hasRenderedOnce = true;
                }

                var sourceSize = new Size(this._bitmap.PixelSize.Width, this._bitmap.PixelSize.Height);
                var viewportSize = this.Bounds.Size;

                // Recalculate rect only if size changed
                Rect destRect;
                if (this._cachedViewportWidth != viewportSize.Width || this._cachedViewportHeight != viewportSize.Height)
                {
                    this._cachedViewportWidth = viewportSize.Width;
                    this._cachedViewportHeight = viewportSize.Height;
                    destRect = this.CalculateStretchRect(sourceSize, viewportSize);
                }
                else
                {
                    // Use cached calculation - just recreate it quickly
                    destRect = this.CalculateStretchRect(sourceSize, viewportSize);
                }

                // Render bitmap with Avalonia's smooth scaling/interpolation
                context.DrawImage(this._bitmap, destRect);
            }
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
            this.CleanupTempFile();
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

            this.CleanupTempFile();

            byte[]? sourceBytes = this._sourceBytes;
            string? resolvedPath = null;

            if (sourceBytes == null)
            {
                if (string.IsNullOrWhiteSpace(this.Source))
                {
                    this._bitmap = null;
                    return;
                }

                resolvedPath = this.ResolveSourceToFilePath(this.Source);
                sourceBytes = this._sourceBytes;  // ResolveSourceToFilePath might have set it for assets

                if (sourceBytes == null && (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)))
                {
                    Debug.WriteLine($"GifBoltControl: could not resolve source '{this.Source}'.");
                    return;
                }
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var t0 = DateTime.UtcNow;
                    var player = new GifPlayer();

                    // Default: enforce minimum delay per Chrome/macOS/ezgif standard
                    player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                    bool loaded = sourceBytes != null
                        ? player.Load(sourceBytes)
                        : player.Load(resolvedPath!);

                    if (!loaded)
                    {
                        player.Dispose();
                        return;
                    }

                    var tLoad = DateTime.UtcNow;

                    // Create bitmap for display with premultiplied alpha for proper composition
                    var bitmap = new WriteableBitmap(
                        new PixelSize(player.Width, player.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    // Render first frame on background thread to avoid delay
                    if (player.TryGetFramePixelsBgra32Premultiplied(0, out byte[] bgraPixels) && bgraPixels.Length > 0)
                    {
                        using (var buffer = bitmap.Lock())
                        {
                            Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                        }

                    }

                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var tUI = DateTime.UtcNow;
                        this._player = player;
                        this._bitmap = bitmap;
                        this._repeatCount = this.Loop ? -1 : 1;

                        // Render the current frame immediately on the UI thread
                        // to avoid waiting for the first timer tick or GIF delay.
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
            if (!this._isLoaded)
            {
                return;
            }

            if (Design.IsDesignMode)
            {
                return;
            }

            bool hasBytes = this._sourceBytes != null && this._sourceBytes.Length > 0;
            if (string.IsNullOrWhiteSpace(this.Source) && !hasBytes)
            {
                return;
            }

            this.LoadGifPlayer();
        }

        private string? ResolveSourceToFilePath(string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(source))
                {
                    return null;
                }

                if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri))
                {
                    switch (uri.Scheme.ToLowerInvariant())
                    {
                        case "file":
                            return uri.LocalPath;

                        case "pack":
                        case "avares":
                        {
                            var assetUri = uri.Scheme == "pack"
                                ? this.ConvertPackToAvaresUri(uri)
                                : uri;

                            if (assetUri != null && AssetLoader.Exists(assetUri))
                            {
                                // Load asset into memory instead of temp file
                                try
                                {
                                    using (var stream = AssetLoader.Open(assetUri))
                                    using (var memory = new MemoryStream())
                                    {
                                        stream.CopyTo(memory);
                                        this._sourceBytes = memory.ToArray();
                                    }
                                    return null;  // Signal it's in-memory
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Failed to load asset '{assetUri}': {ex.Message}");
                                    this._sourceBytes = null;
                                    return null;
                                }
                            }

                            Debug.WriteLine($"Failed to resolve source '{source}'.");
                            return null;
                        }
                    }
                }

                return Path.GetFullPath(source);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to resolve source '{source}': {ex.Message}");
                return null;
            }
        }

        private Uri? ConvertPackToAvaresUri(Uri packUri)
        {
            if (!string.Equals(packUri.Scheme, "pack", StringComparison.OrdinalIgnoreCase))
            {
                return packUri;
            }

            string assemblyName = this.GetType().Assembly.GetName().Name ?? "GifBolt.Avalonia";
            string assetPath = packUri.AbsolutePath;

            if (!assetPath.StartsWith('/'))
            {
                assetPath = "/" + assetPath;
            }

            return new Uri($"avares://{assemblyName}{assetPath}");
        }

        private string ExtractAssetToTempFile(Uri assetUri)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "gifbolt");
            Directory.CreateDirectory(tempDirectory);

            string extension = Path.GetExtension(assetUri.AbsolutePath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".gif";
            }

            string tempFile = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + extension);

            using (var stream = AssetLoader.Open(assetUri))
            using (var fileStream = File.Create(tempFile))
            {
                stream.CopyTo(fileStream);
            }

            return tempFile;
        }

        private void CleanupTempFile()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(this._tempFilePath) && File.Exists(this._tempFilePath))
                {
                    File.Delete(this._tempFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete temp file '{this._tempFilePath}': {ex.Message}");
            }
            finally
            {
                this._tempFilePath = null;
            }
        }
    }
}
