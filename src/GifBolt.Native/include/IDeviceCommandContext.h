// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <cstdint>
#include <memory>

namespace GifBolt
{
namespace Renderer
{

/// \enum Backend
/// \brief Rendering backend type.
enum class Backend
{
    DUMMY,  ///< Dummy/testing backend for cross-platform development
    D3D11,  ///< DirectX 11 backend (WPF-compatible)
    Metal   ///< Apple Metal backend (macOS/iOS Avalonia-compatible)
};

class ITexture;
class IFramebuffer;

/// \class IDeviceCommandContext
/// \brief Abstract interface for rendering device commands and GPU resource management.
///
/// Inspired by 0 A.D.'s architecture, provides backend-agnostic rendering abstraction.
/// Enables pluggable rendering backends (DirectX 11, dummy, etc.) for maximum flexibility.
class IDeviceCommandContext
{
   public:
    /// \brief Virtual destructor for proper cleanup of derived classes.
    virtual ~IDeviceCommandContext() = default;

    /// \brief Creates a texture with the specified properties.
    /// \param width The texture width in pixels.
    /// \param height The texture height in pixels.
    /// \param data Pointer to pixel data (RGBA32 format), or nullptr for empty texture.
    /// \param dataSize Size of the pixel data in bytes.
    /// \return A shared pointer to the created texture.
    /// \throws std::runtime_error if texture creation fails.
    virtual std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height,
                                                    const void* data, size_t dataSize) = 0;

    /// \brief Marks the beginning of a frame.
    /// Must be called before any draw operations.
    virtual void BeginFrame() = 0;

    /// \brief Marks the end of a frame.
    /// Finalizes all pending rendering operations.
    virtual void EndFrame() = 0;

    /// \brief Clears the frame buffer with the specified color.
    /// \param r Red channel (0.0 - 1.0).
    /// \param g Green channel (0.0 - 1.0).
    /// \param b Blue channel (0.0 - 1.0).
    /// \param a Alpha channel (0.0 - 1.0).
    virtual void Clear(float r, float g, float b, float a) = 0;

    /// \brief Draws a texture at the specified position and size.
    /// \param texture The texture to draw.
    /// \param x The X coordinate in screen space.
    /// \param y The Y coordinate in screen space.
    /// \param width The width to draw the texture.
    /// \param height The height to draw the texture.
    virtual void DrawTexture(ITexture* texture, int x, int y, int width, int height) = 0;

    /// \brief Flushes all pending rendering commands to the GPU.
    virtual void Flush() = 0;

    /// \brief Gets the current rendering backend type.
    /// \return The Backend enum value representing the active backend.
    virtual Backend GetBackend() const = 0;

    /// \brief Converts RGBA to BGRA with premultiplied alpha using GPU compute shader.
    /// \param inputRGBA Pointer to input RGBA32 pixel data.
    /// \param outputBGRA Pointer to output buffer for BGRA32 premultiplied pixels.
    /// \param pixelCount Number of pixels to convert.
    /// \return true if GPU conversion succeeded; false if not supported or failed.
    /// \note Falls back to CPU conversion if compute shaders are not available.
    virtual bool ConvertRGBAToBGRAPremultipliedGPU(const void* inputRGBA, void* outputBGRA,
                                                   uint32_t pixelCount)
    {
        (void)inputRGBA;
        (void)outputBGRA;
        (void)pixelCount;
        return false;  // Default: not supported, use CPU fallback
    }
};

}  // namespace Renderer
}  // namespace GifBolt
