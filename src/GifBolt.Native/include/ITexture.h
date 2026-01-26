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

    /// \brief Gets the native texture pointer for platform-specific interop.
    /// \return Platform-specific texture pointer:
    ///         - D3D11: ID3D11Texture2D*
    ///         - Metal: MTLTexture* (__bridge void*)
    ///         - Vulkan: VkImage (cast to void*)
    ///         - Dummy: nullptr
    /// \remarks Use GetBackend() to determine how to cast the returned pointer.
    ///          This enables zero-copy interop with platform UI frameworks
    ///          (WPF D3DImage, Avalonia GPU surfaces, etc.).
    virtual void* GetNativeTexturePtr() = 0;
};

}  // namespace Renderer
}  // namespace GifBolt
