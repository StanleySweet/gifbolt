// <copyright file="ImageBehavior.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using Avalonia;
using GifBolt;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

/// <summary>
/// Avalonia-specific GIF animation controller using DispatcherTimer for frame timing.
/// </summary>
internal sealed class GifAnimationController : GifAnimationControllerBase
{
    private readonly Image _image;
    private WriteableBitmap? _writeableBitmap;
    private DispatcherTimer? _animationTimer;
    private DateTime _frameStartTime;

    public int Width => this.Player?.Width ?? 0;
    public int Height => this.Player?.Height ?? 0;
    public int FrameCount => this.Player?.FrameCount ?? 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
    /// </summary>
    /// <param name="image">The Image control to animate.</param>
    /// <param name="path">The file path to the GIF image.</param>
    /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
    /// <param name="onError">Callback invoked when loading fails.</param>
    public GifAnimationController(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null)
        : base()
    {
        this._image = image;

        // Load the GIF asynchronously to avoid blocking the UI thread
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // Initialize player property
                this.Player = new GifBolt.GifPlayer();
                this.Player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                if (!this.Player.Load(path))
                {
                    var error = new InvalidOperationException($"Failed to load GIF from path: {path}. File may not exist or be corrupt.");
                    this.Player.Dispose();
                    this.Player = null;
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => onError?.Invoke(error));
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

                // Assign the bitmap on the UI thread
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    this._writeableBitmap = wb;
                    this._image.Source = this._writeableBitmap;
                    this._image.InvalidateVisual();

                    this._animationTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(16)
                    };
                    this._animationTimer.Tick += this.OnRenderTick;
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
    public override void SetRepeatBehavior(string repeatBehavior)
    {
        this.RepeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this.Player?.IsLooping ?? true);
    }

    /// <summary>
    /// Sets the minimum frame delay (in milliseconds).
    /// </summary>
    /// <param name="minDelayMs">The minimum frame delay.</param>
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
            // Démarre le timer avec le délai de la première frame
            int initialDelay = this.Player?.GetFrameDelayMs(this.Player?.CurrentFrame ?? 0) ?? 16;
            int effectiveDelay = FrameAdvanceHelper.GetEffectiveFrameDelay(initialDelay, FrameTimingHelper.MinRenderIntervalMs);
            this._animationTimer.Interval = TimeSpan.FromMilliseconds(effectiveDelay);
            this._animationTimer.Start();
        }
    }

    /// <summary>
    /// Pauses playback of the animation.
    /// </summary>
    public override void Pause()
    {
        this.Player?.Pause();
        this.IsPlaying = false;
        this._animationTimer?.Stop();
    }

    /// <summary>
    /// Stops playback and resets to the first frame.
    /// </summary>
    public override void Stop()
    {
        this.Player?.Stop();
        this.IsPlaying = false;
        this._animationTimer?.Stop();
    }

    /// <summary>
    /// Renders a specific frame to the WriteableBitmap.
    /// </summary>
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
            // Swallow render errors
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (this.Player == null || !this.IsPlaying || this._writeableBitmap == null || this._animationTimer == null)
        {
            return;
        }

        try
        {
            // Get the frame delay for the current frame (respects the minimum set in base constructor)
            int frameDelayMs = this.Player.GetFrameDelayMs(this.Player.CurrentFrame);
            long elapsedMs = (long)(DateTime.UtcNow - this._frameStartTime).TotalMilliseconds;

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
            }

            // Render the current frame
            this.RenderFrame(this.Player.CurrentFrame);
        }
        catch
        {
            // Swallow render errors
        }
    }

    /// <summary>
    /// Releases all resources held by the animation controller.
    /// </summary>
    public override void Dispose()
    {
        this._animationTimer?.Stop();
        this._animationTimer = null;
        base.Dispose();
    }
}
