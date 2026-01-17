// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#pragma once

#include <memory>

#include "IDeviceCommandContext.h"

namespace GifBolt
{
namespace Renderer
{

/**
 * Dummy backend for testing and cross-platform development
 */
class DummyDeviceCommandContext : public IDeviceCommandContext
{
   public:
    DummyDeviceCommandContext() = default;
    ~DummyDeviceCommandContext() override = default;

    std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height, const void* data,
                                            size_t dataSize) override;

    void BeginFrame() override;
    void EndFrame() override;

    void Clear(float r, float g, float b, float a) override;
    void DrawTexture(ITexture* texture, int x, int y, int width, int height) override;

    void Flush() override;
    Backend GetBackend() const override
    {
        return Backend::DUMMY;
    }

   private:
    bool m_InFrame = false;
};

}  // namespace Renderer
}  // namespace GifBolt
