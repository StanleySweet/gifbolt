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
        // Default: enforce minimum delay per Chrome/macOS/ezgif standard
        this._player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

        // Load the GIF asynchronously to avoid blocking the UI thread
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

                // Get frame 0 pixels on background thread to avoid UI blocking
                if (this._player.TryGetFramePixelsBgra32Premultiplied(0, out byte[] bgraPixels) && bgraPixels.Length > 0)
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
        this._repeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this._player.IsLooping);
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

        // Render the current frame immediately to avoid delay
        if (this._writeableBitmap != null)
        {
            this.RenderFrame(this._player.CurrentFrame);
        }

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

    /// <summary>
    /// Renders a specific frame to the WriteableBitmap.
    /// </summary>
    /// <param name="frameIndex">The index of the frame to render.</param>
    private void RenderFrame(int frameIndex)
    {
        if (this._player == null || this._writeableBitmap == null)
        {
            return;
        }

        try
        {
            if (this._player.TryGetFramePixelsBgra32Premultiplied(frameIndex, out byte[] bgraPixels))
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

            // Render the current frame
            this.RenderFrame(this._player.CurrentFrame);

            // Advance to the next frame using shared helper
            var advanceResult = FrameAdvanceHelper.AdvanceFrame(
                this._player.CurrentFrame,
                this._player.FrameCount,
                this._repeatCount);

            if (advanceResult.IsComplete)
            {
                this.Stop();
                return;
            }

            // Update the current frame and repeat count
            this._player.CurrentFrame = advanceResult.NextFrame;
            this._repeatCount = advanceResult.UpdatedRepeatCount;

            // Dynamically update the timer interval for the next frame
            int nextDelay = this._player.GetFrameDelayMs(advanceResult.NextFrame);
            if (this._renderTimer != null)
            {
                int effectiveDelay = FrameAdvanceHelper.GetEffectiveFrameDelay(nextDelay, FrameTimingHelper.MinRenderIntervalMs);
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(effectiveDelay);
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
