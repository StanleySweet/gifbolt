// <copyright file="Interop.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;

namespace GifBolt
{
    /// <summary>
    /// Image scaling filter types for resizing operations.
    /// </summary>
    public enum ScalingFilter
    {
        /// <summary>No scaling - display at native GIF resolution (fastest, pixel-perfect).</summary>
        None = -1,

        /// <summary>Nearest-neighbor (point) sampling - fastest scaling, lowest quality.</summary>
        Nearest = 0,

        /// <summary>Bilinear interpolation - good balance of speed and quality.</summary>
        Bilinear = 1,

        /// <summary>Bicubic interpolation - higher quality, slower.</summary>
        Bicubic = 2,

        /// <summary>Lanczos resampling - highest quality, slowest.</summary>
        Lanczos = 3,
    }
}

namespace GifBolt.Internal
{
    /// <summary>
    /// Manual P/Invoke for GifBolt.Native using LoadLibrary + GetProcAddress.
    /// Provides managed access to native decoder functions without delay-load dependencies.
    /// </summary>
    internal static class Native
    {
        private static IntPtr _hModule = IntPtr.Zero;
        private static GbDecoderCreateDelegate? _gbDecoderCreate;
        private static GbDecoderDestroyDelegate? _gbDecoderDestroy;
        private static GbDecoderLoadFromPathDelegate? _gbDecoderLoadFromPath;
        private static GbDecoderLoadFromMemoryDelegate? _gbDecoderLoadFromMemory;
        private static GbDecoderGetFrameCountDelegate? _gbDecoderGetFrameCount;
        private static GbDecoderGetWidthDelegate? _gbDecoderGetWidth;
        private static GbDecoderGetHeightDelegate? _gbDecoderGetHeight;
        private static GbDecoderGetLoopCountDelegate? _gbDecoderGetLoopCount;
        private static GbDecoderGetFrameDelayMsDelegate? _gbDecoderGetFrameDelayMs;
        private static GbDecoderGetFramePixelsRgba32Delegate? _gbDecoderGetFramePixelsRgba32;
        private static GbDecoderGetFramePixelsBgra32PremultipliedDelegate? _gbDecoderGetFramePixelsBgra32Premultiplied;
        private static GbDecoderGetBackgroundColorDelegate? _gbDecoderGetBackgroundColor;
        private static GbDecoderSetMinFrameDelayMsDelegate? _gbDecoderSetMinFrameDelayMs;
        private static GbDecoderGetMinFrameDelayMsDelegate? _gbDecoderGetMinFrameDelayMs;
        private static GbDecoderGetFramePixelsBgra32PremultipliedScaledDelegate? _gbDecoderGetFramePixelsBgra32PremultipliedScaled;
        private static GbVersionGetMajorDelegate? _gbVersionGetMajor;
        private static GbVersionGetMinorDelegate? _gbVersionGetMinor;
        private static GbVersionGetPatchDelegate? _gbVersionGetPatch;
        private static GbVersionGetStringDelegate? _gbVersionGetString;
        private static GbVersionGetIntDelegate? _gbVersionGetInt;
        private static GbVersionCheckDelegate? _gbVersionCheck;
        private static GbDecoderStartPrefetchingDelegate? _gbDecoderStartPrefetching;
        private static GbDecoderStopPrefetchingDelegate? _gbDecoderStopPrefetching;
        private static GbDecoderSetCurrentFrameDelegate? _gbDecoderSetCurrentFrame;

        // Static constructor to load DLL and resolve function pointers
        static Native()
        {
#if NET6_0_OR_GREATER
            // Cross-platform loader for .NET 6+
            LoadLibraryCrossPlatform();
#else
            // Windows-only loader for .NET Standard 2.0
            LoadLibraryWindows();
#endif

            // Resolve all function pointers
            _gbDecoderCreate = GetDelegate<GbDecoderCreateDelegate>("gb_decoder_create");
            _gbDecoderDestroy = GetDelegate<GbDecoderDestroyDelegate>("gb_decoder_destroy");
            _gbDecoderLoadFromPath = GetDelegate<GbDecoderLoadFromPathDelegate>("gb_decoder_load_from_path");
            _gbDecoderLoadFromMemory = GetDelegate<GbDecoderLoadFromMemoryDelegate>("gb_decoder_load_from_memory");
            _gbDecoderGetFrameCount = GetDelegate<GbDecoderGetFrameCountDelegate>("gb_decoder_get_frame_count");
            _gbDecoderGetWidth = GetDelegate<GbDecoderGetWidthDelegate>("gb_decoder_get_width");
            _gbDecoderGetHeight = GetDelegate<GbDecoderGetHeightDelegate>("gb_decoder_get_height");
            _gbDecoderGetLoopCount = GetDelegate<GbDecoderGetLoopCountDelegate>("gb_decoder_get_loop_count");
            _gbDecoderGetFrameDelayMs = GetDelegate<GbDecoderGetFrameDelayMsDelegate>("gb_decoder_get_frame_delay_ms");
            _gbDecoderGetFramePixelsRgba32 = GetDelegate<GbDecoderGetFramePixelsRgba32Delegate>("gb_decoder_get_frame_pixels_rgba32");
            _gbDecoderGetFramePixelsBgra32Premultiplied = GetDelegate<GbDecoderGetFramePixelsBgra32PremultipliedDelegate>("gb_decoder_get_frame_pixels_bgra32_premultiplied");
            _gbDecoderGetBackgroundColor = GetDelegate<GbDecoderGetBackgroundColorDelegate>("gb_decoder_get_background_color");
            _gbDecoderSetMinFrameDelayMs = GetDelegate<GbDecoderSetMinFrameDelayMsDelegate>("gb_decoder_set_min_frame_delay_ms");
            _gbDecoderGetMinFrameDelayMs = GetDelegate<GbDecoderGetMinFrameDelayMsDelegate>("gb_decoder_get_min_frame_delay_ms");
            _gbDecoderGetFramePixelsBgra32PremultipliedScaled = GetDelegate<GbDecoderGetFramePixelsBgra32PremultipliedScaledDelegate>("gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled");
            _gbVersionGetMajor = GetDelegate<GbVersionGetMajorDelegate>("gb_version_get_major");
            _gbVersionGetMinor = GetDelegate<GbVersionGetMinorDelegate>("gb_version_get_minor");
            _gbVersionGetPatch = GetDelegate<GbVersionGetPatchDelegate>("gb_version_get_patch");
            _gbVersionGetString = GetDelegate<GbVersionGetStringDelegate>("gb_version_get_string");
            _gbVersionGetInt = GetDelegate<GbVersionGetIntDelegate>("gb_version_get_int");
            _gbVersionCheck = GetDelegate<GbVersionCheckDelegate>("gb_version_check");
            _gbDecoderStartPrefetching = GetDelegate<GbDecoderStartPrefetchingDelegate>("gb_decoder_start_prefetching");
            _gbDecoderStopPrefetching = GetDelegate<GbDecoderStopPrefetchingDelegate>("gb_decoder_stop_prefetching");
            _gbDecoderSetCurrentFrame = GetDelegate<GbDecoderSetCurrentFrameDelegate>("gb_decoder_set_current_frame");
            _gbVersionGetMajor?.Invoke();

        }

        // Delegate definitions matching native function signatures
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GbDecoderCreateDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GbDecoderDestroyDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate int GbDecoderLoadFromPathDelegate(IntPtr decoder, string path);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderLoadFromMemoryDelegate(IntPtr decoder, IntPtr buffer, int length);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetFrameCountDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetWidthDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetHeightDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetLoopCountDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetFrameDelayMsDelegate(IntPtr decoder, int index);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GbDecoderGetFramePixelsRgba32Delegate(IntPtr decoder, int index, out int byteCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GbDecoderGetFramePixelsBgra32PremultipliedDelegate(IntPtr decoder, int index, out int byteCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint GbDecoderGetBackgroundColorDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GbDecoderSetMinFrameDelayMsDelegate(IntPtr decoder, int minDelayMs);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbDecoderGetMinFrameDelayMsDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:Parameters should be on same line or separate lines", Justification = "<En attente>")]
        private delegate IntPtr GbDecoderGetFramePixelsBgra32PremultipliedScaledDelegate(
            IntPtr decoder, int index, int targetWidth, int targetHeight,
            out int outWidth, out int outHeight, out int byteCount, int filterType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbVersionGetMajorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbVersionGetMinorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbVersionGetPatchDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private delegate IntPtr GbVersionGetStringDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbVersionGetIntDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GbVersionCheckDelegate(int major, int minor, int patch);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GbDecoderStartPrefetchingDelegate(IntPtr decoder, int startFrame);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GbDecoderStopPrefetchingDelegate(IntPtr decoder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GbDecoderSetCurrentFrameDelegate(IntPtr decoder, int currentFrame);

#if NET6_0_OR_GREATER
        /// <summary>
        /// Cross-platform native library loading for .NET 6+.
        /// Searches in multiple standard locations before throwing.
        /// </summary>
        private static void LoadLibraryCrossPlatform()
        {
            const string baseName = "GifBolt.Native";
            string libraryName;
            string[] searchPaths;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libraryName = $"{baseName}.dll";
                searchPaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libraryName),
                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Native).Assembly.Location) ?? string.Empty, libraryName),
                    libraryName,
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libraryName = $"lib{baseName}.dylib";
                searchPaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libraryName),
                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Native).Assembly.Location) ?? string.Empty, libraryName),
                    System.IO.Path.Combine(Environment.CurrentDirectory, libraryName),
                    libraryName,
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libraryName = $"lib{baseName}.so";
                searchPaths = new[]
                {
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, libraryName),
                    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Native).Assembly.Location) ?? string.Empty, libraryName),
                    System.IO.Path.Combine(Environment.CurrentDirectory, libraryName),
                    libraryName,
                };
            }
            else
            {
                throw new PlatformNotSupportedException($"Unsupported platform for GifBolt.Native");
            }

            DllNotFoundException? lastException = null;
            foreach (string searchPath in searchPaths)
            {
                try
                {
                    _hModule = NativeLibrary.Load(searchPath);
                    return;
                }
                catch (DllNotFoundException ex)
                {
                    lastException = ex;
                }
            }


            // If we get here, we couldn't load from any path
            throw new DllNotFoundException(
                $"Could not load {libraryName}. Searched in:\n" +
                string.Join("\n", searchPaths) +
                $"\n\nMake sure the native library is built and copied to the output directory.\n" +
                $"Build the native library with: cmake --build build --config Debug",
                lastException);
        }

        /// <summary>
        /// Gets a delegate for an unmanaged function exported by the native library.
        /// </summary>
        private static T GetDelegate<T>(string symbol) where T : Delegate
        {
            IntPtr addr = NativeLibrary.GetExport(_hModule, symbol);

            return Marshal.GetDelegateForFunctionPointer<T>(addr);
        }
#else
        // Windows-only native library loading for .NET Standard 2.0
        private static void LoadLibraryWindows()
        {
            const string dllName = "GifBolt.Native";

            // Try multiple paths to find the native DLL
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(Native).Assembly.Location) ?? string.Empty,
                    dllName + ".dll"),
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    dllName + ".dll"),
                System.IO.Path.Combine(
                    Environment.CurrentDirectory,
                    dllName + ".dll"),
                dllName + ".dll",
            };

            bool loaded = false;
            foreach (string dllPath in searchPaths)
            {
                if (string.IsNullOrEmpty(dllPath))
                {
                    continue;
                }

                if (System.IO.File.Exists(dllPath) || dllPath == dllName + ".dll")
                {
                    IntPtr result = LoadLibrary(dllPath);
                    if (result != IntPtr.Zero)
                    {
                        _hModule = result;
                        loaded = true;
                        break;
                    }
                }
            }

            if (!loaded || _hModule == IntPtr.Zero)
            {
                throw new DllNotFoundException($"Could not load {dllName}.dll from any search path.");
            }
        }

        /// <summary>
        /// Gets a delegate for an unmanaged function exported by the native library.
        /// </summary>
        private static T GetDelegate<T>(string symbol) where T : Delegate
        {
            IntPtr addr = GetProcAddress(_hModule, symbol);
            if (addr == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException($"Could not find symbol '{symbol}' in native library");
            }

            return Marshal.GetDelegateForFunctionPointer<T>(addr);
        }
#endif

        // Kernel32 functions for Windows-only manual DLL loading
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);


        /// <summary>
        /// Creates a new GifBolt decoder instance.
        /// </summary>
        /// <returns>Pointer to the newly created decoder.</returns>
        internal static IntPtr gb_decoder_create() => _gbDecoderCreate();

        /// <summary>
        /// Destroys a GifBolt decoder instance.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder to destroy.</param>
        internal static void gb_decoder_destroy(IntPtr decoder) => _gbDecoderDestroy(decoder);

        /// <summary>
        /// Loads a GIF from a file path into the decoder.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="path">File path of the GIF to load.</param>
        /// <returns>Status code (0 = success, non-zero = error).</returns>
        internal static int gb_decoder_load_from_path(IntPtr decoder, string path) => _gbDecoderLoadFromPath(decoder, path);

           /// <summary>
           /// Loads a GIF from an in-memory buffer into the decoder.
           /// </summary>
           /// <param name="decoder">Pointer to the decoder.</param>
           /// <param name="buffer">Pointer to the GIF data buffer.</param>
           /// <param name="length">Length of the buffer in bytes.</param>
           /// <returns>Status code (0 = success, non-zero = error).</returns>
           internal static int gb_decoder_load_from_memory(IntPtr decoder, IntPtr buffer, int length)
               => _gbDecoderLoadFromMemory(decoder, buffer, length);

        /// <summary>
        /// Gets the number of frames in the loaded GIF.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Total frame count.</returns>
        internal static int gb_decoder_get_frame_count(IntPtr decoder) => _gbDecoderGetFrameCount(decoder);

        /// <summary>
        /// Gets the width of the GIF frames.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Frame width in pixels.</returns>
        internal static int gb_decoder_get_width(IntPtr decoder) => _gbDecoderGetWidth(decoder);

        /// <summary>
        /// Gets the height of the GIF frames.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Frame height in pixels.</returns>
        internal static int gb_decoder_get_height(IntPtr decoder) => _gbDecoderGetHeight(decoder);

        /// <summary>
        /// Gets the loop count of the GIF.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Number of times the GIF should loop (0 = infinite).</returns>
        internal static int gb_decoder_get_loop_count(IntPtr decoder) => _gbDecoderGetLoopCount(decoder);

        /// <summary>
        /// Gets the delay of a specific frame in milliseconds.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="index">Index of the frame.</param>
        /// <returns>Frame delay in milliseconds.</returns>
        internal static int gb_decoder_get_frame_delay_ms(IntPtr decoder, int index) => _gbDecoderGetFrameDelayMs(decoder, index);

        /// <summary>
        /// Gets the raw RGBA32 pixel data for a frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="index">Index of the frame.</param>
        /// <param name="byteCount">Returns the number of bytes in the pixel buffer.</param>
        /// <returns>Pointer to the RGBA32 pixel data.</returns>
        internal static IntPtr gb_decoder_get_frame_pixels_rgba32(IntPtr decoder, int index, out int byteCount)
             => _gbDecoderGetFramePixelsRgba32(decoder, index, out byteCount);

        /// <summary>
        /// Gets the raw BGRA32 premultiplied pixel data for a frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="index">Index of the frame.</param>
        /// <param name="byteCount">Returns the number of bytes in the pixel buffer.</param>
        /// <returns>Pointer to the BGRA32 premultiplied pixel data.</returns>
        internal static IntPtr gb_decoder_get_frame_pixels_bgra32_premultiplied(IntPtr decoder, int index, out int byteCount)
             => _gbDecoderGetFramePixelsBgra32Premultiplied(decoder, index, out byteCount);

        /// <summary>
        /// Gets the background color of the GIF.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Background color as 0xAARRGGBB.</returns>
        internal static uint gb_decoder_get_background_color(IntPtr decoder) => _gbDecoderGetBackgroundColor(decoder);

        /// <summary>
        /// Sets the minimum frame delay for the decoder.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="minDelayMs">Minimum frame delay in milliseconds.</param>
        internal static void gb_decoder_set_min_frame_delay_ms(IntPtr decoder, int minDelayMs)
             => _gbDecoderSetMinFrameDelayMs(decoder, minDelayMs);

        /// <summary>
        /// Gets the minimum frame delay for the decoder.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <returns>Minimum frame delay in milliseconds.</returns>
        internal static int gb_decoder_get_min_frame_delay_ms(IntPtr decoder)
             => _gbDecoderGetMinFrameDelayMs(decoder);

        /// <summary>
        /// Gets scaled BGRA32 premultiplied pixel data for a frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="index">Index of the frame.</param>
        /// <param name="targetWidth">Desired width of the output frame.</param>
        /// <param name="targetHeight">Desired height of the output frame.</param>
        /// <param name="outWidth">Returns the width of the scaled frame.</param>
        /// <param name="outHeight">Returns the height of the scaled frame.</param>
        /// <param name="byteCount">Returns the number of bytes in the pixel buffer.</param>
        /// <param name="filterType">Scaling filter type (e.g., nearest, bilinear).</param>
        /// <returns>Pointer to the scaled BGRA32 premultiplied pixel data.</returns>
        internal static IntPtr gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled(
            IntPtr decoder,
            int index,
            int targetWidth,
            int targetHeight,
            out int outWidth,
            out int outHeight,
            out int byteCount,
            int filterType)
             => _gbDecoderGetFramePixelsBgra32PremultipliedScaled(decoder, index, targetWidth, targetHeight, out outWidth, out outHeight, out byteCount, filterType);

        /// <summary>
        /// Gets the major version of GifBolt.
        /// </summary>
        /// <returns>Major version number.</returns>
        internal static int gb_version_get_major() => _gbVersionGetMajor();

        /// <summary>
        /// Gets the minor version of GifBolt.
        /// </summary>
        /// <returns>Minor version number.</returns>
        internal static int gb_version_get_minor() => _gbVersionGetMinor();

        /// <summary>
        /// Gets the patch version of GifBolt.
        /// </summary>
        /// <returns>Patch version number.</returns>
        internal static int gb_version_get_patch() => _gbVersionGetPatch();

        /// <summary>
        /// Gets the version string of GifBolt.
        /// </summary>
        /// <returns>Pointer to a null-terminated version string.</returns>
        internal static IntPtr gb_version_get_string() => _gbVersionGetString();

        /// <summary>
        /// Gets the version as a single integer (major*10000 + minor*100 + patch).
        /// </summary>
        /// <returns>Integer representation of the version.</returns>
        internal static int gb_version_get_int() => _gbVersionGetInt();

        /// <summary>
        /// Checks if the current GifBolt version is at least the given version.
        /// </summary>
        /// <param name="major">Major version.</param>
        /// <param name="minor">Minor version.</param>
        /// <param name="patch">Patch version.</param>
        /// <returns>1 if current version >= specified version, 0 otherwise.</returns>
        internal static int gb_version_check(int major, int minor, int patch) => _gbVersionCheck(major, minor, patch);

        /// <summary>
        /// Starts prefetching frames from a specified start frame.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="startFrame">Index of the frame to start prefetching from.</param>
        internal static void gb_decoder_start_prefetching(IntPtr decoder, int startFrame)
             => _gbDecoderStartPrefetching(decoder, startFrame);

        /// <summary>
        /// Stops prefetching frames.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        internal static void gb_decoder_stop_prefetching(IntPtr decoder)
             => _gbDecoderStopPrefetching(decoder);

        /// <summary>
        /// Sets the current frame index of the decoder.
        /// </summary>
        /// <param name="decoder">Pointer to the decoder.</param>
        /// <param name="currentFrame">Frame index to set as current.</param>
        internal static void gb_decoder_set_current_frame(IntPtr decoder, int currentFrame)
             => _gbDecoderSetCurrentFrame(decoder, currentFrame);
    }
}
