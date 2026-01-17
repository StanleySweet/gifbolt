// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>

namespace GifBolt
{
namespace Renderer
{
namespace PixelFormats
{

/// \enum Format
/// \brief Comprehensive pixel format enumeration for texture and framebuffer creation.
///
/// Provides a complete set of pixel formats for cross-platform rendering backends.
/// Inspired by Vulkan's VkFormat and modern rendering engines.
enum class Format
{
    UNDEFINED,  ///< Undefined/invalid format

    // 8-bit normalized formats
    R8_UNORM,        ///< Single-channel 8-bit unsigned normalized
    R8G8_UNORM,      ///< Two-channel 8-bit unsigned normalized
    R8G8_UINT,       ///< Two-channel 8-bit unsigned integer
    R8G8B8_UNORM,    ///< Three-channel 8-bit unsigned normalized (RGB)
    R8G8B8A8_UNORM,  ///< Four-channel 8-bit unsigned normalized (RGBA)
    R8G8B8A8_UINT,   ///< Four-channel 8-bit unsigned integer
    B8G8R8A8_UNORM,  ///< Four-channel 8-bit unsigned normalized (BGRA, DirectX native)

    // Legacy single-channel formats (deprecated, use R8_UNORM with swizzling)
    A8_UNORM,  ///< Alpha-only 8-bit format (legacy)
    L8_UNORM,  ///< Luminance-only 8-bit format (legacy)

    // 16-bit formats
    R16_UNORM,   ///< Single-channel 16-bit unsigned normalized
    R16_UINT,    ///< Single-channel 16-bit unsigned integer
    R16_SINT,    ///< Single-channel 16-bit signed integer
    R16_SFLOAT,  ///< Single-channel 16-bit floating point

    R16G16_UNORM,   ///< Two-channel 16-bit unsigned normalized
    R16G16_UINT,    ///< Two-channel 16-bit unsigned integer
    R16G16_SINT,    ///< Two-channel 16-bit signed integer
    R16G16_SFLOAT,  ///< Two-channel 16-bit floating point

    R16G16B16_SFLOAT,     ///< Three-channel 16-bit floating point (RGB)
    R16G16B16A16_SFLOAT,  ///< Four-channel 16-bit floating point (RGBA)

    // 32-bit floating point formats
    R32_SFLOAT,           ///< Single-channel 32-bit floating point
    R32G32_SFLOAT,        ///< Two-channel 32-bit floating point
    R32G32B32_SFLOAT,     ///< Three-channel 32-bit floating point (RGB)
    R32G32B32A32_SFLOAT,  ///< Four-channel 32-bit floating point (RGBA)

    // Depth and stencil formats
    D16_UNORM,           ///< 16-bit depth buffer
    D24_UNORM,           ///< 24-bit depth buffer
    D24_UNORM_S8_UINT,   ///< 24-bit depth + 8-bit stencil
    D32_SFLOAT,          ///< 32-bit floating point depth
    D32_SFLOAT_S8_UINT,  ///< 32-bit floating point depth + 8-bit stencil

    // Block compressed formats (texture compression)
    BC1_RGB_UNORM,   ///< BC1 compression (DXT1 RGB)
    BC1_RGBA_UNORM,  ///< BC1 compression with alpha (DXT1 RGBA)
    BC2_UNORM,       ///< BC2 compression (DXT3)
    BC3_UNORM        ///< BC3 compression (DXT5)
};

/// \brief Gets the size in bytes of a single pixel for the given format.
/// \param format The pixel format.
/// \return The size in bytes per pixel, or 0 for compressed or undefined formats.
inline uint32_t GetFormatBytesPerPixel(Format format)
{
    switch (format)
    {
        case Format::R8_UNORM:
        case Format::A8_UNORM:
        case Format::L8_UNORM:
            return 1;

        case Format::R8G8_UNORM:
        case Format::R8G8_UINT:
        case Format::R16_UNORM:
        case Format::R16_UINT:
        case Format::R16_SINT:
        case Format::R16_SFLOAT:
        case Format::D16_UNORM:
            return 2;

        case Format::R8G8B8_UNORM:
            return 3;

        case Format::R8G8B8A8_UNORM:
        case Format::R8G8B8A8_UINT:
        case Format::B8G8R8A8_UNORM:
        case Format::R16G16_UNORM:
        case Format::R16G16_UINT:
        case Format::R16G16_SINT:
        case Format::R16G16_SFLOAT:
        case Format::R32_SFLOAT:
        case Format::D24_UNORM:
        case Format::D24_UNORM_S8_UINT:
        case Format::D32_SFLOAT:
            return 4;

        case Format::R16G16B16_SFLOAT:
            return 6;

        case Format::R16G16B16A16_SFLOAT:
        case Format::R32G32_SFLOAT:
        case Format::D32_SFLOAT_S8_UINT:
            return 8;

        case Format::R32G32B32_SFLOAT:
            return 12;

        case Format::R32G32B32A32_SFLOAT:
            return 16;

        // Compressed formats and undefined return 0
        case Format::UNDEFINED:
        case Format::BC1_RGB_UNORM:
        case Format::BC1_RGBA_UNORM:
        case Format::BC2_UNORM:
        case Format::BC3_UNORM:
        default:
            return 0;
    }
}

/// \brief Checks if the format contains an alpha channel.
/// \param format The pixel format.
/// \return true if the format has an alpha channel; false otherwise.
inline bool HasAlphaChannel(Format format)
{
    switch (format)
    {
        case Format::R8G8B8A8_UNORM:
        case Format::R8G8B8A8_UINT:
        case Format::B8G8R8A8_UNORM:
        case Format::A8_UNORM:
        case Format::R16G16B16A16_SFLOAT:
        case Format::R32G32B32A32_SFLOAT:
        case Format::BC1_RGBA_UNORM:
        case Format::BC2_UNORM:
        case Format::BC3_UNORM:
            return true;

        default:
            return false;
    }
}

/// \brief Checks if the format is a depth or stencil format.
/// \param format The pixel format.
/// \return true if the format is depth or stencil; false otherwise.
inline bool IsDepthStencilFormat(Format format)
{
    switch (format)
    {
        case Format::D16_UNORM:
        case Format::D24_UNORM:
        case Format::D24_UNORM_S8_UINT:
        case Format::D32_SFLOAT:
        case Format::D32_SFLOAT_S8_UINT:
            return true;

        default:
            return false;
    }
}

/// \brief Checks if the format is block-compressed.
/// \param format The pixel format.
/// \return true if the format is compressed; false otherwise.
inline bool IsCompressedFormat(Format format)
{
    switch (format)
    {
        case Format::BC1_RGB_UNORM:
        case Format::BC1_RGBA_UNORM:
        case Format::BC2_UNORM:
        case Format::BC3_UNORM:
            return true;

        default:
            return false;
    }
}

}  // namespace PixelFormats
}  // namespace Renderer
}  // namespace GifBolt
