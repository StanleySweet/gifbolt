// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace GifBolt
{

/// \struct GifFrame
/// \brief Represents a single frame in a GIF image.
/// Contains pixel data and timing information for rendering.
struct GifFrame
{
    std::vector<uint32_t> pixels;  ///< RGBA pixel data (32 bits per pixel)
    uint32_t width;                ///< Frame width in pixels
    uint32_t height;               ///< Frame height in pixels
    uint32_t delayMs;              ///< Display duration in milliseconds
};

/// \class GifDecoder
/// \brief Decodes GIF images from files or URLs.
///
/// Provides functionality to load and decode GIF images, with support for
/// frame iteration, looping detection, and pixel data access.
class GifDecoder
{
   public:
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

   private:
    class Impl;
    std::unique_ptr<Impl> pImpl;  ///< Opaque implementation (Pimpl pattern)
};

}  // namespace GifBolt
