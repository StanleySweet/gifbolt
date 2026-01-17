// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>

namespace GifBolt
{
namespace Renderer
{

enum class TextureFormat
{
    RGBA8,
    BGRA8,
    RGB8
};

/**
 * Texture interface for backend abstraction
 */
class ITexture
{
   public:
    virtual ~ITexture() = default;

    virtual uint32_t GetWidth() const = 0;
    virtual uint32_t GetHeight() const = 0;
    virtual TextureFormat GetFormat() const = 0;

    // Update texture data
    virtual bool Update(const void* data, size_t dataSize) = 0;
};

}  // namespace Renderer
}  // namespace GifBolt
