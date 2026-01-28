// <copyright file="GifAnimationControllerD3D.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GifBolt.Wpf
{
    /// <summary>
    /// GPU-accelerated GIF animation controller using D3DImage for direct DirectX interop.
    /// </summary>
    /// <remarks>
    /// This controller provides maximum performance by eliminating CPU-to-GPU memory copies.
    /// DirectX texture is rendered directly into WPF's composition pipeline via D3DImage.
    /// Requires Windows Vista or later with DirectX 9Ex or DirectX 11 compatible hardware.
    /// </remarks>
    internal sealed class GifAnimationControllerD3D : GifAnimationControllerBase
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        static GifAnimationControllerD3D()
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
                    System.IO.Path.GetDirectoryName(typeof(GifAnimationControllerD3D).Assembly.Location) ?? string.Empty,
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
        private D3DImage? _d3dImage;
        private DispatcherTimer? _renderTimer;
        private bool _isDisposed;
        private int _generationId;
        private string? _pendingRepeatBehavior;
        private System.Diagnostics.Stopwatch _frameStopwatch = new System.Diagnostics.Stopwatch();
        private ScalingFilter _scalingFilter = ScalingFilter.None;
        private bool _inNoGCRegion;

        // FPS diagnostics
        private System.Diagnostics.Stopwatch? _fpsStopwatch;
        private int _frameCount;
        private long _lastRenderTimeMs;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationControllerD3D"/> class.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
        /// <param name="onError">Callback invoked when loading fails.</param>
        public GifAnimationControllerD3D(Image image, string path, Action? onLoaded = null, Action<Exception>? onError = null)
        {
            this._image = image;
            this._sourcePath = path;
            this._sourceBytes = null;
            this._generationId = System.Environment.TickCount;

            this.BeginLoad(onLoaded, onError);
        }

        /// <summary>
        /// Initializes a new instance using in-memory GIF bytes.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="sourceBytes">The GIF data buffer.</param>
        /// <param name="onLoaded">Callback invoked when loading completes successfully.</param>
        /// <param name="onError">Callback invoked when loading fails.</param>
        public GifAnimationControllerD3D(Image image, byte[] sourceBytes, Action? onLoaded = null, Action<Exception>? onError = null)
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

                    // Create player with D3D9Ex backend for WPF D3DImage optimization
                    this.Player = GifBolt.GifPlayer.CreateWithBackend(GifBolt.GifPlayer.Backend.D3D9Ex);

                    // Fall back to D3D11 if D3D9Ex is not available
                    if (this.Player == null)
                    {
                        this.Player = GifBolt.GifPlayer.CreateWithBackend(GifBolt.GifPlayer.Backend.D3D11);
                    }

                    // Fall back to Dummy if D3D11 is not available
                    if (this.Player == null)
                    {
                        this.Player = new GifBolt.GifPlayer();
                    }

                    if (this.Player != null)
                    {
                        int backendType = this.Player.GetBackend();
                    }

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

                    this._image.Dispatcher.BeginInvoke(() =>
                    {
                        if (this._isDisposed || this._generationId != capturedGenerationId)
                        {
                            return;
                        }
                        try
                        {
                            this._d3dImage = new D3DImage();
                            this._d3dImage.IsFrontBufferAvailableChanged += this.OnIsFrontBufferAvailableChanged;

                            this._image.Source = this._d3dImage;
                            this._image.Stretch = Stretch.Fill;

                            this._renderTimer = new DispatcherTimer(DispatcherPriority.Render);
                            this._renderTimer.Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs);
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

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
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
            this._frameStopwatch.Restart();

            // Initialize FPS tracking
            this._fpsStopwatch = System.Diagnostics.Stopwatch.StartNew();
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

            if (this._d3dImage != null && !this._isDisposed)
            {
                this.RenderFrame();
            }

            if (this._renderTimer != null && !this._isDisposed)
            {
                this._renderTimer.Interval = TimeSpan.FromMilliseconds(GifPlayer.MinRenderIntervalMs);
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
            if (this.Player == null)
            {
                return;
            }

            this.Player.Stop();
            this.IsPlaying = false;
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

            if (this.Player == null)
            {
                this._pendingRepeatBehavior = repeatBehavior;
                return;
            }

            this.RepeatCount = this.Player.ComputeRepeatCount(repeatBehavior);
        }

        /// <summary>
        /// Sets the scaling filter used when resizing frames.
        /// </summary>
        /// <param name="filter">The scaling filter (Nearest, Bilinear, Bicubic, Lanczos).</param>
        /// <remarks>
        /// Note: D3D controller uses hardware acceleration and does not perform software scaling.
        /// This method is provided for API compatibility but has no effect on rendering.
        /// </remarks>
        public override void SetScalingFilter(ScalingFilter filter)
        {
            this._scalingFilter = filter;
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

        private void RenderFrame()
        {
            if (this._isDisposed || this.Player == null || this._d3dImage == null)
            {
                return;
            }

            if (!this._d3dImage.IsFrontBufferAvailable)
            {
                return;
            }

            try
            {
                // Advance to next frame and update GPU texture (C++ handles looping and frame indices)
                if (!this.Player.AdvanceAndRenderFrame())
                {
                    return;
                }

                // Get the updated native D3D surface pointer for the current frame
                IntPtr texturePtr = this.Player.GetCurrentGpuTexturePtr();

                if (texturePtr != IntPtr.Zero)
                {
                    // GPU-accelerated path: Use native D3D surface directly
                    // Native surfaces are already BGRA32 with alpha channel preserved
                    this._d3dImage.Lock();
                    try
                    {
                        // Set the native D3D surface as back buffer
                        // Alpha transparency is preserved in the surface format
                        this._d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, texturePtr);
                        
                        // Mark the entire image as dirty for repaint
                        if (this.Player != null)
                        {
                            this._d3dImage.AddDirtyRect(new Int32Rect(0, 0, this.Player.Width, this.Player.Height));
                        }
                    }
                    finally
                    {
                        this._d3dImage.Unlock();
                    }
                }
            }
            catch (Exception ex)
            {
                // Swallow render errors
            }
        }

        private void OnRenderTick(object? sender, EventArgs e)
        {
            if (this._isDisposed || this.Player == null || !this.IsPlaying || this._d3dImage == null || this._renderTimer == null)
            {
                return;
            }

            try
            {
                // Use minimum frame delay for timing (C++ handles actual frame delays and advancement)
                int frameDelayMs = GifPlayer.DefaultMinFrameDelayMs;
                long elapsedMs = this._frameStopwatch.ElapsedMilliseconds;

                // Check if enough time has passed to show the next frame
                if (elapsedMs >= frameDelayMs)
                {
                    // Measure render time
                    var renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    
                    // C++ handles frame advancement internally with looping
                    this.RenderFrame();
                    
                    var renderTimeMs = (System.Diagnostics.Stopwatch.GetTimestamp() - renderStart) / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
                    
                    // Update FPS counter
                    this.UpdateFpsTracking(frameDelayMs, renderTimeMs);
                    
                    this._frameStopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                // Swallow render errors
            }
        }

        private void OnIsFrontBufferAvailableChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (this._d3dImage != null && this._d3dImage.IsFrontBufferAvailable && this.IsPlaying)
            {
                this.RenderFrame();
            }
        }

        private void UpdateFpsTracking(int frameDelayMs, double renderTimeMs)
        {
            this._frameCount++;
            this._lastRenderTimeMs = this._fpsStopwatch?.ElapsedMilliseconds ?? 0;

            if (this._fpsStopwatch != null && this._fpsStopwatch.ElapsedMilliseconds > 1000)
            {
                var fps = this._frameCount * 1000.0 / this._fpsStopwatch.ElapsedMilliseconds;
                var fpsText = $"FPS: {fps:F1} | Render: {renderTimeMs:F2}ms | Delay: {frameDelayMs}ms";

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
        /// Releases all resources held by the animation controller.
        /// </summary>
        public override void Dispose()
        {
            this._isDisposed = true;
            this._generationId = int.MinValue;
            this.IsPlaying = false;

            // Exit NoGCRegion if active
            if (this._inNoGCRegion)
            {
                System.GC.EndNoGCRegion();
                this._inNoGCRegion = false;
            }

            if (this._renderTimer != null)
            {
                this._renderTimer.Stop();
                this._renderTimer = null;
            }

            if (this._d3dImage != null)
            {
                this._d3dImage.IsFrontBufferAvailableChanged -= this.OnIsFrontBufferAvailableChanged;
                this._d3dImage = null;
            }

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

            base.Dispose();

            if (!this._image.Dispatcher.CheckAccess())
            {
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
