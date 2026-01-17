// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>

#include "PixelFormat.h"

namespace GifBolt
{
namespace Renderer
{

/// \class ITexture
/// \brief Abstract interface for GPU textures.
///
/// Provides backend-agnostic texture interface for rendering abstraction.
class ITexture
{
   public:
    /// \brief Virtual destructor for proper cleanup of derived classes.
    virtual ~ITexture() = default;

    /// \brief Gets the width of the texture.
    /// \return The texture width in pixels.
    virtual uint32_t GetWidth() const = 0;

    /// \brief Gets the height of the texture.
    /// \return The texture height in pixels.
    virtual uint32_t GetHeight() const = 0;

    /// \brief Gets the pixel format of the texture.
    /// \return The PixelFormats::Format enum value.
    virtual PixelFormats::Format GetFormat() const = 0;

    /// \brief Updates the texture with new pixel data.
    /// \param data Pointer to the pixel data.
    /// \param dataSize Size of the pixel data in bytes.
    /// \return true if the update succeeded; false otherwise.
    virtual bool Update(const void* data, size_t dataSize) = 0;
};

}  // namespace Renderer
}  // namespace GifBolt
