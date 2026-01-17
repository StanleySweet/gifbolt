// SPDX-License-Identifier: MIT
#pragma once

#include "IDeviceCommandContext.h"

namespace GifBolt
{
namespace Renderer
{

#if defined(_WIN32) || defined(_WIN64)

class D3D11Texture;

class D3D11DeviceCommandContext : public IDeviceCommandContext
{
   public:
    D3D11DeviceCommandContext();
    ~D3D11DeviceCommandContext() override;

    Backend GetBackend() const override;
    std::shared_ptr<ITexture> CreateTexture(uint32_t width, uint32_t height,
                                            const void* rgba32Pixels, size_t byteCount) override;
    void BeginFrame() override;
    void Clear(float r, float g, float b, float a) override;
    void DrawTexture(ITexture* texture, int x, int y, int width, int height) override;
    void EndFrame() override;
    void Flush() override;

   private:
    struct Impl;
    Impl* _impl;
};

#endif  // _WIN32

}  // namespace Renderer
}  // namespace GifBolt
