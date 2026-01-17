// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <metal_stdlib>
using namespace metal;

/// \brief Converts RGBA to BGRA with premultiplied alpha on GPU
kernel void ConvertRGBAToBGRAPremultiplied(
    constant uint* inputRGBA [[buffer(0)]],
    device uint* outputBGRA [[buffer(1)]],
    constant uint& pixelCount [[buffer(2)]],
    uint idx [[thread_position_in_grid]])
{
    if (idx >= pixelCount)
        return;

    // Read RGBA pixel (stored as 0xAABBGGRR in little-endian)
    uint rgba = inputRGBA[idx];

    // Extract components (0-255)
    uint r = (rgba & 0x000000FF);
    uint g = (rgba & 0x0000FF00) >> 8;
    uint b = (rgba & 0x00FF0000) >> 16;
    uint a = (rgba & 0xFF000000) >> 24;

    // Premultiply RGB by alpha
    // Fast approximation: (x * a + 128) >> 8
    uint rPremul = (r * a + 128) >> 8;
    uint gPremul = (g * a + 128) >> 8;
    uint bPremul = (b * a + 128) >> 8;

    // Pack as BGRA (0xAARRGGBB for little-endian)
    uint bgra = bPremul | (gPremul << 8) | (rPremul << 16) | (a << 24);

    outputBGRA[idx] = bgra;
}

/// \brief Simple format conversion without premultiplication
kernel void ConvertRGBAToBGRA(
    constant uint* inputRGBA [[buffer(0)]],
    device uint* outputBGRA [[buffer(1)]],
    constant uint& pixelCount [[buffer(2)]],
    uint idx [[thread_position_in_grid]])
{
    if (idx >= pixelCount)
        return;

    uint rgba = inputRGBA[idx];

    uint r = (rgba & 0x000000FF);
    uint g = (rgba & 0x0000FF00) >> 8;
    uint b = (rgba & 0x00FF0000) >> 16;
    uint a = (rgba & 0xFF000000) >> 24;

    // Pack as BGRA without premultiplication
    uint bgra = b | (g << 8) | (r << 16) | (a << 24);

    outputBGRA[idx] = bgra;
}
