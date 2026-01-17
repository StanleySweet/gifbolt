// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

/// \file gifbolt_version.h
/// \brief Version information for GifBolt.Native library.
///
/// This header defines version constants and macros for querying
/// the GifBolt.Native library version at compile-time and runtime.

#pragma once

#ifdef __cplusplus
extern "C"
{
#endif

/// \def GIFBOLT_VERSION_MAJOR
/// \brief Major version number (incompatible API changes).
#define GIFBOLT_VERSION_MAJOR 1

/// \def GIFBOLT_VERSION_MINOR
/// \brief Minor version number (backwards-compatible functionality added).
#define GIFBOLT_VERSION_MINOR 0

/// \def GIFBOLT_VERSION_PATCH
/// \brief Patch version number (backwards-compatible bug fixes).
#define GIFBOLT_VERSION_PATCH 0

/// \def GIFBOLT_VERSION_STRING
/// \brief Full semantic version string (e.g., "1.0.0").
#define GIFBOLT_VERSION_STRING "1.0.0"

/// \def GIFBOLT_VERSION_INT
/// \brief Packed integer version (major * 10000 + minor * 100 + patch).
/// Useful for compile-time version checks.
#define GIFBOLT_VERSION_INT \
    (GIFBOLT_VERSION_MAJOR * 10000 + GIFBOLT_VERSION_MINOR * 100 + GIFBOLT_VERSION_PATCH)

/// \def GIFBOLT_VERSION_CHECK
/// \brief Compile-time version comparison macro.
/// \param major Major version to check against.
/// \param minor Minor version to check against.
/// \param patch Patch version to check against.
/// \return 1 if current version >= specified version; 0 otherwise.
#define GIFBOLT_VERSION_CHECK(major, minor, patch) \
    (GIFBOLT_VERSION_INT >= ((major) * 10000 + (minor) * 100 + (patch)))

#if defined(_WIN32) || defined(_WIN64)
#ifdef GIFBOLT_NATIVE_EXPORTS
#define GB_API __declspec(dllexport)
#else
#define GB_API __declspec(dllimport)
#endif
#else
#define GB_API
#endif

    /// \brief Gets the major version number at runtime.
    /// \return The major version number.
    GB_API int gb_version_get_major(void) noexcept;

    /// \brief Gets the minor version number at runtime.
    /// \return The minor version number.
    GB_API int gb_version_get_minor(void) noexcept;

    /// \brief Gets the patch version number at runtime.
    /// \return The patch version number.
    GB_API int gb_version_get_patch(void) noexcept;

    /// \brief Gets the full semantic version string at runtime.
    /// \return A null-terminated version string (e.g., "1.0.0").
    ///         The string is statically allocated and must not be freed.
    GB_API const char* gb_version_get_string(void) noexcept;

    /// \brief Gets the packed integer version at runtime.
    /// \return The version as an integer (major * 10000 + minor * 100 + patch).
    GB_API int gb_version_get_int(void) noexcept;

    /// \brief Checks if the runtime version is at least the specified version.
    /// \param major Minimum required major version.
    /// \param minor Minimum required minor version.
    /// \param patch Minimum required patch version.
    /// \return 1 if runtime version >= specified version; 0 otherwise.
    GB_API int gb_version_check(int major, int minor, int patch) noexcept;

#ifdef __cplusplus
}
#endif
