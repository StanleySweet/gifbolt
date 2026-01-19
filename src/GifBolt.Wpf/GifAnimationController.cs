// <copyright file="GifAnimationController.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Manages animation state and frame rendering for a GIF displayed in an Image control.
    /// Handles frame decoding, timing, and pixel updates to the display.
    /// Uses asynchronous loading and DispatcherTimer for efficient CPU usage.
    /// </summary>
    internal sealed class GifAnimationController : IDisposable
    {
        // Static DLL import for explicit loading
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        // Static constructor to preload the native library
        static GifAnimationController()
        {
            EnsureNativeLibraryLoaded();
        }

        /// <summary>
        /// Ensures the native GifBolt library is loaded before P/Invoke calls.
        /// </summary>
        private static void EnsureNativeLibraryLoaded()
        {
            const string nativeLib = "GifBolt.Native";
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(GifAnimationController).Assembly.Location) ?? string.Empty,
                    nativeLib + ".dll"),
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    nativeLib + ".dll"),
                System.IO.Path.Combine(
                    Environment.CurrentDirectory,
                    nativeLib + ".dll"),
                nativeLib + ".dll"
            };

            foreach (string dllPath in searchPaths)
            {
                if (string.IsNullOrEmpty(dllPath))
                {
                    continue;
                }

                if (System.IO.File.Exists(dllPath) || dllPath == nativeLib + ".dll")
                {
                    IntPtr result = LoadLibrary(dllPath);
                    if (result != IntPtr.Zero)
                    {
                        return;
                    }
                }
            }
        }

        private readonly Image _image;
        private readonly string? _sourcePath;
        private readonly byte[]? _sourceBytes;
        private GifBolt.GifPlayer? _player;
        private WriteableBitmap? _writeableBitmap;
        private DispatcherTimer? _renderTimer;
        private bool _isPlaying;
        private int _repeatCount;
        private DateTime _lastFrameTimestamp = default;
        private bool _isDisposed;
        private int _generationId;
        private string? _pendingRepeatBehavior;
        private DateTime _frameStartTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
        /// Performs asynchronous loading to avoid blocking the UI thread.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
        /// <param name="onError">Callback invoked when loading fails.</param>
        public GifAnimationController(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null)
        {
            this._image = image;
            this._sourcePath = path;
            this._sourceBytes = null;
            this._generationId = System.Environment.TickCount; // Unique ID for this instance

            this.BeginLoad(onLoaded, onError);
        }

        /// <summary>
        /// Initializes a new instance using in-memory GIF bytes.
        /// </summary>
        public GifAnimationController(Image image, byte[] sourceBytes, Action? onLoaded = null, Action<Exception>? onError = null)
        {
            this._image = image;
            this._sourceBytes = sourceBytes;
            this._sourcePath = null;
            this._generationId = System.Environment.TickCount;

            this.BeginLoad(onLoaded, onError);
        }

        private void BeginLoad(Action? onLoaded, Action<Exception>? onError)
        {
            int capturedGenerationId = this._generationId;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (this._isDisposed || this._generationId != capturedGenerationId)
                    {
                        return;
                    }

                    this._player = new GifBolt.GifPlayer();
                    this._player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                    bool loaded = this._sourceBytes != null
                        ? this._player.Load(this._sourceBytes)
                        : this._sourcePath != null && this._player.Load(this._sourcePath);

                    if (!loaded || this._player == null)
                    {
                        var error = this._sourceBytes != null
                            ? new InvalidOperationException("Failed to load GIF from in-memory bytes.")
                            : new InvalidOperationException($"Failed to load GIF from path: {this._sourcePath}. File may not exist or be corrupt.");

                        this._player?.Dispose();
                        this._player = null;

                        if (!this._isDisposed && this._generationId == capturedGenerationId)
                        {
                            this._image.Dispatcher.BeginInvoke(() => onError?.Invoke(error));
                        }

                        return;
                    }

                    int displayWidth = Math.Max(1, (int)this._image.ActualWidth);
                    int displayHeight = Math.Max(1, (int)this._image.ActualHeight);

                    if (displayWidth < 10 || displayHeight < 10)
                    {
                        displayWidth = this._player.Width;
                        displayHeight = this._player.Height;
                    }

                    byte[]? initialPixels = null;
                    int scaledWidth = displayWidth;
                    int scaledHeight = displayHeight;

                    if (this._player.TryGetFramePixelsBgra32PremultipliedScaled(
                        0,
                        displayWidth,
                        displayHeight,
                        out byte[] bgraPixels,
                        out int outWidth,
                        out int outHeight,
                        filter: GifBolt.Internal.ScalingFilter.Lanczos) &&
                        bgraPixels.Length > 0)
                    {
                        initialPixels = new byte[bgraPixels.Length];
                        System.Buffer.BlockCopy(bgraPixels, 0, initialPixels, 0, bgraPixels.Length);
                        scaledWidth = outWidth;
                        scaledHeight = outHeight;
                    }

                    this._image.Dispatcher.BeginInvoke(() =>
                    {
                        if (this._isDisposed || this._generationId != capturedGenerationId)
                        {
                            return;
                        }

                        try
                        {
                            var wb = new WriteableBitmap(
                                scaledWidth,
                                scaledHeight,
                                96,
                                96,
                                PixelFormats.Bgra32,
                                null);

                            if (initialPixels != null && initialPixels.Length > 0)
                            {
                                wb.WritePixels(
                                    new Int32Rect(0, 0, scaledWidth, scaledHeight),
                                    initialPixels,
                                    scaledWidth * 4,
                                    0);
                            }

                            this._writeableBitmap = wb;
                            this._image.Source = this._writeableBitmap;

                            this._renderTimer = new DispatcherTimer(DispatcherPriority.Render);
                            this._renderTimer.Interval = TimeSpan.FromMilliseconds(FrameTimingHelper.MinRenderIntervalMs);
                            this._renderTimer.Tick += this.OnRenderTick;

                            if (!string.IsNullOrWhiteSpace(this._pendingRepeatBehavior))
                            {
                                this.SetRepeatBehavior(this._pendingRepeatBehavior);
                                this._pendingRepeatBehavior = null;
                            }

                            onLoaded?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            if (!this._isDisposed && this._generationId == capturedGenerationId)
                            {
                                onError?.Invoke(ex);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (!this._isDisposed && this._generationId == capturedGenerationId)
                    {
                        this._image.Dispatcher.BeginInvoke(() => onError?.Invoke(ex));
                    }
                }
            });
        }

        public void Play()
        {
            if (this._isDisposed)
            {
                return;
            }

            if (this._player == null)
            {
                return;
            }

            this._player.Play();
            this._isPlaying = true;
            this._frameStartTime = DateTime.UtcNow;

            if (this._writeableBitmap != null && !this._isDisposed)
            {
                this.RenderFrame(this._player.CurrentFrame);
            }

            if (this._renderTimer != null && !this._isDisposed)
            {
                int initialDelay = this._player.GetFrameDelayMs(this._player.CurrentFrame);
                int effectiveDelay = FrameAdvanceHelper.GetEffectiveFrameDelay(initialDelay, FrameTimingHelper.MinRenderIntervalMs);
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(effectiveDelay);
                this._renderTimer.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public void Pause()
        {
            if (this._isDisposed || this._player == null)
            {
                return;
            }

            this._player.Pause();
            this._isPlaying = false;
            this._renderTimer?.Stop();
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            if (this._player == null)
            {
                return;
            }

            this._player.Stop();
            this._isPlaying = false;
            this._renderTimer?.Stop();
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        public void SetRepeatBehavior(string repeatBehavior)
        {
            if (this._isDisposed)
            {
                return;
            }

            // If player is not ready yet, store the behavior to apply after loading
            if (this._player == null)
            {
                this._pendingRepeatBehavior = repeatBehavior;
                return;
            }

            this._repeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this._player.IsLooping);
        }

        /// <summary>
        /// Sets the minimum frame delay in milliseconds.
        /// </summary>
        /// <param name="minDelayMs">The minimum frame delay.</param>
        public void SetMinFrameDelayMs(int minDelayMs)
        {
            if (this._player != null && minDelayMs > 0)
            {
                this._player.SetMinFrameDelayMs(minDelayMs);
            }
        }

        /// <summary>
        /// Renders a specific frame to the WriteableBitmap.
        /// </summary>
        /// <param name="frameIndex">The index of the frame to render.</param>
        private void RenderFrame(int frameIndex)
        {
            if (this._isDisposed || this._player == null || this._writeableBitmap == null)
            {
                return;
            }

            try
            {
                int displayWidth = this._writeableBitmap.PixelWidth;
                int displayHeight = this._writeableBitmap.PixelHeight;

                if (this._player.TryGetFramePixelsBgra32PremultipliedScaled(
                    frameIndex,
                    displayWidth,
                    displayHeight,
                    out byte[] bgraPixels,
                    out int outWidth,
                    out int outHeight,
                    filter: GifBolt.Internal.ScalingFilter.Lanczos))
                {
                    if (bgraPixels.Length == 0 || this._isDisposed)
                    {
                        return;
                    }

                    int stride = outWidth * 4;

                    // Add disposal check before WritePixels to prevent deadlock
                    if (!this._isDisposed && this._writeableBitmap != null)
                    {
                        this._writeableBitmap.WritePixels(
                            new Int32Rect(0, 0, outWidth, outHeight),
                            bgraPixels,
                            stride,
                            0);
                    }
                }
            }
            catch
            {
                // Swallow render errors
            }
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            // Early exit if disposed or invalid
            if (this._isDisposed || this._player == null || !this._isPlaying || this._writeableBitmap == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                // Get the frame delay for the current frame (respects the 100ms minimum set in base constructor)
                int frameDelayMs = this._player.GetFrameDelayMs(this._player.CurrentFrame);
                long elapsedMs = (long)(DateTime.UtcNow - this._frameStartTime).TotalMilliseconds;

                // Only advance frame if enough time has elapsed for the current frame
                if (elapsedMs >= frameDelayMs)
                {
                    // Advance to next frame and reset the frame start time
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
                    this._frameStartTime = DateTime.UtcNow;
                }

                // Render the current frame
                this.RenderFrame(this._player.CurrentFrame);
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
            // Set disposed flag FIRST to block any pending operations
            this._isDisposed = true;
            this._generationId = int.MinValue; // Invalidate generation ID to block pending operations
            this._isPlaying = false;

            // Stop and fully release the render timer
            if (this._renderTimer != null)
            {
                this._renderTimer.Stop();
                this._renderTimer = null;
            }

            // Dispose player and stop prefetching thread
            if (this._player != null)
            {
                try
                {
                    this._player.Stop();
                    this._player.Dispose();
                }
                catch
                {
                    // Swallow disposal errors
                }
                finally
                {
                    this._player = null;
                }
            }

            // Release bitmap data
            this._writeableBitmap = null;

            // Clear the image source on the UI thread to break references
            if (!this._image.Dispatcher.CheckAccess())
            {
                // If not on UI thread, use BeginInvoke to clear async
                this._image.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        this._image.Source = null;
                    }
                    catch
                    {
                        // Swallow errors if image is already disposed
                    }
                });
            }
            else
            {
                // Already on UI thread, clear synchronously
                try
                {
                    this._image.Source = null;
                }
                catch
                {
                    // Swallow errors if image is already disposed
                }
            }

            this._lastFrameTimestamp = default;
            GC.SuppressFinalize(this);
        }
    }
}
