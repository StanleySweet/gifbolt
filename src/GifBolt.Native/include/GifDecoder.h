// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

#include "ScalingFilter.h"

namespace GifBolt
{

/// \enum DisposalMethod
/// \brief Specifies how to dispose of a frame before rendering the next one.
enum class DisposalMethod : uint8_t
{
    None = 0,               ///< No disposal specified (leave pixels as-is)
    DoNotDispose = 1,       ///< Leave frame pixels in place
    RestoreBackground = 2,  ///< Clear frame area to background color
    RestorePrevious = 3     ///< Restore to previous frame state
};

/// \struct GifFrame
/// \brief Represents a single frame in a GIF image.
/// Contains pixel data and timing information for rendering.
struct GifFrame
{
    std::vector<uint32_t> pixels;  ///< RGBA pixel data (32 bits per pixel)
    uint32_t width;                ///< Frame width in pixels
    uint32_t height;               ///< Frame height in pixels
    uint32_t offsetX;              ///< Frame horizontal offset within canvas
    uint32_t offsetY;              ///< Frame vertical offset within canvas
    uint32_t delayMs;              ///< Display duration in milliseconds
    DisposalMethod disposal;       ///< Frame disposal method
    int32_t transparentIndex;      ///< Index of transparent color (-1 if none)
};

/// \class GifDecoder
/// \brief Decodes GIF images from files or URLs.
///
/// Provides functionality to load and decode GIF images, with support for
/// frame iteration, looping detection, and pixel data access.
class GifDecoder
{
   public:
    /// \brief Définit le délai minimal appliqué à chaque frame GIF (en ms).
    /// \param minDelayMs Délai minimal en millisecondes.
    void SetMinFrameDelayMs(uint32_t minDelayMs);

    /// \brief Obtient le délai minimal appliqué à chaque frame GIF (en ms).
    /// \return Délai minimal en millisecondes.
    uint32_t GetMinFrameDelayMs() const;
    /// \brief Initializes a new instance of the GifDecoder class.
    GifDecoder();

    /// \brief Destroys the GifDecoder and releases associated resources.
    ~GifDecoder();

    /// \brief Loads a GIF image from a file path.
    /// \param filePath The file system path to the GIF image.
    /// \return true if the GIF was loaded successfully; false otherwise.
    bool LoadFromFile(const std::string& filePath);

    /// \brief Loads a GIF image from a URL.
    /// \param url The URL to the GIF image.
    /// \return true if the GIF was loaded successfully; false otherwise.
    bool LoadFromUrl(const std::string& url);

    /// \brief Gets the total number of frames in the GIF.
    /// \return The number of frames, or 0 if no GIF is loaded.
    uint32_t GetFrameCount() const;

    /// \brief Gets the frame data at the specified index.
    /// \param index The zero-based index of the frame.
    /// \return A reference to the GifFrame at the specified index.
    /// \throws std::out_of_range if index >= GetFrameCount().
    const GifFrame& GetFrame(uint32_t index) const;

    /// \brief Gets the width of the GIF image.
    /// \return The width in pixels, or 0 if no GIF is loaded.
    uint32_t GetWidth() const;

    /// \brief Gets the height of the GIF image.
    /// \return The height in pixels, or 0 if no GIF is loaded.
    uint32_t GetHeight() const;

    /// \brief Determines whether the GIF loops indefinitely.
    /// \return true if the GIF should loop; false otherwise.
    bool IsLooping() const;

    /// \brief Gets the background color of the GIF.
    /// \return The background color as RGBA32 (0xAABBGGRR).
    uint32_t GetBackgroundColor() const;

    /// \brief Gets BGRA pixel data with premultiplied alpha for the specified frame.
    /// \param index The zero-based index of the frame.
    /// \return A pointer to BGRA32 premultiplied pixel data, or nullptr on error.
    ///         The data is cached internally and valid until the next call to this function.
    const uint8_t* GetFramePixelsBGRA32Premultiplied(uint32_t index);

    /// \brief Gets BGRA pixel data with premultiplied alpha for the specified frame, scaled to
    /// target dimensions.
    /// \param index The zero-based index of the frame.
    /// \param targetWidth The desired output width in pixels.
    /// \param targetHeight The desired output height in pixels.
    /// \param outWidth Output parameter receiving the actual output width.
    /// \param outHeight Output parameter receiving the actual output height.
    /// \param filter The scaling filter to use (Nearest, Bilinear, Bicubic, Lanczos).
    /// \return A pointer to BGRA32 premultiplied scaled pixel data, or nullptr on error.
    ///         The data is cached internally and valid until the next call to this function.
    const uint8_t* GetFramePixelsBGRA32PremultipliedScaled(
        uint32_t index, uint32_t targetWidth, uint32_t targetHeight, uint32_t& outWidth,
        uint32_t& outHeight, ScalingFilter filter = ScalingFilter::Bilinear);

    /// \brief Starts background prefetching of frames ahead of the current playback position.
    /// \param startFrame The frame to start prefetching from.
    /// \remarks This starts a background thread that decodes frames ahead of playback,
    ///          reducing latency for sequential frame access.
    void StartPrefetching(uint32_t startFrame);

    /// \brief Stops background prefetching and joins the prefetch thread.
    void StopPrefetching();

    /// \brief Updates the current playback position for prefetch lookahead.
    /// \param currentFrame The current frame being displayed.
    /// \remarks The prefetch thread uses this to determine which frames to decode next.
    void SetCurrentFrame(uint32_t currentFrame);

   private:
    class Impl;
    std::unique_ptr<Impl> _pImpl;  ///< Opaque implementation (Pimpl pattern)
};

}  // namespace GifBolt
