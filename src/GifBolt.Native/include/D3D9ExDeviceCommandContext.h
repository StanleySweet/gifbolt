// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include "IDeviceCommandContext.h"

namespace GifBolt
{
namespace Renderer
{

#ifdef _WIN32

class D3D9ExTexture;

/// \class D3D9ExDeviceCommandContext
/// \brief DirectX 9Ex implementation of IDeviceCommandContext with D3D11 interop.
///
/// Provides GPU-accelerated rendering using Direct3D 9Ex on Windows with shared
/// resources for zero-copy interop with D3D11. Enables WPF D3DImage integration
/// for maximum performance.
class D3D9ExDeviceCommandContext : public IDeviceCommandContext
{
   public:
    /// \brief Initializes a new instance of D3D9ExDeviceCommandContext.
    /// Sets up D3D9Ex device and enables D3D11 interop via shared resources.
    /// \throws std::runtime_error if Direct3D device creation fails.
    D3D9ExDeviceCommandContext();

    /// \brief Destroys the D3D9ExDeviceCommandContext and releases GPU resources.
    ~D3D9ExDeviceCommandContext() override;

    /// \brief Gets the backend type (always returns Backend::D3D9Ex).
    /// \return Backend::D3D9Ex
    Backend GetBackend() const override;

    /// \brief Creates a texture with D3D9Ex shared resource for D3D11 interop.
    /// \param width The texture width in pixels.
    /// \param height The texture height in pixels.
    /// \param rgba32Pixels Pointer to RGBA32 pixel data, or nullptr for empty texture.
    /// \param byteCount Size of the pixel data in bytes.
    /// \return A shared pointer to the created D3D9Ex texture.
    /// \throws std::runtime_error if texture creation fails.
    std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height,
                                            const void* rgba32Pixels, size_t byteCount) override;

    /// \brief Marks the beginning of a frame.
    void BeginFrame() override;

    /// \brief Clears the render target with the specified color.
    /// \param r Red channel (0.0 - 1.0).
    /// \param g Green channel (0.0 - 1.0).
    /// \param b Blue channel (0.0 - 1.0).
    /// \param a Alpha channel (0.0 - 1.0).
    void Clear(float r, float g, float b, float a) override;

    /// \brief Draws a texture at the specified position and size.
    /// \param texture The texture to draw.
    /// \param x The X coordinate in screen space.
    /// \param y The Y coordinate in screen space.
    /// \param width The width to draw the texture.
    /// \param height The height to draw the texture.
    void DrawTexture(ITexture* texture, int x, int y, int width, int height) override;

    /// \brief Marks the end of a frame.
    void EndFrame() override;

    /// \brief Flushes all pending rendering commands.
    void Flush() override;

    /// \brief Gets the D3D9Ex device pointer for advanced interop.
    /// \return Raw IDirect3DDevice9Ex* pointer. Do not release.
    void* GetD3D9ExDevice() const;

   private:
    struct Impl;
    Impl* _impl;  ///< Opaque implementation pointer
};

#endif  // _WIN32

}  // namespace Renderer
}  // namespace GifBolt
