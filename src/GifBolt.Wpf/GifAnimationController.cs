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
using GifBolt;

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
        private System.Diagnostics.Stopwatch _frameStopwatch = new System.Diagnostics.Stopwatch();
        private bool _wasPlayingBeforeHidden;
        private ScalingFilter _scalingFilter = ScalingFilter.None;
        private bool _inNoGCRegion;
        private byte[]? _cachedTransparentFill;
        private int _cachedFillWidth;
        private int _cachedFillHeight;

        // FPS diagnostics
        private System.Diagnostics.Stopwatch? _fpsStopwatch;
        private int _frameCount;

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

            // Subscribe to visibility changes for automatic pause/resume
            this._image.IsVisibleChanged += this.OnImageVisibilityChanged;

            this.BeginLoad(onLoaded, onError);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class using in-memory GIF bytes.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <param name="sourceBytes">The image data.</param>
        /// <param name="onLoaded">The loaded callback.</param>
        /// <param name="onError">The error callback.</param>
        public GifAnimationController(Image image, byte[] sourceBytes, Action? onLoaded = null, Action<Exception>? onError = null)
        {
            this._image = image;
            this._sourceBytes = sourceBytes;
            this._sourcePath = null;
            this._generationId = System.Environment.TickCount;

            // Subscribe to visibility changes for automatic pause/resume
            this._image.IsVisibleChanged += this.OnImageVisibilityChanged;

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

                    // Get initial frame pixels (consolidates scaling logic)
                    byte[]? initialPixels = this.GetFramePixels(0, out int scaledWidth, out int scaledHeight);

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
                            this._renderTimer.Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs);
                            this._renderTimer.Tick += this.OnRenderTick;

                            // Create animation context with GIF metadata
                            if (this.Player != null)
                            {
                                this.AnimationContext = GifPlayer.CreateAnimationContext(this.Player, null);
                            }

                            // Apply pending repeat behavior BEFORE calling onLoaded (which starts playback)
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

            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Play();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, true, false);
            this._frameStopwatch.Restart();

            // Initialize FPS stopwatch
            if (this._fpsStopwatch == null)
            {
                this._fpsStopwatch = new System.Diagnostics.Stopwatch();
            }

            this._fpsStopwatch.Restart();
            this._frameCount = 0;

            // Suppress GC during animation to prevent collection pauses that cause jitter
            if (!this._inNoGCRegion)
            {
                // Aggressively collect before starting region
                System.GC.Collect(System.GC.MaxGeneration, System.GCCollectionMode.Forced, blocking: true, compacting: true);
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
                try
                {
                    // 100MB budget for sustained animation playback
                    this._inNoGCRegion = System.GC.TryStartNoGCRegion(100 * 1024 * 1024);
                }
                catch (System.InvalidOperationException)
                {
                    // Already in a NoGCRegion (shouldn't happen with flag check, but handle defensively)
                    this._inNoGCRegion = true;
                }
            }

            if (this._writeableBitmap != null && !this._isDisposed)
            {
                this.RenderFrame(this.Player.CurrentFrame);
            }

            if (this._renderTimer != null && !this._isDisposed)
            {
                // Use a fixed, fast timer (16ms = 60 FPS) for smooth rendering
                // Frame advancement is calculated based on actual elapsed time, not timer interval
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs);
                this._renderTimer.Start();
            }
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public override void Pause()
        {
            if (this._isDisposed || this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Pause();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, false, false);
            this._renderTimer?.Stop();
            if (this._inNoGCRegion)
            {
                System.GC.EndNoGCRegion();
                this._inNoGCRegion = false;
            }
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public override void Stop()
        {
            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Stop();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, false, true);
            this._renderTimer?.Stop();
            if (this._inNoGCRegion)
            {
                System.GC.EndNoGCRegion();
                this._inNoGCRegion = false;
            }
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
            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                this._pendingRepeatBehavior = repeatBehavior;
                return;
            }

            int repeatCount = this.Player.ComputeRepeatCount(repeatBehavior);
            GifPlayer.SetAnimationRepeatCount(this.AnimationContext, repeatCount);
        }

        /// <summary>
        /// Sets the minimum frame delay in milliseconds.
        /// </summary>
        /// <param name="minDelayMs">The minimum frame delay.</param>
        public void SetMinFrameDelayMs(int minDelayMs)
        {
            if (this.Player != null && minDelayMs > 0)
            {
                this.Player.MinFrameDelayMs = minDelayMs;
            }
        }

        /// <summary>
        /// Sets the scaling filter used when resizing frames.
        /// </summary>
        /// <param name="filter">The scaling filter (Nearest, Bilinear, Bicubic, Lanczos).</param>
        /// <remarks>
        /// When the filter changes, reinitializes the rendering pipeline and re-renders the current frame.
        /// </remarks>
        public override void SetScalingFilter(ScalingFilter filter)
        {
            if (this._scalingFilter == filter)
            {
                return; // No change
            }

            // Pause during filter change to avoid race conditions with the animation timer
            bool wasPlaying = this.AnimationContext != System.IntPtr.Zero && 
                              GifPlayer.GetAnimationState(this.AnimationContext).IsPlaying != 0;
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
                    int displayWidth = Math.Max(1, (int)this._image.ActualWidth);
                    int displayHeight = Math.Max(1, (int)this._image.ActualHeight);

                    if (displayWidth < 10 || displayHeight < 10)
                    {
                        displayWidth = nativeWidth;
                        displayHeight = nativeHeight;
                    }

                    // Determine target bitmap dimensions based on filter
                    int targetWidth = filter == ScalingFilter.None ? nativeWidth : displayWidth;
                    int targetHeight = filter == ScalingFilter.None ? nativeHeight : displayHeight;

                    // Recreate bitmap if dimensions changed
                    if (this._writeableBitmap.PixelWidth != targetWidth ||
                        this._writeableBitmap.PixelHeight != targetHeight)
                    {
                        this._writeableBitmap = new WriteableBitmap(
                            targetWidth,
                            targetHeight,
                            96,
                            96,
                            PixelFormats.Bgra32,
                            null);

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
        /// Renders a specific frame to the WriteableBitmap atomically.
        /// Consolidates pixel buffer fetching and bitmap updates in a single call.
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
                // Single consolidate call to get frame pixels (avoids duplicate scaling logic)
                byte[]? bgraPixels = this.GetFramePixels(frameIndex, out int outWidth, out int outHeight);

                if (bgraPixels != null && bgraPixels.Length > 0 && !this._isDisposed)
                {
                    // Fill transparent area if bitmap is larger than frame data
                    if (this._writeableBitmap.PixelWidth > outWidth || this._writeableBitmap.PixelHeight > outHeight)
                    {
                        this.FillTransparent(this._writeableBitmap.PixelWidth, this._writeableBitmap.PixelHeight);
                    }

                    // Write frame pixels to bitmap
                    this._writeableBitmap.WritePixels(
                        new Int32Rect(0, 0, outWidth, outHeight),
                        bgraPixels,
                        outWidth * 4,
                        0);
                }
            }
            catch
            {
                // Swallow render errors
            }
        }

        /// <summary>
        /// Gets frame pixels, handling both scaled and unscaled rendering in a single method.
        /// Dynamically determines dimensions during initial load vs rendering phases.
        /// </summary>
        private byte[]? GetFramePixels(int frameIndex, out int outWidth, out int outHeight)
        {
            outWidth = 0;
            outHeight = 0;

            if (this.Player == null)
            {
                return null;
            }

            // Unscaled rendering - use native GIF resolution
            if (this._scalingFilter == ScalingFilter.None)
            {
                if (this.Player.TryGetFramePixelsBgra32PremultipliedBuffer(frameIndex, out var pixelBuffer) &&
                    pixelBuffer.IsValid && pixelBuffer.SizeInBytes > 0)
                {
                    byte[] pixels = pixelBuffer.ToArray();
                    pixelBuffer.Dispose();
                    outWidth = this.Player.Width;
                    outHeight = this.Player.Height;
                    return pixels;
                }
            }
            else
            {
                // Scaled rendering - determine target size dynamically
                int displayWidth = this.Player.Width;
                int displayHeight = this.Player.Height;

                // During rendering, use bitmap size if available
                if (this._writeableBitmap != null)
                {
                    displayWidth = this._writeableBitmap.PixelWidth;
                    displayHeight = this._writeableBitmap.PixelHeight;
                }
                // During initial load, try to use image actual size
                else if (this._image?.ActualWidth > 0 && this._image?.ActualHeight > 0)
                {
                    displayWidth = Math.Max(1, (int)this._image.ActualWidth);
                    displayHeight = Math.Max(1, (int)this._image.ActualHeight);
                    if (displayWidth < 10 || displayHeight < 10)
                    {
                        displayWidth = this.Player.Width;
                        displayHeight = this.Player.Height;
                    }
                }

                if (this.Player.TryGetFramePixelsBgra32PremultipliedScaledBuffer(
                    frameIndex,
                    displayWidth,
                    displayHeight,
                    out var scaledPixelBuffer,
                    out outWidth,
                    out outHeight,
                    filter: this._scalingFilter) &&
                    scaledPixelBuffer.IsValid && scaledPixelBuffer.SizeInBytes > 0)
                {
                    byte[] pixels = scaledPixelBuffer.ToArray();
                    scaledPixelBuffer.Dispose();
                    return pixels;
                }
            }

            return null;
        }

        /// <summary>
        /// Fills the bitmap with transparent pixels (reuses cached buffer).
        /// </summary>
        private void FillTransparent(int width, int height)
        {
            int requiredSize = width * height * 4;

            // Reuse cached transparent buffer if dimensions match
            if (this._cachedTransparentFill == null || 
                this._cachedFillWidth != width || 
                this._cachedFillHeight != height)
            {
                this._cachedTransparentFill = new byte[requiredSize];
                this._cachedFillWidth = width;
                this._cachedFillHeight = height;
            }

            // Write the zeroed (transparent) buffer to bitmap
            if (this._writeableBitmap != null)
            {
                this._writeableBitmap.WritePixels(
                    new Int32Rect(0, 0, width, height),
                    this._cachedTransparentFill,
                    width * 4,
                    0);
            }
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            // Early exit if disposed or invalid  
            if (this._isDisposed || this.Player == null || this.AnimationContext == System.IntPtr.Zero 
                || this._writeableBitmap == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                // Get the raw frame delay and advance frame with consolidated C++ logic
                int rawFrameDelayMs = this.Player.GetFrameDelayMs(this.Player.CurrentFrame);
                int minFrameDelayMs = this.Player.MinFrameDelayMs;
                long elapsedMs = this._frameStopwatch.ElapsedMilliseconds;

                // Only advance frame if enough time has elapsed
                if (elapsedMs < rawFrameDelayMs)
                {
                    return;
                }

                // Use consolidated animation context API for frame advancement
                // Returns frame index, completion state, repeat count, and effective delay all at once
                bool advanced = GifPlayer.AdvanceAnimation(
                    this.AnimationContext,
                    rawFrameDelayMs,
                    minFrameDelayMs,
                    out var advanceResult);

                if (!advanced || advanceResult.IsComplete != 0)
                {
                    this.Stop();
                    return;
                }

                // Reset and render frame 0 when looping
                if (advanceResult.NextFrame == 0)
                {
                    if (this.Player != null)
                    {
                        this.Player.ResetCanvas();
                        this.Player.CurrentFrame = 0;
                        GifPlayer.SetAnimationCurrentFrame(this.AnimationContext, 0);
                    }

                    this.RenderFrameOptimized(0, advanceResult.EffectiveDelayMs);
                    return;
                }

                // Update frame and render (consolidate state update and render in one call)
                if (this.Player != null)
                {
                    this.Player.CurrentFrame = advanceResult.NextFrame;
                    this.RenderFrameOptimized(advanceResult.NextFrame, advanceResult.EffectiveDelayMs);
                }
            }
            catch
            {
                // Swallow render errors
            }
        }

        /// <summary>
        /// Optimized frame rendering that combines pixel buffer fetch and bitmap update.
        /// Resets the stopwatch and updates FPS tracking in one operation.
        /// </summary>
        private void RenderFrameOptimized(int frameIndex, int effectiveDelayMs)
        {
            this.RenderFrame(frameIndex);
            this._frameStopwatch.Restart();
            this.UpdateFpsTracking(effectiveDelayMs, frameIndex);
        }

        private void UpdateFpsTracking(int frameDelayMs, int frameIndex)
        {
            this._frameCount++;

            if (this._fpsStopwatch != null && this._fpsStopwatch.ElapsedMilliseconds > 1000)
            {
                var fps = this._frameCount * 1000.0 / this._fpsStopwatch.ElapsedMilliseconds;
                var fpsText = $"FPS: {fps:F1} | Frame: {frameIndex} | Delay: {frameDelayMs}ms";

                // Update FPS on the Image control
                this._image.Dispatcher.BeginInvoke(() =>
                {
                    AnimationBehavior.SetFpsText(this._image, fpsText);
                });

                this._fpsStopwatch.Restart();
                this._frameCount = 0;
            }
        }

        /// <summary>
        /// Handles visibility changes of the Image control.
        /// Pauses animation when hidden and resumes when visible to save resources.
        /// </summary>
        /// <param name="sender">The Image control.</param>
        /// <param name="e">Event arguments.</param>
        private void OnImageVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this._isDisposed || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            bool isVisible = this._image.IsVisible;
            var state = GifPlayer.GetAnimationState(this.AnimationContext);
            bool isPlaying = state.IsPlaying != 0;

            if (!isVisible && isPlaying)
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
        public override void Dispose()
        {
            // Set disposed flag FIRST to block any pending operations
            this._isDisposed = true;
            this._generationId = int.MinValue; // Invalidate generation ID to block pending operations

            // Exit NoGCRegion if active
            if (this._inNoGCRegion)
            {
                System.GC.EndNoGCRegion();
                this._inNoGCRegion = false;
            }

            // Unsubscribe from visibility changes
            this._image.IsVisibleChanged -= this.OnImageVisibilityChanged;

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

            // Destroy animation context
            if (this.AnimationContext != System.IntPtr.Zero)
            {
                GifPlayer.DestroyAnimationContext(this.AnimationContext);
                this.AnimationContext = System.IntPtr.Zero;
            }

            // Call base Dispose to clean up player and animation context
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
