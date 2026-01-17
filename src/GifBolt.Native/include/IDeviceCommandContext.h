// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>

namespace GifBolt
{
namespace Renderer
{

enum class Backend
{
    DUMMY,  // For testing/cross-platform development
    D3D11   // DirectX 11 (WPF-compatible across targets)
};

class ITexture;
class IFramebuffer;

/**
 * DeviceCommandContext manages rendering commands and GPU resources.
 * Inspired by 0 A.D.'s architecture for backend abstraction.
 */
class IDeviceCommandContext
{
   public:
    virtual ~IDeviceCommandContext() = default;

    // Resource management
    virtual std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height,
                                                    const void* data, size_t dataSize) = 0;

    // Frame management
    virtual void BeginFrame() = 0;
    virtual void EndFrame() = 0;

    // Drawing
    virtual void Clear(float r, float g, float b, float a) = 0;
    virtual void DrawTexture(ITexture* texture, int x, int y, int width, int height) = 0;

    // State
    virtual void Flush() = 0;
    virtual Backend GetBackend() const = 0;
};

}  // namespace Renderer
}  // namespace GifBolt
