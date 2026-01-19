// <copyright file="GifAnimationController.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
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
    /// Avalonia-specific GIF animation controller using DispatcherTimer for frame timing.
    /// </summary>
    /// <remarks>
    /// Manages GIF animation playback on Avalonia Image controls using a WriteableBitmap
    /// and DispatcherTimer for frame timing. Handles asynchronous loading and frame rendering.
    /// </remarks>
    internal sealed class GifAnimationController : GifAnimationControllerBase
    {
        private readonly Image _image;
        private WriteableBitmap? _writeableBitmap;
        private DispatcherTimer? _animationTimer;
        private DateTime _frameStartTime;

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
        /// <exception cref="ArgumentNullException">When image or path is null.</exception>
        public GifAnimationController(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null)
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

            // Load the GIF asynchronously to avoid blocking the UI thread
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Initialize player property
                    this.Player = new GifPlayer();
                    // this.Player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                    if (!this.Player.Load(path))
                    {
                        var error = new InvalidOperationException($"Failed to load GIF from path: {path}. File may not exist or be corrupt.");
                        this.Player.Dispose();
                        this.Player = null;
                        Dispatcher.UIThread.Post(() => onError?.Invoke(error));
                        return;
                    }

                    var wb = new WriteableBitmap(
                        new PixelSize(this.Player.Width, this.Player.Height),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);

                    // Get frame 0 pixels on background thread to avoid UI blocking
                    if (this.Player.TryGetFramePixelsBgra32Premultiplied(0, out byte[] bgraPixels) && bgraPixels.Length > 0)
                    {
                        // Copy pixels to bitmap on background thread
                        using (var buffer = wb.Lock())
                        {
                            Marshal.Copy(bgraPixels, 0, buffer.Address, bgraPixels.Length);
                        }
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        this._writeableBitmap = wb;
                        this._image.Source = this._writeableBitmap;
                        this._image.InvalidateVisual();

                        this._animationTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(16),
                        };
                        this._animationTimer.Tick += this.OnRenderTick;
                        onLoaded?.Invoke();
                    });
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
        /// Parses the repeat behavior string and updates the repeat count accordingly.
        /// Valid formats: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </remarks>
        /// <param name="repeatBehavior">The repeat behavior string.</param>
        public override void SetRepeatBehavior(string repeatBehavior)
        {
            this.RepeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this.Player?.IsLooping ?? true);
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
        /// Starts playback of the animation.
        /// </summary>
        /// <remarks>
        /// Immediately renders the current frame and starts the animation timer.
        /// The timer interval is adjusted based on the first frame's delay.
        /// </remarks>
        public override void Play()
        {
            this.Player?.Play();
            this.IsPlaying = true;
            this._frameStartTime = DateTime.UtcNow;

            // Render the current frame immediately to avoid delay
            if (this._writeableBitmap != null && this.Player != null)
            {
                this.RenderFrame(this.Player.CurrentFrame);
            }

            if (this._animationTimer != null)
            {
                // Always use 16ms fixed interval - frame advancement is time-based, not timer-based
                this._animationTimer.Interval = TimeSpan.FromMilliseconds(FrameTimingHelper.MinRenderIntervalMs);
                this._animationTimer.Start();

                // DEBUG
                try
                {
                    System.IO.File.AppendAllText("/tmp/gifbolt_timing.log", $"[START] Animation started with 16ms timer\n");
                }
                catch { }
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
                if (this.Player.TryGetFramePixelsBgra32Premultiplied(frameIndex, out byte[] bgraPixels))
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
            if (this.Player == null || !this.IsPlaying || this._writeableBitmap == null || this._animationTimer == null)
            {
                return;
            }

            try
            {
                // Get the frame delay for the current frame and clamp to minimum
                int rawFrameDelayMs = this.Player.GetFrameDelayMs(this.Player.CurrentFrame);
                int frameDelayMs = Math.Max(rawFrameDelayMs, FrameTimingHelper.DefaultMinFrameDelayMs);
                long elapsedMs = (long)(DateTime.UtcNow - this._frameStartTime).TotalMilliseconds;

                // DEBUG: Log every tick to understand timing
                if (this.Player.CurrentFrame == 0)
                {
                    try
                    {
                        System.IO.File.AppendAllText("/tmp/gifbolt_timing.log", $"[TICK] Frame 0: raw={rawFrameDelayMs}ms, clamped={frameDelayMs}ms, elapsed={elapsedMs}ms\n");
                    }
                    catch { }
                }

                // Only advance frame if enough time has elapsed for the current frame
                if (elapsedMs >= frameDelayMs)
                {
                    // Advance to the next frame using shared helper
                    var advanceResult = FrameAdvanceHelper.AdvanceFrame(
                        this.Player.CurrentFrame,
                        this.Player.FrameCount,
                        this.RepeatCount);

                    if (advanceResult.IsComplete)
                    {
                        this.Stop();
                        return;
                    }

                    // Update the current frame and repeat count
                    this.Player.CurrentFrame = advanceResult.NextFrame;
                    this.RepeatCount = advanceResult.UpdatedRepeatCount;
                    this._frameStartTime = DateTime.UtcNow;

                    // DEBUG: Log frame advancement
                    var msg = $"Frame {advanceResult.NextFrame}: delay={frameDelayMs}ms, elapsed={elapsedMs}ms";
                    System.Diagnostics.Debug.WriteLine(msg);
                    try
                    {
                        System.IO.File.AppendAllText("/tmp/gifbolt_timing.log", msg + "\n");
                    }
                    catch { }
                }

                // Always render on every tick for smooth visual feedback
                this.RenderFrame(this.Player.CurrentFrame);
            }
            catch
            {
                // Suppress render errors to avoid interrupting animation loop
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
            this._animationTimer?.Stop();
            this._animationTimer = null;
            base.Dispose();
        }
    }
}
