// <copyright file="GifAnimationController.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GifBolt.Avalonia
{
    /// <summary>
    /// Avalonia-specific GIF animation controller using DispatcherTimer for frame timing.
    /// </summary>
    /// <remarks>
    /// Manages GIF animation playback on Avalonia Image controls using a WriteableBitmap
    /// and DispatcherTimer for frame timing. Handles asynchronous loading and frame rendering.
    /// </remarks>
    public sealed class GifAnimationController : GifAnimationControllerBase
    {
        private readonly Image _image;
        private WriteableBitmap? _writeableBitmap;
        private DispatcherTimer? _animationTimer;
        private Stopwatch? _frameTimer;
        private bool _wasPlayingBeforeHidden;
        private ScalingFilter _scalingFilter = ScalingFilter.None;
        private byte[]? _frameBuffer;

        // FPS diagnostics
        private Stopwatch? _fpsStopwatch;
        private int _frameCount;
        private long _lastRenderTimeMs;

        /// <summary>
        /// Gets the width of the GIF in pixels.
        /// </summary>
        public int Width => this.Player?.Width ?? 0;

        /// <summary>
        /// Gets the height of the GIF in pixels.
        /// </summary>
        public int Height => this.Player?.Height ?? 0;

        /// <summary>
        /// Gets the total number of frames in the GIF.
        /// </summary>
        public int FrameCount => this.Player?.FrameCount ?? 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
        /// </summary>
        /// <remarks>
        /// Asynchronously loads the GIF file and initializes the animation timer.
        /// All UI updates are marshaled to the dispatcher thread.
        /// </remarks>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
        /// <param name="onError">Callback invoked when loading fails.</param>
        /// <param name="cacheFrames">Whether to cache decoded frames in memory for repeated playback.</param>
        /// <exception cref="ArgumentNullException">When image or path is null.</exception>
        public GifAnimationController(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null, bool cacheFrames = false)
            : base()
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this._image = image;

            // Subscribe to visibility changes for automatic pause/resume
            this._image.AttachedToVisualTree += this.OnImageAttachedToVisualTree;
            this._image.DetachedFromVisualTree += this.OnImageDetachedFromVisualTree;
            this._image.PropertyChanged += this.OnImagePropertyChanged;

            // Load the GIF asynchronously to avoid blocking the UI thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Initialize player and set min frame delay BEFORE loading
                    this.Player = new GifPlayer();
                    this.Player.SetMinFrameDelayMs(GifPlayer.DefaultMinFrameDelayMs);

                    if (!this.Player.Load(path))
                    {
                        var error = new InvalidOperationException($"Failed to load GIF from path: {path}. File may not exist or be corrupt.");
                        this.Player.Dispose();
                        this.Player = null;
                        Dispatcher.UIThread.Post(() => onError?.Invoke(error));
                        return;
                    }

                    WriteableBitmap wb;
                    byte[]? initialPixels = null;

                    // Check if scaling is disabled (None filter) or enabled
                    if (this._scalingFilter == ScalingFilter.None)
                    {
                        // Use native GIF resolution without scaling
                        wb = new WriteableBitmap(
                            new PixelSize(this.Player.Width, this.Player.Height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul);

                        if (this.Player.TryGetFramePixelsBgra32Premultiplied(0, out byte[] bgraPixels) &&
                            bgraPixels.Length > 0)
                        {
                            initialPixels = bgraPixels;
                        }
                    }
                    else
                    {
                        // Scale to display size with selected filter
                        int displayWidth = Math.Max(1, (int)this._image.Bounds.Width);
                        int displayHeight = Math.Max(1, (int)this._image.Bounds.Height);

                        if (displayWidth < 10 || displayHeight < 10)
                        {
                            displayWidth = this.Player.Width;
                            displayHeight = this.Player.Height;
                        }

                        // Get scaled frame 0 pixels on background thread
                        if (this.Player.TryGetFramePixelsBgra32PremultipliedScaled(
                            0,
                            displayWidth,
                            displayHeight,
                            out byte[] bgraPixels,
                            out int outWidth,
                            out int outHeight,
                            filter: this._scalingFilter) &&
                            bgraPixels.Length > 0)
                        {
                            wb = new WriteableBitmap(
                                new PixelSize(outWidth, outHeight),
                                new Vector(96, 96),
                                PixelFormat.Bgra8888,
                                AlphaFormat.Premul);

                            initialPixels = bgraPixels;
                        }
                        else
                        {
                            // Fallback: failed to get scaled pixels
                            var error = new InvalidOperationException("Failed to decode initial frame.");
                            Dispatcher.UIThread.Post(() => onError?.Invoke(error));
                            return;
                        }
                    }

                    // Copy pixels to bitmap on background thread
                    if (initialPixels != null)
                    {
                        using (var buffer = wb.Lock())
                        {
                            Marshal.Copy(initialPixels, 0, buffer.Address, initialPixels.Length);
                        }

                        Dispatcher.UIThread.Post(() =>
                        {
                            this._writeableBitmap = wb;
                            this._image.Source = this._writeableBitmap;
                            this._image.InvalidateVisual();

                            this._animationTimer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs),
                            };
                            this._animationTimer.Tick += this.OnRenderTick;

                            onLoaded?.Invoke();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => onError?.Invoke(ex));
                }
            });
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <remarks>
        /// Parses the repeat behavior string and updates the repeat strategy and count accordingly.
        /// Valid formats: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </remarks>
        /// <param name="repeatBehavior">The repeat behavior string.</param>
        public override void SetRepeatBehavior(string repeatBehavior)
        {
            if (this.Player == null)
            {
                return;
            }

            this.RepeatCount = this.Player.ComputeRepeatCount(repeatBehavior);
        }

        /// <summary>
        /// Sets the minimum frame delay (in milliseconds).
        /// </summary>
        /// <remarks>
        /// Frames will not be rendered faster than this interval, even if the GIF
        /// specifies shorter frame delays.
        /// </remarks>
        /// <param name="minDelayMs">The minimum frame delay in milliseconds.</param>
        public void SetMinFrameDelayMs(int minDelayMs)
        {
            if (this.Player != null)
            {
                this.Player.SetMinFrameDelayMs(minDelayMs);
            }
        }

        /// <summary>
        /// Sets the scaling filter used when resizing frames.
        /// </summary>
        /// <remarks>
        /// When the filter changes, reinitializes the rendering pipeline and re-renders the current frame.
        /// </remarks>
        /// <param name="filter">The scaling filter (Nearest, Bilinear, Bicubic, Lanczos).</param>
        public override void SetScalingFilter(ScalingFilter filter)
        {
            if (this._scalingFilter == filter)
            {
                return; // No change
            }

            // Pause during filter change to avoid race conditions with the animation timer
            bool wasPlaying = this.IsPlaying;
            if (wasPlaying)
            {
                this.Pause();
            }

            this._scalingFilter = filter;

            // If we have a valid player and bitmap, reinitialize for the new filter
            if (this.Player != null && this._writeableBitmap != null && this._image != null)
            {
                try
                {
                    int nativeWidth = this.Player.Width;
                    int nativeHeight = this.Player.Height;

                    // Get the current display size
                    int displayWidth = Math.Max(1, (int)this._image.Bounds.Width);
                    int displayHeight = Math.Max(1, (int)this._image.Bounds.Height);

                    if (displayWidth < 10 || displayHeight < 10)
                    {
                        displayWidth = nativeWidth;
                        displayHeight = nativeHeight;
                    }

                    // Determine target bitmap dimensions based on filter
                    int targetWidth = filter == ScalingFilter.None ? nativeWidth : displayWidth;
                    int targetHeight = filter == ScalingFilter.None ? nativeHeight : displayHeight;

                    // Recreate bitmap if dimensions changed
                    if (this._writeableBitmap.PixelSize.Width != targetWidth ||
                        this._writeableBitmap.PixelSize.Height != targetHeight)
                    {
                        this._writeableBitmap = new WriteableBitmap(
                            new PixelSize(targetWidth, targetHeight),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul);

                        this._image.Source = this._writeableBitmap;
                    }

                    // Re-render the current frame with the new filter
                    this.RenderFrame(this.Player.CurrentFrame);
                }
                catch
                {
                    // Suppress errors during filter change
                }
            }

            // Resume playback if it was playing
            if (wasPlaying)
            {
                this.Play();
            }
        }

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        /// <remarks>
        /// Starts the animation timer and begins rendering frames based on the GIF metadata delays.
        /// </remarks>
        public override void Play()
        {
            if (this.Player == null)
            {
                return;
            }

            this.Player.Play();
            this.IsPlaying = true;

            // Start high-precision frame timer
            if (this._frameTimer == null)
            {
                this._frameTimer = new Stopwatch();
            }
            this._frameTimer.Restart();

            // Initialize FPS stopwatch
            if (this._fpsStopwatch == null)
            {
                this._fpsStopwatch = new Stopwatch();
            }
            this._fpsStopwatch.Restart();
            this._frameCount = 0;
            this._lastRenderTimeMs = 0;

            if (this._writeableBitmap != null)
            {
                this.RenderFrame(this.Player.CurrentFrame);
            }

            if (this._animationTimer != null)
            {
                // Use a fixed, fast timer for smooth rendering
                // Frame advancement is calculated based on actual elapsed time, not timer interval
                this._animationTimer.Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs);
                this._animationTimer.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        /// <remarks>
        /// Stops the animation timer but preserves the current frame position.
        /// </remarks>
        public override void Pause()
        {
            this.Player?.Pause();
            this.IsPlaying = false;
            this._animationTimer?.Stop();
            this._frameTimer?.Stop();
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        /// <remarks>
        /// Completely halts animation and resets the player to the beginning.
        /// </remarks>
        public override void Stop()
        {
            this.Player?.Stop();
            this.IsPlaying = false;
            this._animationTimer?.Stop();
        }

        /// <summary>
        /// Renders a specific frame to the WriteableBitmap.
        /// </summary>
        /// <remarks>
        /// Thread-safe method that locks the bitmap, copies pixel data, and invalidates
        /// the visual to trigger a redraw.
        /// </remarks>
        /// <param name="frameIndex">The index of the frame to render.</param>
        private void RenderFrame(int frameIndex)
        {
            if (this.Player == null || this._writeableBitmap == null)
            {
                return;
            }

            try
            {
                byte[] bgraPixels;
                bool success;

                // Check if scaling is disabled or enabled
                if (this._scalingFilter == ScalingFilter.None)
                {
                    // Use native resolution without scaling
                    success = this.Player.TryGetFramePixelsBgra32Premultiplied(frameIndex, out bgraPixels);
                }
                else
                {
                    // Scale to display size with selected filter
                    int displayWidth = this._writeableBitmap.PixelSize.Width;
                    int displayHeight = this._writeableBitmap.PixelSize.Height;

                    success = this.Player.TryGetFramePixelsBgra32PremultipliedScaled(
                        frameIndex,
                        displayWidth,
                        displayHeight,
                        out bgraPixels,
                        out int outWidth,
                        out int outHeight,
                        filter: this._scalingFilter);
                }

                if (success)
                {
                    if (bgraPixels.Length == 0)
                    {
                        return;
                    }

                    using (var buffer = this._writeableBitmap.Lock())
                    {
                        Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                    }

                    this._image.InvalidateVisual();
                }
            }
            catch
            {
                // Suppress render errors to avoid interrupting animation loop
            }
        }

        /// <summary>
        /// Handles the animation timer tick event.
        /// </summary>
        /// <remarks>
        /// Called at regular intervals to advance frames and update the display.
        /// Respects frame delays and repeat behavior.
        /// </remarks>
        private void OnRenderTick(object? sender, EventArgs e)
        {
            if (this.Player == null || !this.IsPlaying || this._writeableBitmap == null || this._animationTimer == null || this._frameTimer == null)
            {
                return;
            }

            try
            {
                // Get the frame delay for the current frame
                int frameDelayMs = this.Player.GetFrameDelayMs(this.Player.CurrentFrame);
                if (frameDelayMs <= 0)
                {
                    // Safety: if delay is 0 or negative, use the configured default
                    frameDelayMs = GifPlayer.DefaultMinFrameDelayMs;
                }

                int minFrameDelayMs = this.Player.GetMinFrameDelayMs();
                long elapsedMs = this._frameTimer.ElapsedMilliseconds;

                // Only advance frame if enough time has elapsed for the current frame
                if (elapsedMs >= frameDelayMs)
                {
                    // Consolidated frame advancement with timing: handles frame advancement,
                    // effective delay computation, and repeat count management in a single C++ call
                    var advanceResult = GifPlayer.AdvanceFrameTimed(
                        this.Player.CurrentFrame,
                        this.Player.FrameCount,
                        this.RepeatCount,
                        frameDelayMs,
                        minFrameDelayMs);

                    if (advanceResult.IsComplete != 0)
                    {
                        this.Stop();
                        return;
                    }

                    // Reset canvas state when looping back to frame 0
                    if (advanceResult.NextFrame == 0 && this.Player.CurrentFrame > 0)
                    {
                        this.Player.ResetCanvas();
                    }

                    this.Player.CurrentFrame = advanceResult.NextFrame;
                    this.RepeatCount = advanceResult.UpdatedRepeatCount;
                    this._frameTimer.Restart();

                    // Render only when frame changes for better performance
                    var renderStart = Stopwatch.GetTimestamp();
                    this.RenderFrame(this.Player.CurrentFrame);
                    var renderTimeMs = (Stopwatch.GetTimestamp() - renderStart) / (double)Stopwatch.Frequency * 1000.0;

                    // FPS tracking
                    this._frameCount++;
                    this._lastRenderTimeMs = this._fpsStopwatch?.ElapsedMilliseconds ?? 0;

                    if (this._fpsStopwatch != null && this._fpsStopwatch.ElapsedMilliseconds > 1000)
                    {
                        var fps = this._frameCount * 1000.0 / this._fpsStopwatch.ElapsedMilliseconds;
                        var fpsText = $"FPS: {fps:F1} | Render: {renderTimeMs:F2}ms | Delay: {advanceResult.EffectiveDelayMs}ms";

                        // Update FPS on the Image control
                        AnimationBehavior.SetFpsText(this._image, fpsText);

                        this._fpsStopwatch.Restart();
                        this._frameCount = 0;
                    }
                }
            }
            catch
            {
                // Suppress render errors to avoid interrupting animation loop
            }
        }

        /// <summary>
        /// Handles the Image control being attached to the visual tree.
        /// Resumes animation if it was playing before detachment.
        /// </summary>
        /// <param name="sender">The Image control.</param>
        /// <param name="e">Event arguments.</param>
        private void OnImageAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            // Check if we should resume playback
            if (this._wasPlayingBeforeHidden && !this.IsPlaying)
            {
                this._wasPlayingBeforeHidden = false;
                this.Play();
                return;
            }

            // Also check visibility in case it wasn't already triggered
            if (this._image.IsVisible && this._wasPlayingBeforeHidden && !this.IsPlaying)
            {
                this._wasPlayingBeforeHidden = false;
                this.Play();
            }
        }

        /// <summary>
        /// Handles the Image control being detached from the visual tree.
        /// Pauses animation to save resources when not visible.
        /// </summary>
        /// <param name="sender">The Image control.</param>
        /// <param name="e">Event arguments.</param>
        private void OnImageDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.IsPlaying)
            {
                this._wasPlayingBeforeHidden = true;
                this.Pause();
            }
        }

        /// <summary>
        /// Handles property changes on the Image control to monitor IsVisible.
        /// Pauses/resumes animation based on visibility to save resources.
        /// </summary>
        /// <param name="sender">The Image control.</param>
        /// <param name="e">Property changed event arguments.</param>
        private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name != nameof(this._image.IsVisible))
            {
                return;
            }

            bool isVisible = this._image.IsVisible;

            if (!isVisible && this.IsPlaying)
            {
                // Image became hidden while playing - pause and remember state
                this._wasPlayingBeforeHidden = true;
                this.Pause();
            }
            else if (isVisible && this._wasPlayingBeforeHidden)
            {
                // Image became visible again and was playing before - resume
                this._wasPlayingBeforeHidden = false;
                this.Play();
            }
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        /// <remarks>
        /// Stops the animation timer and disposes the underlying player.
        /// </remarks>
        public override void Dispose()
        {
            // Unsubscribe from visibility events
            this._image.AttachedToVisualTree -= this.OnImageAttachedToVisualTree;
            this._image.DetachedFromVisualTree -= this.OnImageDetachedFromVisualTree;
            this._image.PropertyChanged -= this.OnImagePropertyChanged;

            this._animationTimer?.Stop();
            this._animationTimer = null;

            base.Dispose();
        }
    }
}
