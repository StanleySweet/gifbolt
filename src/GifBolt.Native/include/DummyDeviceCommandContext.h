// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <memory>

#include "IDeviceCommandContext.h"

namespace GifBolt
{
namespace Renderer
{

/// \class DummyDeviceCommandContext
/// \brief Dummy/no-op implementation of IDeviceCommandContext.
///
/// Used for testing and cross-platform development when GPU acceleration
/// is not available. Implements the full interface but performs no actual rendering.
class DummyDeviceCommandContext : public IDeviceCommandContext
{
   public:
    /// \brief Initializes a new instance of DummyDeviceCommandContext.
    DummyDeviceCommandContext() = default;

    /// \brief Destroys the DummyDeviceCommandContext.
    ~DummyDeviceCommandContext() override = default;

    /// \brief Creates a dummy texture (no GPU allocation).
    /// \param width The texture width in pixels.
    /// \param height The texture height in pixels.
    /// \param data Pointer to pixel data (ignored).
    /// \param dataSize Size of the pixel data in bytes (ignored).
    /// \return A shared pointer to the dummy texture.
    std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height, const void* data,
                                            size_t dataSize) override;

    /// \brief No-op implementation for BeginFrame.
    void BeginFrame() override;

    /// \brief No-op implementation for EndFrame.
    void EndFrame() override;

    /// \brief No-op implementation for Clear.
    /// \param r Red channel (unused).
    /// \param g Green channel (unused).
    /// \param b Blue channel (unused).
    /// \param a Alpha channel (unused).
    void Clear(float r, float g, float b, float a) override;

    /// \brief No-op implementation for DrawTexture.
    /// \param texture The texture to draw (unused).
    /// \param x The X coordinate (unused).
    /// \param y The Y coordinate (unused).
    /// \param width The width (unused).
    /// \param height The height (unused).
    void DrawTexture(ITexture* texture, int x, int y, int width, int height) override;

    /// \brief No-op implementation for Flush.
    void Flush() override;

    /// \brief Gets the backend type (always returns Backend::DUMMY).
    /// \return Backend::DUMMY
    Backend GetBackend() const override
    {
        return Backend::DUMMY;
    }

   private:
    bool m_InFrame = false;  ///< Tracks whether a frame is currently in progress
};

}  // namespace Renderer
}  // namespace GifBolt
