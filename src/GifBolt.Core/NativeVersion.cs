// <copyright file="NativeVersion.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using GifBolt.Internal;

namespace GifBolt;

/// <summary>
/// Provides version information for the GifBolt.Native library.
/// </summary>
/// <remarks>
/// All properties are thread-safe and use lazy initialization with caching
/// for optimal performance in multi-threaded scenarios.
/// </remarks>
public static class NativeVersion
{
    private static readonly Lazy<Version> _cachedVersion = new Lazy<Version>(() =>
    {
        int major = Native.gb_version_get_major();
        int minor = Native.gb_version_get_minor();
        int patch = Native.gb_version_get_patch();
        return new Version(major, minor, patch);
    });

    /// <summary>
    /// Gets the semantic version of the native library.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe and cached after the first access.
    /// </remarks>
    public static Version Version => _cachedVersion.Value;

    /// <summary>
    /// Gets the version string of the native library (e.g., "1.0.0").
    /// </summary>
    public static string VersionString
    {
        get
        {
            IntPtr ptr = Native.gb_version_get_string();
            if (ptr == IntPtr.Zero)
            {
                return "0.0.0";
            }
            return Marshal.PtrToStringAnsi(ptr) ?? "0.0.0";
        }
    }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public static int Major => Native.gb_version_get_major();

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public static int Minor => Native.gb_version_get_minor();

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public static int Patch => Native.gb_version_get_patch();

    /// <summary>
    /// Checks if the native library version meets the minimum required version.
    /// </summary>
    /// <param name="major">Minimum required major version.</param>
    /// <param name="minor">Minimum required minor version.</param>
    /// <param name="patch">Minimum required patch version.</param>
    /// <returns>true if the native library version is greater than or equal to the specified version; otherwise false.</returns>
    public static bool IsAtLeast(int major, int minor, int patch)
    {
        return Native.gb_version_check(major, minor, patch) != 0;
    }

    /// <summary>
    /// Checks if the native library version meets the minimum required version.
    /// </summary>
    /// <param name="requiredVersion">The minimum required version.</param>
    /// <returns>true if the native library version is greater than or equal to the specified version; otherwise false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredVersion"/> is null.</exception>
    public static bool IsAtLeast(Version requiredVersion)
    {
        if (requiredVersion == null)
        {
            throw new ArgumentNullException(nameof(requiredVersion));
        }

        return Native.gb_version_check(
            requiredVersion.Major,
            requiredVersion.Minor,
            requiredVersion.Build >= 0 ? requiredVersion.Build : 0) != 0;
    }
}
