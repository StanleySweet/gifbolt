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

namespace GifBolt;

/// <summary>
/// Cross-platform GIF player for netstandard2.0.
/// Provides loading, playback control, and RGBA pixel access.
/// Platform wrappers (WPF, etc.) build on top of this.
/// </summary>
public sealed class GifPlayer : IDisposable
{
    private DecoderHandle? _decoder;

    /// <summary>Gets a value indicating whether playback is in progress.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Gets a value indicating whether the GIF loops indefinitely.</summary>
    public bool IsLooping { get; private set; }

    /// <summary>Gets or sets a value indicating whether background frame prefetching is enabled.</summary>
    /// <remarks>
    /// When enabled, frames ahead of the current playback position are decoded in background,
    /// reducing apparent latency during sequential playback. Default is true.
    /// </remarks>
    public bool EnablePrefetching { get; set; } = true;

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

    /// <summary>Gets the RGBA32 pixel data for the specified frame.</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="pixels">The output buffer containing RGBA32 pixel data.</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    public bool TryGetFramePixelsRgba32(int frameIndex, out byte[] pixels)
    {
        pixels = Array.Empty<byte>();
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        int byteCount;
        var ptr = Native.gb_decoder_get_frame_pixels_rgba32(this._decoder.DangerousGetHandle(), frameIndex, out byteCount);
        if (ptr == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, byteCount);
        return true;
    }

    /// <summary>Gets the BGRA32 pixel data with premultiplied alpha for the specified frame.</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="pixels">The output buffer containing BGRA32 premultiplied pixel data.</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    /// <remarks>This method is optimized for Avalonia and other frameworks requiring premultiplied alpha.</remarks>
    public bool TryGetFramePixelsBgra32Premultiplied(int frameIndex, out byte[] pixels)
    {
        pixels = Array.Empty<byte>();
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        int byteCount;
        var ptr = Native.gb_decoder_get_frame_pixels_bgra32_premultiplied(this._decoder.DangerousGetHandle(), frameIndex, out byteCount);
        if (ptr == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, byteCount);
        return true;
    }

    /// <summary>Gets the BGRA32 pixel data with premultiplied alpha for the specified frame, scaled to target dimensions using GPU.</summary>
    /// <param name="frameIndex">The index of the frame.</param>
    /// <param name="targetWidth">The desired output width in pixels.</param>
    /// <param name="targetHeight">The desired output height in pixels.</param>
    /// <param name="pixels">The output buffer containing BGRA32 premultiplied scaled pixel data.</param>
    /// <param name="outWidth">The actual output width in pixels.</param>
    /// <param name="outHeight">The actual output height in pixels.</param>
    /// <param name="filter">The scaling filter to use (Nearest, Bilinear, Bicubic, Lanczos).</param>
    /// <returns>true if the frame pixels were retrieved successfully; otherwise false.</returns>
    /// <remarks>This method uses GPU acceleration for high-quality bilinear scaling.</remarks>
    public bool TryGetFramePixelsBgra32PremultipliedScaled(int frameIndex, int targetWidth, int targetHeight,
                                                            out byte[] pixels, out int outWidth, out int outHeight,
                                                            Internal.ScalingFilter filter = Internal.ScalingFilter.Bilinear)
    {
        pixels = Array.Empty<byte>();
        outWidth = 0;
        outHeight = 0;
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
        {
            return false;
        }

        int byteCount;
        var ptr = Native.gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled(
            this._decoder.DangerousGetHandle(), frameIndex, targetWidth, targetHeight,
            out outWidth, out outHeight, out byteCount, (int)filter);
        if (ptr == IntPtr.Zero || byteCount <= 0)
        {
            return false;
        }

        pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, byteCount);
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

    /// <summary>
    /// Sets the minimum frame delay (in ms) for GIF playback.
    /// </summary>
    /// <param name="minDelayMs">Minimum delay in milliseconds.</param>
    public void SetMinFrameDelayMs(int minDelayMs)
    {
        if (this._decoder != null)
        {
            Native.gb_decoder_set_min_frame_delay_ms(this._decoder.DangerousGetHandle(), minDelayMs);
        }
    }

    /// <summary>
    /// Gets the minimum frame delay (in ms) for GIF playback.
    /// </summary>
    /// <returns>Minimum delay in milliseconds.</returns>
    public int GetMinFrameDelayMs()
    {
        if (this._decoder != null)
        {
            return Native.gb_decoder_get_min_frame_delay_ms(this._decoder.DangerousGetHandle());
        }
        return 0;
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
        this.DisposeDecoder();
        var handle = Native.gb_decoder_create();
        if (handle == IntPtr.Zero)
        {
            Debug.WriteLine($"GifPlayer: decoder create failed ({debugContext}).");
            return false;
        }

        var tmp = new DecoderHandle(handle);
        bool ok;
        try
        {
            ok = loader(tmp);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GifPlayer: load threw ({debugContext}): {ex.Message}");
            tmp.Dispose();
            return false;
        }

        if (!ok)
        {
            Debug.WriteLine($"GifPlayer: load failed ({debugContext}).");
            tmp.Dispose();
            return false;
        }

        this.AssignDecoder(tmp);
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
        this.Width = Native.gb_decoder_get_width(this._decoder.DangerousGetHandle());
        this.Height = Native.gb_decoder_get_height(this._decoder.DangerousGetHandle());
        this.FrameCount = Native.gb_decoder_get_frame_count(this._decoder.DangerousGetHandle());
        this.IsLooping = Native.gb_decoder_get_loop_count(this._decoder.DangerousGetHandle()) < 0;
        this.CurrentFrame = 0;

        if (this.EnablePrefetching)
        {
            Native.gb_decoder_start_prefetching(this._decoder.DangerousGetHandle(), 0);
        }
    }
}
