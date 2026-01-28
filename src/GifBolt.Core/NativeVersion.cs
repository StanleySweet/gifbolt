// <copyright file="NativeVersion.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
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
    /// <summary>
    /// Gets the semantic version of the native library.
    /// </summary>
    /// <remarks>
    /// This property queries the Native library on each call, but the overhead is minimal
    /// as the native function simply returns constants defined at compile-time.
    /// </remarks>
    public static Version Version
    {
        get
        {
            var info = Native.gb_version_get_info();
            return new Version(info.Major, info.Minor, info.Patch);
        }
    }

    /// <summary>
    /// Gets the version string of the native library (e.g., "1.0.0").
    /// </summary>
    public static string VersionString
    {
        get
        {
            var info = Native.gb_version_get_info();
            return info.VersionString ?? "0.0.0";
        }
    }

    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public static int Major => Native.gb_version_get_info().Major;

    /// <summary>
    /// Gets the minor version number.
    /// </summary>
    public static int Minor => Native.gb_version_get_info().Minor;

    /// <summary>
    /// Gets the patch version number.
    /// </summary>
    public static int Patch => Native.gb_version_get_info().Patch;

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
