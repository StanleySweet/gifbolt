// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>

#include "ITexture.h"

namespace GifBolt
{
namespace Renderer
{

#ifdef _WIN32

#include "PixelFormat.h"

/// \class D3D9ExTexture
/// \brief DirectX 9Ex texture with D3D11 interop via shared resources.
///
/// Manages both D3D11 and D3D9Ex textures, synchronizing data between them
/// for zero-copy interop with WPF D3DImage.
class D3D9ExTexture : public ITexture
{
   public:
    /// \brief Creates a D3D9Ex texture with optional shared resource for D3D11 interop.
    /// \param d3d9Device The D3D9Ex device.
    /// \param width The texture width in pixels.
    /// \param height The texture height in pixels.
    /// \param initialData Optional initial pixel data (RGBA32).
    /// \param initialDataSize Size of the initial data in bytes.
    /// \throws std::runtime_error if texture creation fails.
    D3D9ExTexture(void* d3d9Device, uint32_t width, uint32_t height,
                  const void* initialData, size_t initialDataSize);

    /// \brief Destroys the texture and releases GPU resources.
    ~D3D9ExTexture() override;

    /// \brief Gets the texture width.
    uint32_t GetWidth() const override;

    /// \brief Gets the texture height.
    uint32_t GetHeight() const override;

    /// \brief Gets the pixel format (always BGRA32).
    PixelFormats::Format GetFormat() const override;

    /// \brief Updates the texture with new pixel data.
    /// Converts RGBA32 to BGRA32 premultiplied and uploads to both D3D9Ex and D3D11.
    /// \param data Pointer to RGBA32 pixel data.
    /// \param dataSize Size of the pixel data in bytes.
    /// \return true if the update succeeded; false otherwise.
    bool Update(const void* data, size_t dataSize) override;

    /// \brief Gets the native texture pointer (IDirect3DSurface9*).
    /// \return Raw IDirect3DSurface9* for D3DImage interop.
    void* GetNativeTexturePtr() override;

    /// \brief Gets the shared resource handle for D3D11 interop.
    /// \return HANDLE to the shared resource, or nullptr if not supported.
    void* GetSharedResourceHandle() const;

    /// \brief Gets the D3D11 texture pointer (ID3D11Texture2D*).
    /// \return Raw ID3D11Texture2D* for direct GPU access.
    void* GetD3D11TexturePtr() const;

   private:
    struct Impl;
    std::unique_ptr<Impl> _impl;
};

#endif  // _WIN32

}  // namespace Renderer
}  // namespace GifBolt
