// <copyright file="Interop.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;

namespace GifBolt.Internal
{
    /// <summary>
    /// Image scaling filter types for resizing operations.
    /// </summary>
    public enum ScalingFilter
    {
        /// <summary>Nearest-neighbor (point) sampling - fastest, lowest quality.</summary>
        Nearest = 0,

        /// <summary>Bilinear interpolation - good balance of speed and quality.</summary>
        Bilinear = 1,

        /// <summary>Bicubic interpolation - higher quality, slower.</summary>
        Bicubic = 2,

        /// <summary>Lanczos resampling - highest quality, slowest.</summary>
        Lanczos = 3
    }

    /// <summary>
    /// P/Invoke declarations for the GifBolt.Native C API.
    /// Provides managed access to native decoder functions.
    /// </summary>
    internal static class Native
    {
#if WINDOWS
        private const string DllName = "GifBolt.Native";
#else
        private const string DllName = "GifBolt.Native";
#endif

        /// <summary>
        /// Creates a new decoder instance.
        /// </summary>
        /// <returns>A pointer to the created decoder, or <see cref="IntPtr.Zero"/> if creation failed.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr gb_decoder_create();

        /// <summary>
        /// Destroys a decoder instance and releases its resources.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder to destroy.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_destroy(IntPtr decoder);

        /// <summary>
        /// Loads a GIF image from the specified file path.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="path">The file system path to the GIF image.</param>
        /// <returns>A non-zero value if loading succeeded; 0 if loading failed.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gb_decoder_load_from_path(IntPtr decoder, string path);

        /// <summary>
        /// Gets the total number of frames in the loaded GIF.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>The number of frames, or 0 if no GIF is loaded.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_frame_count(IntPtr decoder);

        /// <summary>
        /// Gets the width of the GIF image in pixels.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>The image width in pixels, or 0 if no GIF is loaded.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_width(IntPtr decoder);

        /// <summary>
        /// Gets the height of the GIF image in pixels.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>The image height in pixels, or 0 if no GIF is loaded.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_height(IntPtr decoder);

        /// <summary>
        /// Gets the loop count from the GIF metadata.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>-1 for infinite looping, or a non-negative count for finite loops.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_loop_count(IntPtr decoder);

        /// <summary>
        /// Gets the display duration of a frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="index">The zero-based frame index.</param>
        /// <returns>The frame delay in milliseconds.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_frame_delay_ms(IntPtr decoder, int index);

        /// <summary>
        /// Gets the RGBA32 pixel data for a specific frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="index">The zero-based frame index.</param>
        /// <param name="byteCount">Output parameter receiving the size of the pixel buffer in bytes.</param>
        /// <returns>A pointer to the RGBA32 pixel buffer, or <see cref="IntPtr.Zero"/> if retrieval failed.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr gb_decoder_get_frame_pixels_rgba32(IntPtr decoder, int index, out int byteCount);

        /// <summary>
        /// Gets BGRA32 pixel data with premultiplied alpha for a specific frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="index">The zero-based frame index.</param>
        /// <param name="byteCount">Output parameter receiving the size of the pixel buffer in bytes.</param>
        /// <returns>A pointer to the BGRA32 premultiplied pixel buffer, or <see cref="IntPtr.Zero"/> if retrieval failed.</returns>
        /// <remarks>This is optimized for Avalonia and other frameworks requiring premultiplied alpha.</remarks>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr gb_decoder_get_frame_pixels_bgra32_premultiplied(IntPtr decoder, int index, out int byteCount);

        /// <summary>
        /// Gets the background color of the GIF.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>The background color as RGBA32 (0xAABBGGRR).</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint gb_decoder_get_background_color(IntPtr decoder);

        /// <summary>
        /// Sets the minimum frame delay (in ms) for GIF playback.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="minDelayMs">Minimum delay in milliseconds.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_set_min_frame_delay_ms(IntPtr decoder, int minDelayMs);

        /// <summary>
        /// Gets the minimum frame delay (in ms) for GIF playback.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <returns>Minimum delay in milliseconds.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_min_frame_delay_ms(IntPtr decoder);

        /// <summary>
        /// Gets BGRA32 pixel data with premultiplied alpha for a specific frame, scaled to target dimensions.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="index">The zero-based frame index.</param>
        /// <param name="targetWidth">The desired output width in pixels.</param>
        /// <param name="targetHeight">The desired output height in pixels.</param>
        /// <param name="outWidth">Output parameter receiving the actual output width.</param>
        /// <param name="outHeight">Output parameter receiving the actual output height.</param>
        /// <param name="byteCount">Output parameter receiving the size of the pixel buffer in bytes.</param>
        /// <param name="filterType">The scaling filter to use (0=Nearest, 1=Bilinear, 2=Bicubic, 3=Lanczos).</param>
        /// <returns>A pointer to the BGRA32 premultiplied scaled pixel buffer, or <see cref="IntPtr.Zero"/> if retrieval failed.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled(
            IntPtr decoder, int index, int targetWidth, int targetHeight,
            out int outWidth, out int outHeight, out int byteCount, int filterType);

        /// <summary>
        /// Gets the major version number of the native library.
        /// </summary>
        /// <returns>The major version number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_version_get_major();

        /// <summary>
        /// Gets the minor version number of the native library.
        /// </summary>
        /// <returns>The minor version number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_version_get_minor();

        /// <summary>
        /// Gets the patch version number of the native library.
        /// </summary>
        /// <returns>The patch version number.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_version_get_patch();

        /// <summary>
        /// Gets the full semantic version string of the native library.
        /// </summary>
        /// <returns>A version string (e.g., "1.0.0").</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr gb_version_get_string();

        /// <summary>
        /// Gets the packed integer version of the native library.
        /// </summary>
        /// <returns>The version as an integer (major * 10000 + minor * 100 + patch).</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_version_get_int();

        /// <summary>
        /// Checks if the native library version is at least the specified version.
        /// </summary>
        /// <param name="major">Minimum required major version.</param>
        /// <param name="minor">Minimum required minor version.</param>
        /// <param name="patch">Minimum required patch version.</param>
        /// <returns>1 if runtime version >= specified version; 0 otherwise.</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_version_check(int major, int minor, int patch);

        /// <summary>
        /// Starts background prefetching of frames ahead of the current playback position.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="startFrame">The frame to start prefetching from.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_start_prefetching(IntPtr decoder, int startFrame);

        /// <summary>
        /// Stops background prefetching and joins the prefetch thread.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_stop_prefetching(IntPtr decoder);

        /// <summary>
        /// Updates the current playback position for prefetch lookahead.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder instance.</param>
        /// <param name="currentFrame">The current frame being displayed.</param>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_set_current_frame(IntPtr decoder, int currentFrame);
    }
}
