// <copyright file="GifPlayer.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using GifBolt.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static GifBolt.Internal.Native;

namespace GifBolt;

/// <summary>
/// Cross-platform GIF player for netstandard2.0.
/// Provides loading, playback control, and RGBA pixel access.
/// Platform wrappers (WPF, etc.) build on top of this.
/// </summary>
public sealed class GifPlayer : IDisposable
{
    /// <summary>
    /// The default minimum frame delay in milliseconds (10 ms).
    /// Most GIFs are created with delays of 10-100ms; we use 10ms as a reasonable
    /// minimum to prevent GIFs with very small delays from playing too fast,
    /// while still allowing fast animations to play at reasonable speeds.
    /// Synchronized with C++: GB_DEFAULT_MIN_FRAME_DELAY_MS.
    /// </summary>
    public const int DefaultMinFrameDelayMs = 10;

    /// <summary>
    /// The minimum render interval for the UI thread timer (16 ms = 60 FPS).
    /// This is the fastest the UI can be updated while staying responsive.
    /// Synchronized with C++: GB_MIN_RENDER_INTERVAL_MS.
    /// </summary>
    public const int MinRenderIntervalMs = 16;


    /// <summary>
    /// Rendering backend identifiers for GPU acceleration.
    /// </summary>
    public enum Backend
    {
        /// <summary>Dummy/software-only backend (always available, no GPU acceleration).</summary>
        Dummy = 0,

        /// <summary>DirectX 11 backend (requires Windows Vista+ with D3D11 compatible hardware).</summary>
        D3D11 = 1,

        /// <summary>Metal backend (requires macOS/iOS with Metal support).</summary>
        Metal = 2,

        /// <summary>DirectX 9Ex backend (requires Windows with D3D9Ex support, optimized for WPF D3DImage).</summary>
        D3D9Ex = 3,
    }

    private DecoderHandle? _decoder;
    private Backend _requestedBackend = Backend.Dummy;

    /// <summary>Gets or sets the percentage of frames to cache (0.0 to 1.0). Default is 0.25 (25%).</summary>
    /// <remarks>Applied when a GIF is loaded. Use SetMaxCachedFrames() to override with an absolute value.</remarks>
    public float CachePercentage { get; set; } = 0.1f;

    /// <summary>Gets or sets the minimum number of frames to cache. Default is 5.</summary>
    public uint MinCachedFrames { get; set; } = 5;

    /// <summary>Gets or sets the maximum number of frames to cache. Default is 10.</summary>
    public uint MaxCachedFrames { get; set; } = 10;

    /// <summary>Gets a value indicating whether playback is in progress.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Gets a value indicating whether the GIF loops indefinitely.</summary>
    public bool IsLooping { get; private set; }

    /// <summary>Gets or sets a value indicating whether background frame prefetching is enabled.</summary>
    /// <remarks>
    /// When enabled, frames ahead of the current playback position are decoded in background,
    /// reducing apparent latency during sequential playback. Default is true.
    /// </remarks>
    public bool EnablePrefetching { get; set; } = false;

    /// <summary>Gets the total number of frames in the GIF.</summary>
    public int FrameCount { get; private set; }

    /// <summary>Gets or sets the index of the current frame.</summary>
    public int CurrentFrame
    {
        get => this._currentFrame;
        set
        {
            this._currentFrame = value;

            // Update prefetch thread about current playback position
            if (this._decoder != null && this.EnablePrefetching)
            {
                Native.gb_decoder_set_current_frame(this._decoder.DangerousGetHandle(), value);
            }
        }
    }

    private int _currentFrame;

    /// <summary>Gets the width of the image in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the height of the image in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Gets a value indicating whether the GIF contains transparent pixels.</summary>
    /// <remarks>When true, GPU rendering (D3DImage) may not fully composite alpha channels correctly.</remarks>
    public bool HasTransparency
    {
        get
        {
            if (this._decoder == null)
            {
                return false;
            }

            return Native.gb_decoder_has_transparency(this._decoder.DangerousGetHandle()) != 0;
        }
    }

    /// <summary>Gets the native decoder handle for low-level P/Invoke operations.</summary>
    /// <remarks>
    /// Returns System.IntPtr.Zero if no GIF is loaded. This is useful for direct
    /// access to native decoder functions to reduce marshalling overhead.
    /// </remarks>
    public System.IntPtr NativeDecoderHandle
    {
        get => this._decoder?.DangerousGetHandle() ?? System.IntPtr.Zero;
    }



    /// <summary>
    /// Performs consolidated frame advancement with timing and repeat count management.
    /// This is more efficient than calling AdvanceFrame() and GetEffectiveFrameDelay() separately,
    /// as it performs all operations in a single P/Invoke call.
    /// </summary>
    /// <param name="currentFrame">The current frame index (0-based).</param>
    /// <param name="frameCount">The total number of frames in the GIF.</param>
    /// <param name="repeatCount">The current repeat count (-1 = infinite, 0 = stop, >0 = repeat N times).</param>
    /// <param name="rawFrameDelayMs">The raw frame delay from GIF metadata (in milliseconds).</param>
    /// <param name="minFrameDelayMs">The minimum frame delay threshold (in milliseconds).</param>
    /// <returns>A result containing the next frame, effective timing, and updated state.</returns>
    /// <exception cref="ArgumentException">Thrown if frameCount is less than 1.</exception>
    public static FrameAdvanceTimedResult AdvanceFrameTimed(int currentFrame, int frameCount, int repeatCount, int rawFrameDelayMs, int minFrameDelayMs = DefaultMinFrameDelayMs)
    {
        if (frameCount < 1)
        {
            throw new ArgumentException("frameCount must be at least 1", nameof(frameCount));
        }

        // Call consolidated C++ implementation
        return Native.gb_decoder_advance_frame_timed(currentFrame, frameCount, repeatCount, rawFrameDelayMs, minFrameDelayMs);
    }

    /// <summary>
    /// Creates a new GifPlayer with a specified rendering backend.
    /// </summary>
    /// <param name="backend">The rendering backend to use for GPU acceleration.</param>
    /// <returns>A new GifPlayer instance using the specified backend, or null if the backend is unavailable.</returns>
    /// <remarks>
    /// This factory method allows selecting a specific rendering backend (D3D11, D3D9Ex, Metal, or Dummy).
    /// If the requested backend is unavailable on the platform, null is returned.
    /// Use Backend.Dummy for guaranteed software-only rendering on any platform.
    /// </remarks>
    public static GifPlayer? CreateWithBackend(Backend backend)
    {
        var handle = Native.gb_decoder_create_with_backend((int)backend);
        if (handle == IntPtr.Zero)
        {
            // Get the actual error message from native code
            string errorMsg = Native.GB_decoder_get_last_error();
            if (string.IsNullOrEmpty(errorMsg))
            {
                errorMsg = "Backend initialization failed (no error message available)";
            }

            System.Diagnostics.Debug.WriteLine($"[GifBolt.Core] Backend {backend} failed: {errorMsg}");
            return null;
        }

        var player = new GifPlayer();
        player._requestedBackend = backend;
        player.AssignDecoder(new DecoderHandle(handle));
        return player;
    }

    /// <summary>Loads a GIF from the specified file path.</summary>
    /// <param name="path">The file path to the GIF image.</param>
    /// <returns>true if the GIF was loaded successfully; otherwise false.</returns>
    public bool Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return this.LoadDecoder(
            handle => Native.gb_decoder_load_from_path(handle.DangerousGetHandle(), path) != 0,
            $"path:{path}");
    }

    /// <summary>Loads a GIF from an in-memory byte buffer.</summary>
    /// <param name="data">The GIF data buffer.</param>
    /// <returns>true if the GIF was loaded successfully; otherwise false.</returns>
    public bool Load(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return false;
        }

        return this.LoadDecoder(
            handle => this.LoadFromMemory(handle, data),
            $"memory:{data.Length}b");
    }

    /// <summary>Loads a GIF from a stream by buffering it in memory.</summary>
    /// <param name="stream">The stream containing GIF data.</param>
    /// <returns>true if the GIF was loaded successfully; otherwise false.</returns>
    public bool Load(Stream stream)
    {
        if (stream == null)
        {
            return false;
        }

        try
        {
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return this.Load(memory.ToArray());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GifPlayer: stream load failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Starts playback of the GIF.</summary>
    public void Play() => this.IsPlaying = true;

    /// <summary>Pauses playback of the GIF.</summary>
    public void Pause() => this.IsPlaying = false;

    /// <summary>Stops playback and resets to the first frame.</summary>
    public void Stop()
    {
        this.IsPlaying = false;
        this.CurrentFrame = 0;
    }

    /// <summary>Gets the RGBA32 pixel data for the specified frame as a reference-counted buffer (zero-copy access).</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="pixelBuffer">The output pixel buffer. Must be disposed when finished.</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    /// <remarks>This method provides zero-copy access to pixel data. The caller must dispose the buffer when finished.</remarks>
    public bool TryGetFramePixelsRgba32Buffer(int frameIndex, out PixelBuffer pixelBuffer)
    {
        pixelBuffer = default;
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        IntPtr buffer;
        int byteCount;
        int result = Native.gb_decoder_get_frame_pixels_rgba32_buffer(
            this._decoder.DangerousGetHandle(), frameIndex, out buffer, out byteCount);
        
        if (result == 0 || buffer == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixelBuffer = new PixelBuffer(buffer, byteCount);
        return true;
    }

    /// <summary>Gets the BGRA32 premultiplied pixel data as a reference-counted buffer (zero-copy access).</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="pixelBuffer">The output pixel buffer. Must be disposed when finished.</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    /// <remarks>This method provides zero-copy access to pixel data and is optimized for Avalonia. The caller must dispose the buffer when finished.</remarks>
    public bool TryGetFramePixelsBgra32PremultipliedBuffer(int frameIndex, out PixelBuffer pixelBuffer)
    {
        pixelBuffer = default;
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        IntPtr buffer;
        int byteCount;
        int result = Native.gb_decoder_get_frame_pixels_bgra32_premultiplied_buffer(
            this._decoder.DangerousGetHandle(), frameIndex, out buffer, out byteCount);
        
        if (result == 0 || buffer == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixelBuffer = new PixelBuffer(buffer, byteCount);
        return true;
    }

    /// <summary>Gets the scaled BGRA32 premultiplied pixel data as a reference-counted buffer (zero-copy access).</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="targetWidth">The desired output width in pixels.</param>
    /// <param name="targetHeight">The desired output height in pixels.</param>
    /// <param name="pixelBuffer">The output pixel buffer. Must be disposed when finished.</param>
    /// <param name="outWidth">The actual output width in pixels.</param>
    /// <param name="outHeight">The actual output height in pixels.</param>
    /// <param name="filter">The scaling filter to use (Nearest, Bilinear, Bicubic, Lanczos).</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    /// <remarks>This method uses GPU acceleration and provides zero-copy access. The caller must dispose the buffer when finished.</remarks>
    public bool TryGetFramePixelsBgra32PremultipliedScaledBuffer(int frameIndex, int targetWidth, int targetHeight,
                                                                  out PixelBuffer pixelBuffer, out int outWidth, out int outHeight,
                                                                  ScalingFilter filter = ScalingFilter.Bilinear)
    {
        pixelBuffer = default;
        outWidth = 0;
        outHeight = 0;
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        IntPtr buffer;
        int byteCount;
        int result = Native.gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled_buffer(
            this._decoder.DangerousGetHandle(), frameIndex, targetWidth, targetHeight,
            out buffer, out outWidth, out outHeight, out byteCount, (int)filter);
        
        if (result == 0 || buffer == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixelBuffer = new PixelBuffer(buffer, byteCount);
        return true;
    }

    /// <summary>Gets the display duration of the specified frame.</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <returns>The frame delay in milliseconds.</returns>
    public int GetFrameDelayMs(int frameIndex)
    {
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return 0;
        }

        return Native.gb_decoder_get_frame_delay_ms(this._decoder.DangerousGetHandle(), frameIndex);
    }

    /// <summary>Gets or sets the minimum frame delay (in ms) for GIF playback.</summary>
    /// <remarks>Default is 10 ms. Used to enforce a lower bound on frame delays during playback.</remarks>
    public int MinFrameDelayMs
    {
        get => this._decoder != null ? Native.gb_decoder_get_min_frame_delay_ms(this._decoder.DangerousGetHandle()) : 0;
        set
        {
            if (this._decoder != null)
            {
                Native.gb_decoder_set_min_frame_delay_ms(this._decoder.DangerousGetHandle(), value);
            }
        }
    }

    /// <summary>Gets or sets the maximum number of frames to cache in memory.</summary>
    /// <remarks>Default is 10 frames. Larger values improve playback smoothness but consume more memory.</remarks>
    public uint MaxCachedFramesCount
    {
        get => this._decoder != null ? Native.gb_decoder_get_max_cached_frames(this._decoder.DangerousGetHandle()) : 0;
        set
        {
            if (this._decoder != null && value > 0)
            {
                Native.gb_decoder_set_max_cached_frames(this._decoder.DangerousGetHandle(), value);
            }
        }
    }

    /// <summary>
    /// Resets the canvas composition state for looping.
    /// </summary>
    /// <remarks>
    /// Call this when looping back to frame 0 to ensure proper frame composition
    /// without reloading the entire GIF.
    /// </remarks>
    public void ResetCanvas()
    {
        if (this._decoder != null)
        {
            Native.gb_decoder_reset_canvas(this._decoder.DangerousGetHandle());
        }
    }

    /// <summary>Gets the rendering backend type.</summary>
    /// <returns>Backend ID: 0=Dummy (Software), 1=DirectX 11, 2=Metal.</returns>
    public int GetBackend()
    {
        if (this._decoder == null)
        {
            return -1;
        }

        return Native.gb_decoder_get_backend(this._decoder.DangerousGetHandle());
    }

    /// <summary>Gets the native GPU texture pointer for zero-copy rendering.</summary>
    /// <param name="frameIndex">The frame index to get the texture for.</param>
    /// <returns>Native texture pointer (ID3D11Texture2D* or MTLTexture*), or IntPtr.Zero on error.</returns>
    /// <remarks>
    /// This allows direct access to the native GPU texture for advanced rendering scenarios.
    /// The returned pointer type depends on the backend: ID3D11Texture2D* for D3D11, MTLTexture* for Metal.
    /// </remarks>
    public IntPtr GetNativeTexturePtr(int frameIndex)
    {
        if (this._decoder == null)
        {
            return IntPtr.Zero;
        }

        return Native.gb_decoder_get_native_texture_ptr(this._decoder.DangerousGetHandle(), frameIndex);
    }
    /// <summary>Updates the GPU texture with the pixel data for the specified frame (for GPU-accelerated rendering).</summary>
    /// <param name="frameIndex">The frame index to render to the GPU texture.</param>
    /// <returns>true if the texture was updated successfully; false if no GPU texture is available or an error occurred.</returns>
    /// <remarks>
    /// This method should be called before displaying a frame on D3DImage. It ensures the 
    /// GPU texture is synchronized with the frame pixel data. For CPU rendering (WriteableBitmap),
    /// use TryGetFramePixels* methods instead.
    /// </remarks>
    public bool UpdateNativeTexture(int frameIndex)
    {
        if (this._decoder == null || this.FrameCount == 0)
        {
            return false;
        }

        // Wrap frame index to frame count to handle looping (defensive)
        // This ensures we never request a frame outside valid bounds
        int wrappedFrameIndex = frameIndex % this.FrameCount;
        if (wrappedFrameIndex < 0)
        {
            wrappedFrameIndex += this.FrameCount;
        }

        // Call the native update function to sync GPU texture with frame data
        // This is critical for GPU-accelerated rendering on D3D backends
        int result = Native.gb_decoder_update_gpu_texture(this._decoder.DangerousGetHandle(), wrappedFrameIndex);
        return result != 0;
    }

    /// <summary>
    /// Advances to the next frame and updates GPU texture (C++handles looping automatically).
    /// </summary>
    /// <returns>true if frame advanced and GPU texture updated; false on error.</returns>
    public bool AdvanceAndRenderFrame()
    {
        if (this._decoder == null)
        {
            return false;
        }

        // C++ handles frame advancement and wrapping internally
        int result = Native.gb_decoder_advance_and_update_gpu_texture(this._decoder.DangerousGetHandle());
        return result != 0;
    }

    /// <summary>
    /// Gets the native GPU texture pointer for the current frame (after advancing).
    /// </summary>
    /// <returns>Native texture pointer, or IntPtr.Zero on error.</returns>
    public IntPtr GetCurrentGpuTexturePtr()
    {
        if (this._decoder == null)
        {
            return IntPtr.Zero;
        }

        return Native.gb_decoder_get_current_gpu_texture_ptr(this._decoder.DangerousGetHandle());
    }

    /// <summary>
    /// Computes the repeat count based on the specified repeat behavior string.
    /// </summary>
    /// <param name="repeatBehavior">The repeat behavior</param>
    /// <returns></returns>
    public int ComputeRepeatCount(string repeatBehavior)
    {
        return Native.gb_decoder_compute_repeat_count(repeatBehavior, this.IsLooping ? 1 : 0);
    }

    /// <summary>
    /// Creates a new animation context for managing playback state.
    /// </summary>
    /// <param name="player">The GIF player instance (used to get frame count and loop count).</param>
    /// <param name="repeatBehavior">Optional repeat behavior override (null uses metadata).</param>
    /// <returns>A handle to the animation context, or System.IntPtr.Zero on error.</returns>
    public static System.IntPtr CreateAnimationContext(GifPlayer? player, string? repeatBehavior = null)
    {
        if (player == null || player.FrameCount < 1)
        {
            return System.IntPtr.Zero;
        }

        int loopCount = player.IsLooping ? -1 : 1;
        return Native.gb_animation_context_create(player.FrameCount, loopCount, repeatBehavior);
    }

    /// <summary>
    /// Destroys an animation context and releases resources.
    /// </summary>
    /// <param name="context">The animation context handle to destroy.</param>
    public static void DestroyAnimationContext(System.IntPtr context)
    {
        if (context != System.IntPtr.Zero)
        {
            Native.gb_animation_context_destroy(context);
        }
    }

    /// <summary>
    /// Gets the current animation state.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <returns>An AnimationState struct containing the current state.</returns>
    public static GifBolt.AnimationState GetAnimationState(System.IntPtr context)
    {
        return Native.gb_animation_context_get_state(context);
    }

    /// <summary>
    /// Sets the playback state.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <param name="isPlaying">true to start playing, false to pause/stop.</param>
    /// <param name="doReset">true to reset to frame 0, false to maintain current position.</param>
    public static void SetAnimationPlaying(System.IntPtr context, bool isPlaying, bool doReset = false)
    {
        if (context != System.IntPtr.Zero)
        {
            Native.gb_animation_context_set_playing(context, isPlaying ? 1 : 0, doReset ? 1 : 0);
        }
    }

    /// <summary>
    /// Advances to the next frame with consolidated state management.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <param name="rawFrameDelayMs">The raw frame delay from GIF metadata (in milliseconds).</param>
    /// <param name="minFrameDelayMs">The minimum frame delay threshold (in milliseconds).</param>
    /// <param name="result">Receives the updated animation state and timing info.</param>
    /// <returns>true if frame advanced successfully, false on error or completion.</returns>
    public static bool AdvanceAnimation(System.IntPtr context, int rawFrameDelayMs, int minFrameDelayMs, out GifBolt.AnimationAdvanceResult result)
    {
        result = default;
        if (context == System.IntPtr.Zero)
        {
            return false;
        }

        return Native.gb_animation_context_advance(context, rawFrameDelayMs, minFrameDelayMs, out result) != 0;
    }

    /// <summary>
    /// Sets a custom repeat count for the animation.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <param name="repeatCount">-1 for infinite, 0 to stop, >0 for specific repeat count.</param>
    public static void SetAnimationRepeatCount(System.IntPtr context, int repeatCount)
    {
        if (context != System.IntPtr.Zero)
        {
            Native.gb_animation_context_set_repeat_count(context, repeatCount);
        }
    }

    /// <summary>
    /// Gets the current frame index from the animation context.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <returns>The current frame index (0-based).</returns>
    public static int GetAnimationCurrentFrame(System.IntPtr context)
    {
        if (context == System.IntPtr.Zero)
        {
            return 0;
        }

        return Native.gb_animation_context_get_current_frame(context);
    }

    /// <summary>
    /// Sets the current frame index in the animation context.
    /// </summary>
    /// <param name="context">The animation context handle.</param>
    /// <param name="frameIndex">The frame index to set (0-based).</param>
    public static void SetAnimationCurrentFrame(System.IntPtr context, int frameIndex)
    {
        if (context != System.IntPtr.Zero)
        {
            Native.gb_animation_context_set_current_frame(context, frameIndex);
        }
    }

    /// <summary>Releases the unmanaged resources associated with the player.</summary>
    public void Dispose()
    {
        this.DisposeDecoder();
        GC.SuppressFinalize(this);
    }

    private void DisposeDecoder()
    {
        if (this._decoder != null)
        {
            // Stop prefetch thread before disposing
            Native.gb_decoder_stop_prefetching(this._decoder.DangerousGetHandle());
            this._decoder.Dispose();
            this._decoder = null;
        }
    }

    private bool LoadDecoder(Func<DecoderHandle, bool> loader, string debugContext)
    {
        // If decoder already exists (created with CreateWithBackend), reuse it
        // Otherwise create a new decoder using the requested backend type
        DecoderHandle tmp;
        if (this._decoder != null)
        {
            tmp = this._decoder;
        }
        else
        {
            this.DisposeDecoder();
            var handle = Native.gb_decoder_create_with_backend((int)this._requestedBackend);
            if (handle == IntPtr.Zero)
            {
                Debug.WriteLine($"GifPlayer: decoder create failed ({debugContext}).");
                return false;
            }
            tmp = new DecoderHandle(handle);
        }

        bool ok;
        try
        {
            ok = loader(tmp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GifPlayer: load threw ({debugContext}): {ex.Message}");
            if (this._decoder == null)  // Only dispose if we created it
            {
                tmp.Dispose();
            }
            return false;
        }

        if (!ok)
        {
            Debug.WriteLine($"GifPlayer: load failed ({debugContext}).");
            if (this._decoder == null)  // Only dispose if we created it
            {
                tmp.Dispose();
            }
            return false;
        }

        if (this._decoder == null)  // Only assign if we created it
        {
            this.AssignDecoder(tmp);
        }
        else
        {
            // Update dimensions for existing decoder (use consolidated query for efficiency)
            var metadata = Native.gb_decoder_get_metadata(this._decoder.DangerousGetHandle());
            this.Width = metadata.Width;
            this.Height = metadata.Height;
            this.FrameCount = metadata.FrameCount;
            this.IsLooping = metadata.LoopCount < 0;
            this.CurrentFrame = 0;
        }

        return true;
    }

    private bool LoadFromMemory(DecoderHandle handle, byte[] data)
    {
        var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            int ok = Native.gb_decoder_load_from_memory(
                handle.DangerousGetHandle(),
                pinned.AddrOfPinnedObject(),
                data.Length);
            return ok != 0;
        }
        finally
        {
            if (pinned.IsAllocated)
            {
                pinned.Free();
            }
        }
    }

    private void AssignDecoder(DecoderHandle handle)
    {
        this._decoder = handle;

        // Get all metadata in a single P/Invoke call for efficiency
        var metadata = Native.gb_decoder_get_metadata(this._decoder.DangerousGetHandle());
        this.Width = metadata.Width;
        this.Height = metadata.Height;
        this.FrameCount = metadata.FrameCount;
        this.IsLooping = metadata.LoopCount < 0;
        this.CurrentFrame = 0;

        // Calculate and set adaptive cache size based on frame count
        // This is done once during initialization via metadata, not repeatedly
        uint adaptiveCacheSize = Native.gb_decoder_calculate_adaptive_cache_size(
            this.FrameCount,
            this.CachePercentage,
            this.MinCachedFrames,
            this.MaxCachedFrames);
        Native.gb_decoder_set_max_cached_frames(this._decoder.DangerousGetHandle(), adaptiveCacheSize);

        if (this.EnablePrefetching)
        {
            Native.gb_decoder_start_prefetching(this._decoder.DangerousGetHandle(), 0);
        }
    }
}
