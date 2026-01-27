// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>
#include <string>

#include <cstddef>

#include "IDeviceCommandContext.h"

namespace GifBolt
{

class GifDecoder;

/// \class GifBoltRenderer
/// \brief High-performance GPU-accelerated GIF renderer.
///
/// Provides rendering capabilities with pluggable device backends (DirectX 11, dummy).
/// Handles GIF decoding, frame timing, looping, and GPU-accelerated rendering.
class GifBoltRenderer
{
   public:
    /// \brief Initializes a new instance of GifBoltRenderer with default context.
    GifBoltRenderer();

    /// \brief Initializes a new instance of GifBoltRenderer with a specific device context.
    /// \param context The rendering device context to use.
    explicit GifBoltRenderer(std::shared_ptr<Renderer::IDeviceCommandContext> context);

    /// \brief Initializes a new instance of GifBoltRenderer with a specified backend.
    /// \param backend The rendering backend to use (D3D9Ex, D3D11, Metal, Dummy).
    /// \throws std::runtime_error if the specified backend is not available on this platform.
    explicit GifBoltRenderer(Renderer::Backend backend);

    /// \brief Destroys the GifBoltRenderer and releases associated resources.
    ~GifBoltRenderer();

    /// \brief Initializes the renderer with the specified dimensions.
    /// \param width The width of the rendering surface in pixels.
    /// \param height The height of the rendering surface in pixels.
    /// \return true if initialization succeeded; false otherwise.
    bool Initialize(uint32_t width, uint32_t height);

    /// \brief Sets the device context, allowing runtime backend switching.
    /// \param context The new rendering device context to use.
    void SetDeviceContext(std::shared_ptr<Renderer::IDeviceCommandContext> context);

    /// \brief Loads a GIF from a file path or URL.
    /// \param path The file path or URL to the GIF image.
    /// \return true if the GIF was loaded successfully; false otherwise.
    bool LoadGif(const std::string& path);

    /// \brief Loads a GIF from an in-memory buffer.
    /// \param data Pointer to GIF data.
    /// \param length Length of the data buffer in bytes.
    /// \return true if the GIF was loaded successfully; false otherwise.
    bool LoadGifFromMemory(const uint8_t* data, std::size_t length);

    /// \brief Starts playback of the loaded GIF.
    void Play();

    /// \brief Stops playback and resets to the first frame.
    void Stop();

    /// \brief Pauses playback at the current frame.
    void Pause();

    /// \brief Sets whether the GIF loops indefinitely.
    /// \param loop true to enable looping; false for single playback.
    void SetLooping(bool loop);

    /// \brief Renders the current frame.
    /// \return true if rendering succeeded; false otherwise.
    bool Render();

    /// \brief Sets the current frame index.
    /// \param frameIndex The zero-based index of the frame to display.
    void SetCurrentFrame(uint32_t frameIndex);

    /// \brief Gets the current frame index.
    /// \return The zero-based index of the currently displayed frame.
    uint32_t GetCurrentFrame() const;

    /// \brief Gets the total number of frames in the loaded GIF.
    /// \return The frame count, or 0 if no GIF is loaded.
    uint32_t GetFrameCount() const;

    /// \brief Gets the width of the GIF image.
    /// \return The width in pixels, or 0 if no GIF is loaded.
    uint32_t GetWidth() const;

    /// \brief Gets the height of the GIF image.
    /// \return The height in pixels, or 0 if no GIF is loaded.
    uint32_t GetHeight() const;

   private:
    class Impl;
    std::unique_ptr<Impl> pImpl;  ///< Opaque implementation (Pimpl pattern)
};

}  // namespace GifBolt
