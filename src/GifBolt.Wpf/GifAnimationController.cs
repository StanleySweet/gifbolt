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
    /// WPF-specific GIF animation controller using CompositionTarget.Rendering for frame timing.
    /// </summary>
    internal sealed class GifAnimationController : GifAnimationControllerBase
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
        private WriteableBitmap? _writeableBitmap;
        private DispatcherTimer? _renderTimer;
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

                    this.Player = new GifBolt.GifPlayer();
                    // this.Player.SetMinFrameDelayMs(FrameTimingHelper.DefaultMinFrameDelayMs);

                    bool loaded = this._sourceBytes != null
                        ? this.Player.Load(this._sourceBytes)
                        : this._sourcePath != null && this.Player.Load(this._sourcePath);

                    if (!loaded || this.Player == null)
                    {
                        var error = this._sourceBytes != null
                            ? new InvalidOperationException("Failed to load GIF from in-memory bytes.")
                            : new InvalidOperationException($"Failed to load GIF from path: {this._sourcePath}. File may not exist or be corrupt.");

                        this.Player?.Dispose();
                        this.Player = null;

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
                        displayWidth = this.Player.Width;
                        displayHeight = this.Player.Height;
                    }

                    byte[]? initialPixels = null;
                    int scaledWidth = displayWidth;
                    int scaledHeight = displayHeight;

                    if (this.Player.TryGetFramePixelsBgra32PremultipliedScaled(
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

        public override void Play()
        {
            if (this._isDisposed)
            {
                return;
            }

            if (this.Player == null)
            {
                return;
            }

            this.Player.Play();
            this.IsPlaying = true;
            this._frameStartTime = DateTime.UtcNow;

            if (this._writeableBitmap != null && !this._isDisposed)
            {
                this.RenderFrame(this.Player.CurrentFrame);
            }

            if (this._renderTimer != null && !this._isDisposed)
            {
                // Use a fixed, fast timer (16ms = 60 FPS) for smooth rendering
                // Frame advancement is calculated based on actual elapsed time, not timer interval
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(FrameTimingHelper.MinRenderIntervalMs);
                this._renderTimer.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public override void Pause()
        {
            if (this._isDisposed || this.Player == null)
            {
                return;
            }

            this.Player.Pause();
            this.IsPlaying = false;
            this._renderTimer?.Stop();
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public override void Stop()
        {
            if (this.Player == null)
            {
                return;
            }

            this.Player.Stop();
            this.IsPlaying = false;
            this._renderTimer?.Stop();
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        public override void SetRepeatBehavior(string repeatBehavior)
        {
            if (this._isDisposed)
            {
                return;
            }

            // If player is not ready yet, store the behavior to apply after loading
            if (this.Player == null)
            {
                this._pendingRepeatBehavior = repeatBehavior;
                return;
            }

            this.RepeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this.Player.IsLooping);
        }

        /// <summary>
        /// Sets the minimum frame delay in milliseconds.
        /// </summary>
        /// <param name="minDelayMs">The minimum frame delay.</param>
        public void SetMinFrameDelayMs(int minDelayMs)
        {
            if (this.Player != null && minDelayMs > 0)
            {
                this.Player.SetMinFrameDelayMs(minDelayMs);
            }
        }

        /// <summary>
        /// Renders a specific frame to the WriteableBitmap.
        /// </summary>
        /// <param name="frameIndex">The index of the frame to render.</param>
        private void RenderFrame(int frameIndex)
        {
            if (this._isDisposed || this.Player == null || this._writeableBitmap == null)
            {
                return;
            }

            try
            {
                int displayWidth = this._writeableBitmap.PixelWidth;
                int displayHeight = this._writeableBitmap.PixelHeight;

                if (this.Player.TryGetFramePixelsBgra32PremultipliedScaled(
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
            if (this._isDisposed || this.Player == null || !this.IsPlaying || this._writeableBitmap == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                // Get the frame delay for the current frame and clamp to minimum
                int rawFrameDelayMs = this.Player.GetFrameDelayMs(this.Player.CurrentFrame);
                int frameDelayMs = Math.Max(rawFrameDelayMs, FrameTimingHelper.DefaultMinFrameDelayMs);
                long elapsedMs = (long)(DateTime.UtcNow - this._frameStartTime).TotalMilliseconds;

                // Only advance frame if enough time has elapsed for the current frame
                if (elapsedMs >= frameDelayMs)
                {
                    // Advance to next frame and reset the frame start time
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
                // Swallow render errors
            }
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public override void Dispose()
        {
            // Set disposed flag FIRST to block any pending operations
            this._isDisposed = true;
            this._generationId = int.MinValue; // Invalidate generation ID to block pending operations
            this.IsPlaying = false;

            // Stop and fully release the render timer
            if (this._renderTimer != null)
            {
                this._renderTimer.Stop();
                this._renderTimer = null;
            }

            // Dispose player and stop prefetching thread
            if (this.Player != null)
            {
                try
                {
                    this.Player.Stop();
                }
                catch
                {
                    // Swallow disposal errors
                }
            }

            // Call base Dispose to clean up player
            base.Dispose();

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

            GC.SuppressFinalize(this);
        }
    }
}
