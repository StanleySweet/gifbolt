// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>
#include <string>

#include "IDeviceCommandContext.h"

namespace GifBolt
{

class GifDecoder;

class GifBoltRenderer
{
   public:
    GifBoltRenderer();
    explicit GifBoltRenderer(std::shared_ptr<Renderer::IDeviceCommandContext> context);
    ~GifBoltRenderer();

    // Initialize the renderer
    bool Initialize(uint32_t width, uint32_t height);

    // Set the device context (allows swapping backends)
    void SetDeviceContext(std::shared_ptr<Renderer::IDeviceCommandContext> context);

    // Load a GIF from file or URL
    bool LoadGif(const std::string& path);

    // Playback control
    void Play();
    void Stop();
    void Pause();
    void SetLooping(bool loop);

    // Rendering
    bool Render();

    // Frame control
    void SetCurrentFrame(uint32_t frameIndex);
    uint32_t GetCurrentFrame() const;
    uint32_t GetFrameCount() const;

    // Getters
    uint32_t GetWidth() const;
    uint32_t GetHeight() const;

   private:
    class Impl;
    std::unique_ptr<Impl> pImpl;
};

}  // namespace GifBolt
