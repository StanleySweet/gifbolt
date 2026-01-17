// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include "IDeviceCommandContext.h"

namespace GifBolt
{
namespace Renderer
{

#if defined(__APPLE__)

class MetalTexture;

/// \class MetalDeviceCommandContext
/// \brief Metal implementation of IDeviceCommandContext.
///
/// Provides GPU-accelerated rendering using Apple's Metal framework on macOS/iOS.
/// Used as the primary rendering backend for Avalonia integration on Apple platforms.
class MetalDeviceCommandContext : public IDeviceCommandContext
{
   public:
    /// \brief Initializes a new instance of MetalDeviceCommandContext.
    /// \throws std::runtime_error if Metal device creation fails.
    MetalDeviceCommandContext();

    /// \brief Destroys the MetalDeviceCommandContext and releases GPU resources.
    ~MetalDeviceCommandContext() override;

    /// \brief Gets the backend type (always returns Backend::Metal).
    /// \return Backend::Metal
    Backend GetBackend() const override;

    /// \brief Creates a Metal texture with the specified properties.
    /// \param width The texture width in pixels.
    /// \param height The texture height in pixels.
    /// \param rgba32Pixels Pointer to RGBA32 pixel data, or nullptr for empty texture.
    /// \param byteCount Size of the pixel data in bytes.
    /// \return A shared pointer to the created Metal texture.
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

    /// \brief Converts RGBA to BGRA with premultiplied alpha using GPU compute shader.
    /// \param inputRGBA Pointer to input RGBA32 pixel data.
    /// \param outputBGRA Pointer to output buffer for BGRA32 premultiplied pixels.
    /// \param pixelCount Number of pixels to convert.
    /// \return true if GPU conversion succeeded; false if not supported or failed.
    bool ConvertRGBAToBGRAPremultipliedGPU(const void* inputRGBA, void* outputBGRA,
                                           uint32_t pixelCount) override;

    /// \brief Scales an image using GPU acceleration with the specified filter.
    /// \param inputBGRA Pointer to input BGRA32 premultiplied pixel data.
    /// \param inputWidth Width of the input image.
    /// \param inputHeight Height of the input image.
    /// \param outputBGRA Pointer to output buffer for scaled BGRA32 pixels.
    /// \param outputWidth Desired output width.
    /// \param outputHeight Desired output height.
    /// \param filterType The scaling filter to use (Nearest, Bilinear, Bicubic, Lanczos).
    /// \return true if GPU scaling succeeded; false if not supported or failed.
    bool ScaleImageGPU(const void* inputBGRA, uint32_t inputWidth, uint32_t inputHeight,
                       void* outputBGRA, uint32_t outputWidth, uint32_t outputHeight,
                       int filterType) override;

   private:
    struct Impl;
    Impl* _impl;  ///< Opaque implementation pointer
};

#endif  // __APPLE__

}  // namespace Renderer
}  // namespace GifBolt
