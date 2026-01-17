// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <cstring>
#include <memory>
#include <thread>
#include <vector>

#include "IDeviceCommandContext.h"
#include "PixelFormat.h"

namespace GifBolt
{
namespace Renderer
{
namespace PixelFormats
{

// Threshold for enabling multi-threading (pixels)
// Below this, single-threaded is faster due to thread overhead
constexpr size_t THREADING_THRESHOLD = 100000;  // ~316x316 image

// Thread pool size (reuse threads to avoid creation/destruction overhead)
constexpr unsigned int MAX_WORKER_THREADS = 8;

/// \brief Converts RGBA pixels to BGRA format.
/// \param source Source buffer containing RGBA pixel data.
/// \param dest Destination buffer for BGRA pixel data (must be pre-allocated).
/// \param pixelCount Number of pixels to convert.
inline void ConvertRGBAToBGRA(const uint8_t* source, uint8_t* dest, size_t pixelCount)
{
    for (size_t i = 0; i < pixelCount; ++i)
    {
        const size_t offset = i * 4;
        dest[offset + 0] = source[offset + 2];  // B
        dest[offset + 1] = source[offset + 1];  // G
        dest[offset + 2] = source[offset + 0];  // R
        dest[offset + 3] = source[offset + 3];  // A
    }
}

/// \brief Converts BGRA pixels to RGBA format.
/// \param source Source buffer containing BGRA pixel data.
/// \param dest Destination buffer for RGBA pixel data (must be pre-allocated).
/// \param pixelCount Number of pixels to convert.
inline void ConvertBGRAToRGBA(const uint8_t* source, uint8_t* dest, size_t pixelCount)
{
    // BGRA to RGBA is symmetric with RGBA to BGRA
    ConvertRGBAToBGRA(source, dest, pixelCount);
}

/// \brief Premultiplies alpha in RGBA format.
/// \param pixels Pixel buffer containing RGBA data (modified in-place).
/// \param pixelCount Number of pixels to process.
inline void PremultiplyAlphaRGBA(uint8_t* pixels, size_t pixelCount)
{
    for (size_t i = 0; i < pixelCount; ++i)
    {
        const size_t offset = i * 4;
        uint8_t& r = pixels[offset + 0];
        uint8_t& g = pixels[offset + 1];
        uint8_t& b = pixels[offset + 2];
        uint8_t alpha = pixels[offset + 3];

        if (alpha == 0)
        {
            // Fully transparent: zero out RGB to avoid color bleed
            r = 0;
            g = 0;
            b = 0;
        }
        else if (alpha < 255)
        {
            // Premultiply RGB by alpha
            const float alphaFactor = alpha / 255.0f;
            r = static_cast<uint8_t>(r * alphaFactor);
            g = static_cast<uint8_t>(g * alphaFactor);
            b = static_cast<uint8_t>(b * alphaFactor);
        }
        // If alpha == 255, no premultiplication needed
    }
}

/// \brief Helper function to process a chunk of pixels for premultiplication.
/// \param pixels Pixel buffer containing BGRA data.
/// \param start Starting pixel index.
/// \param end Ending pixel index (exclusive).
inline void PremultiplyAlphaBGRAChunk(uint8_t* pixels, size_t start, size_t end)
{
    for (size_t i = start; i < end; ++i)
    {
        const size_t offset = i * 4;
        uint8_t& b = pixels[offset + 0];
        uint8_t& g = pixels[offset + 1];
        uint8_t& r = pixels[offset + 2];
        uint8_t alpha = pixels[offset + 3];

        if (alpha == 0)
        {
            // Fully transparent: zero out RGB to avoid color bleed
            b = 0;
            g = 0;
            r = 0;
        }
        else if (alpha < 255)
        {
            // Premultiply RGB by alpha
            const float alphaFactor = alpha / 255.0f;
            r = static_cast<uint8_t>(r * alphaFactor);
            g = static_cast<uint8_t>(g * alphaFactor);
            b = static_cast<uint8_t>(b * alphaFactor);
        }
        // If alpha == 255, no premultiplication needed
    }
}

/// \brief Premultiplies alpha in BGRA format.
/// \param pixels Pixel buffer containing BGRA data (modified in-place).
/// \param pixelCount Number of pixels to process.
inline void PremultiplyAlphaBGRA(uint8_t* pixels, size_t pixelCount)
{
    // Use single-threaded for small images
    if (pixelCount < THREADING_THRESHOLD)
    {
        PremultiplyAlphaBGRAChunk(pixels, 0, pixelCount);
        return;
    }

    // Multi-threaded for large images
    const unsigned int hardwareThreads = std::thread::hardware_concurrency();
    const unsigned int numThreads =
        (hardwareThreads > 0) ? std::min(hardwareThreads, MAX_WORKER_THREADS) : 4;

    const size_t pixelsPerThread = pixelCount / numThreads;
    const size_t remainderPixels = pixelCount % numThreads;

    std::vector<std::thread> threads;
    threads.reserve(numThreads);

    size_t startPixel = 0;
    for (unsigned int t = 0; t < numThreads; ++t)
    {
        size_t chunkSize = pixelsPerThread + (t < remainderPixels ? 1 : 0);
        size_t endPixel = startPixel + chunkSize;

        threads.emplace_back(PremultiplyAlphaBGRAChunk, pixels, startPixel, endPixel);
        startPixel = endPixel;
    }

    for (auto& thread : threads)
    {
        thread.join();
    }
}

/// \brief Legacy single-threaded version (kept for compatibility).
inline void PremultiplyAlphaBGRA_SingleThreaded(uint8_t* pixels, size_t pixelCount)
{
    for (size_t i = 0; i < pixelCount; ++i)
    {
        const size_t offset = i * 4;
        uint8_t& b = pixels[offset + 0];
        uint8_t& g = pixels[offset + 1];
        uint8_t& r = pixels[offset + 2];
        uint8_t alpha = pixels[offset + 3];

        if (alpha == 0)
        {
            // Fully transparent: zero out RGB to avoid color bleed
            b = 0;
            g = 0;
            r = 0;
        }
        else if (alpha < 255)
        {
            // Premultiply RGB by alpha
            const float alphaFactor = alpha / 255.0f;
            b = static_cast<uint8_t>(b * alphaFactor);
            g = static_cast<uint8_t>(g * alphaFactor);
            r = static_cast<uint8_t>(r * alphaFactor);
        }
        // If alpha == 255, no premultiplication needed
    }
}

/// \brief Worker function for threaded RGBA to BGRA premultiplied conversion.
/// \param source Source buffer containing RGBA pixel data.
/// \param dest Destination buffer for BGRA premultiplied pixel data.
/// \param startPixel Starting pixel index (inclusive).
/// \param endPixel Ending pixel index (exclusive).
inline void ConvertRGBAToBGRAPremultipliedChunk(const uint8_t* source, uint8_t* dest,
                                                size_t startPixel, size_t endPixel)
{
    for (size_t i = startPixel; i < endPixel; ++i)
    {
        const size_t offset = i * 4;
        const uint8_t r = source[offset + 0];
        const uint8_t g = source[offset + 1];
        const uint8_t b = source[offset + 2];
        const uint8_t alpha = source[offset + 3];

        if (alpha == 0)
        {
            // Fully transparent: zero out RGB to avoid color bleed
            dest[offset + 0] = 0;  // B
            dest[offset + 1] = 0;  // G
            dest[offset + 2] = 0;  // R
            dest[offset + 3] = 0;  // A
        }
        else if (alpha < 255)
        {
            // Premultiply RGB by alpha and swap R/B channels
            const float alphaFactor = alpha / 255.0f;
            dest[offset + 0] = static_cast<uint8_t>(b * alphaFactor);  // B
            dest[offset + 1] = static_cast<uint8_t>(g * alphaFactor);  // G
            dest[offset + 2] = static_cast<uint8_t>(r * alphaFactor);  // R
            dest[offset + 3] = alpha;                                  // A
        }
        else
        {
            // Fully opaque: just swap R/B channels, no premultiplication
            dest[offset + 0] = b;      // B
            dest[offset + 1] = g;      // G
            dest[offset + 2] = r;      // R
            dest[offset + 3] = alpha;  // A
        }
    }
}

/// \brief Converts RGBA to BGRA with premultiplied alpha in a single pass (multi-threaded).
/// \param source Source buffer containing RGBA pixel data.
/// \param dest Destination buffer for BGRA premultiplied pixel data (must be pre-allocated).
/// \param pixelCount Number of pixels to convert.
/// \param deviceContext Optional GPU device context for hardware acceleration.
///
/// Automatically uses multi-threading for large images (>100k pixels).
/// If deviceContext is provided and supports compute shaders, uses GPU acceleration.
/// This is more efficient than calling ConvertRGBAToBGRA followed by PremultiplyAlphaBGRA.
inline void ConvertRGBAToBGRAPremultiplied(const uint8_t* source, uint8_t* dest, size_t pixelCount,
                                           IDeviceCommandContext* deviceContext = nullptr)
{
    // Try GPU acceleration first if available
    if (deviceContext)
    {
        if (deviceContext->ConvertRGBAToBGRAPremultipliedGPU(source, dest,
                                                             static_cast<uint32_t>(pixelCount)))
        {
            return;  // GPU conversion succeeded
        }
        // Fall through to CPU if GPU failed
    }

    // Use single-threaded for small images (thread overhead not worth it)
    if (pixelCount < THREADING_THRESHOLD)
    {
        ConvertRGBAToBGRAPremultipliedChunk(source, dest, 0, pixelCount);
        return;
    }

    // Determine optimal thread count
    const unsigned int hardwareThreads = std::thread::hardware_concurrency();
    const unsigned int numThreads = (hardwareThreads > 0) ? hardwareThreads : 4;

    // Divide work into chunks
    const size_t pixelsPerThread = pixelCount / numThreads;
    const size_t remainderPixels = pixelCount % numThreads;

    // Pre-allocate thread vector to exact size needed
    std::vector<std::thread> threads;
    threads.reserve(numThreads);  // Avoid reallocations during emplace_back

    size_t startPixel = 0;
    for (unsigned int t = 0; t < numThreads; ++t)
    {
        // Distribute remainder pixels to first threads
        size_t chunkSize = pixelsPerThread + (t < remainderPixels ? 1 : 0);
        size_t endPixel = startPixel + chunkSize;

        threads.emplace_back(ConvertRGBAToBGRAPremultipliedChunk, source, dest, startPixel,
                             endPixel);

        startPixel = endPixel;
    }

    // Wait for all threads to complete
    for (auto& thread : threads)
    {
        thread.join();
    }
}

/// \brief Converts pixel data from one format to another.
/// \param source Source buffer containing pixel data.
/// \param sourceFormat The format of the source pixels.
/// \param dest Destination buffer (must be pre-allocated).
/// \param destFormat The desired destination format.
/// \param pixelCount Number of pixels to convert.
/// \param premultiplyAlpha If true and dest has alpha, premultiply alpha.
/// \return true if conversion succeeded; false if formats are incompatible.
inline bool ConvertPixelFormat(const uint8_t* source, Format sourceFormat, uint8_t* dest,
                               Format destFormat, size_t pixelCount, bool premultiplyAlpha = false)
{
    // Same format: direct copy
    if (sourceFormat == destFormat)
    {
        const size_t sourceBytes = GetFormatBytesPerPixel(sourceFormat) * pixelCount;
        std::memcpy(dest, source, sourceBytes);

        if (premultiplyAlpha && HasAlphaChannel(destFormat))
        {
            if (destFormat == Format::R8G8B8A8_UNORM)
            {
                PremultiplyAlphaRGBA(dest, pixelCount);
            }
            else if (destFormat == Format::B8G8R8A8_UNORM)
            {
                PremultiplyAlphaBGRA(dest, pixelCount);
            }
        }
        return true;
    }

    // RGBA8 <-> BGRA8 conversions
    if (sourceFormat == Format::R8G8B8A8_UNORM && destFormat == Format::B8G8R8A8_UNORM)
    {
        if (premultiplyAlpha)
        {
            ConvertRGBAToBGRAPremultiplied(source, dest, pixelCount);
        }
        else
        {
            ConvertRGBAToBGRA(source, dest, pixelCount);
        }
        return true;
    }

    if (sourceFormat == Format::B8G8R8A8_UNORM && destFormat == Format::R8G8B8A8_UNORM)
    {
        ConvertBGRAToRGBA(source, dest, pixelCount);
        if (premultiplyAlpha)
        {
            PremultiplyAlphaRGBA(dest, pixelCount);
        }
        return true;
    }

    // Unsupported conversion
    return false;
}

}  // namespace PixelFormats
}  // namespace Renderer
}  // namespace GifBolt
