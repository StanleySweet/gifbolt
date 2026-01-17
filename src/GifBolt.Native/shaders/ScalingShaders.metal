// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <metal_stdlib>
using namespace metal;

/// Nearest-neighbor (point) sampling kernel
kernel void scaleNearest(
    texture2d<float, access::read> inputTexture [[texture(0)]],
    texture2d<float, access::write> outputTexture [[texture(1)]],
    uint2 gid [[thread_position_in_grid]])
{
    const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
    const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

    if (gid.x >= outputSize.x || gid.y >= outputSize.y) {
        return;
    }

    // Calculate source position
    const float xRatio = float(inputSize.x) / float(outputSize.x);
    const float yRatio = float(inputSize.y) / float(outputSize.y);

    const uint2 srcPos = uint2(uint(gid.x * xRatio), uint(gid.y * yRatio));

    // Read and write pixel
    const float4 color = inputTexture.read(srcPos);
    outputTexture.write(color, gid);
}

/// Bilinear interpolation sampling kernel
kernel void scaleBilinear(
    texture2d<float, access::read> inputTexture [[texture(0)]],
    texture2d<float, access::write> outputTexture [[texture(1)]],
    uint2 gid [[thread_position_in_grid]])
{
    const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
    const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

    if (gid.x >= outputSize.x || gid.y >= outputSize.y) {
        return;
    }

    // Calculate source position (float for interpolation)
    const float xRatio = float(inputSize.x) / float(outputSize.x);
    const float yRatio = float(inputSize.y) / float(outputSize.y);

    const float srcX = float(gid.x) * xRatio;
    const float srcY = float(gid.y) * yRatio;

    // Get integer and fractional parts
    const uint x0 = uint(srcX);
    const uint y0 = uint(srcY);
    const uint x1 = min(x0 + 1, inputSize.x - 1);
    const uint y1 = min(y0 + 1, inputSize.y - 1);

    const float fracX = srcX - float(x0);
    const float fracY = srcY - float(y0);

    // Read four surrounding pixels
    const float4 c00 = inputTexture.read(uint2(x0, y0));
    const float4 c10 = inputTexture.read(uint2(x1, y0));
    const float4 c01 = inputTexture.read(uint2(x0, y1));
    const float4 c11 = inputTexture.read(uint2(x1, y1));

    // Bilinear interpolation
    const float4 cTop = mix(c00, c10, fracX);
    const float4 cBottom = mix(c01, c11, fracX);
    const float4 result = mix(cTop, cBottom, fracY);

    outputTexture.write(result, gid);
}

/// Bicubic interpolation helper function
float cubicWeight(float x)
{
    const float a = -0.5; // Catmull-Rom parameter
    const float absX = abs(x);

    if (absX <= 1.0) {
        return ((a + 2.0) * absX - (a + 3.0)) * absX * absX + 1.0;
    } else if (absX < 2.0) {
        return ((a * absX - 5.0 * a) * absX + 8.0 * a) * absX - 4.0 * a;
    }
    return 0.0;
}

/// Bicubic interpolation sampling kernel
kernel void scaleBicubic(
    texture2d<float, access::read> inputTexture [[texture(0)]],
    texture2d<float, access::write> outputTexture [[texture(1)]],
    uint2 gid [[thread_position_in_grid]])
{
    const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
    const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

    if (gid.x >= outputSize.x || gid.y >= outputSize.y) {
        return;
    }

    // Calculate source position
    const float xRatio = float(inputSize.x) / float(outputSize.x);
    const float yRatio = float(inputSize.y) / float(outputSize.y);

    const float srcX = float(gid.x) * xRatio;
    const float srcY = float(gid.y) * yRatio;

    const int x = int(srcX);
    const int y = int(srcY);
    const float dx = srcX - float(x);
    const float dy = srcY - float(y);

    // Sample 4x4 neighborhood
    float4 result = float4(0.0);
    float weightSum = 0.0;

    for (int j = -1; j <= 2; ++j) {
        for (int i = -1; i <= 2; ++i) {
            const int sx = clamp(x + i, 0, int(inputSize.x) - 1);
            const int sy = clamp(y + j, 0, int(inputSize.y) - 1);

            const float wx = cubicWeight(float(i) - dx);
            const float wy = cubicWeight(float(j) - dy);
            const float weight = wx * wy;

            const float4 sample = inputTexture.read(uint2(sx, sy));
            result += sample * weight;
            weightSum += weight;
        }
    }

    if (weightSum > 0.0) {
        result /= weightSum;
    }

    outputTexture.write(result, gid);
}

/// Lanczos resampling helper function
float lanczosWeight(float x, float a)
{
    if (abs(x) < 0.001) {
        return 1.0;
    }
    if (abs(x) >= a) {
        return 0.0;
    }

    const float pi = 3.14159265359;
    const float piX = pi * x;
    return a * sin(piX) * sin(piX / a) / (piX * piX);
}

/// Lanczos resampling kernel
kernel void scaleLanczos(
    texture2d<float, access::read> inputTexture [[texture(0)]],
    texture2d<float, access::write> outputTexture [[texture(1)]],
    uint2 gid [[thread_position_in_grid]])
{
    const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
    const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

    if (gid.x >= outputSize.x || gid.y >= outputSize.y) {
        return;
    }

    const float a = 3.0; // Lanczos-3

    // Calculate source position
    const float xRatio = float(inputSize.x) / float(outputSize.x);
    const float yRatio = float(inputSize.y) / float(outputSize.y);

    const float srcX = float(gid.x) * xRatio;
    const float srcY = float(gid.y) * yRatio;

    const int x = int(srcX);
    const int y = int(srcY);
    const float dx = srcX - float(x);
    const float dy = srcY - float(y);

    // Sample neighborhood
    float4 result = float4(0.0);
    float weightSum = 0.0;

    const int radius = int(ceil(a));
    for (int j = -radius; j <= radius; ++j) {
        for (int i = -radius; i <= radius; ++i) {
            const int sx = clamp(x + i, 0, int(inputSize.x) - 1);
            const int sy = clamp(y + j, 0, int(inputSize.y) - 1);

            const float wx = lanczosWeight(float(i) - dx, a);
            const float wy = lanczosWeight(float(j) - dy, a);
            const float weight = wx * wy;

            const float4 sample = inputTexture.read(uint2(sx, sy));
            result += sample * weight;
            weightSum += weight;
        }
    }

    if (weightSum > 0.0) {
        result /= weightSum;
    }

    outputTexture.write(result, gid);
}
