// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>

namespace GifBolt
{

/// \enum ScalingFilter
/// \brief Image scaling filter types for resizing operations.
enum class ScalingFilter : uint8_t
{
    /// \brief Nearest-neighbor (point) sampling - fastest, lowest quality.
    Nearest = 0,

    /// \brief Bilinear interpolation - good balance of speed and quality.
    Bilinear = 1,

    /// \brief Bicubic interpolation - higher quality, slower.
    Bicubic = 2,

    /// \brief Lanczos resampling - highest quality, slowest.
    Lanczos = 3
};

}  // namespace GifBolt
