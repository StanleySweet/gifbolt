// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

// Compute shader for RGBA to BGRA conversion with premultiplied alpha

cbuffer ConversionParams : register(b0)
{
    uint pixelCount;
    uint padding0;
    uint padding1;
    uint padding2;
};

// Input: RGBA32 pixels (R, G, B, A as 4 bytes)
StructuredBuffer<uint> inputRGBA : register(t0);

// Output: BGRA32 premultiplied pixels
RWStructuredBuffer<uint> outputBGRA : register(u0);

/// \brief Converts RGBA to BGRA with premultiplied alpha
/// Reads 4 bytes as uint (0xAABBGGRR), converts to BGRA with alpha premultiplication
[numthreads(256, 1, 1)]
void ConvertRGBAToBGRAPremultiplied(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint idx = dispatchThreadID.x;

    if (idx >= pixelCount)
        return;

    // Read RGBA pixel (stored as 0xAABBGGRR in little-endian)
    uint rgba = inputRGBA[idx];

    // Extract components (0-255)
    uint r = (rgba & 0x000000FF);
    uint g = (rgba & 0x0000FF00) >> 8;
    uint b = (rgba & 0x00FF0000) >> 16;
    uint a = (rgba & 0xFF000000) >> 24;

    // Premultiply RGB by alpha (alpha is 0-255, so divide by 255)
    // Use fast approximation: (x * a + 127) / 255 ≈ (x * a + 128) >> 8
    uint rPremul = (r * a + 128) >> 8;
    uint gPremul = (g * a + 128) >> 8;
    uint bPremul = (b * a + 128) >> 8;

    // Pack as BGRA (0xAARRGGBB for little-endian)
    uint bgra = bPremul | (gPremul << 8) | (rPremul << 16) | (a << 24);

    outputBGRA[idx] = bgra;
}

/// \brief Simple format conversion without premultiplication
[numthreads(256, 1, 1)]
void ConvertRGBAToBGRA(uint3 dispatchThreadID : SV_DispatchThreadID)
{
    uint idx = dispatchThreadID.x;

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
