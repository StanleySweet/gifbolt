// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "DummyDeviceCommandContext.h"

#include <vector>

#include "ITexture.h"

namespace GifBolt
{
namespace Renderer
{

class DummyTexture : public ITexture
{
   public:
    DummyTexture(uint32_t width, uint32_t height, const void* data, size_t dataSize)
        : m_Width(width), m_Height(height)
    {
        if ((data != nullptr) && dataSize > 0)
        {
            m_Data.resize(dataSize);
            std::memcpy(m_Data.data(), data, dataSize);
        }
    }

    uint32_t GetWidth() const override
    {
        return m_Width;
    }
    uint32_t GetHeight() const override
    {
        return m_Height;
    }
    PixelFormats::Format GetFormat() const override
    {
        return PixelFormats::Format::R8G8B8A8_UNORM;
    }

    bool Update(const void* data, size_t dataSize) override
    {
        if ((data == nullptr) || dataSize == 0)
        {
            return false;
        }
        m_Data.resize(dataSize);
        std::memcpy(m_Data.data(), data, dataSize);
        return true;
    }

    void* GetNativeTexturePtr() override
    {
        return nullptr;  // Dummy backend has no native texture
    }

   private:
    uint32_t m_Width;
    uint32_t m_Height;
    std::vector<uint8_t> m_Data;
};

std::shared_ptr<ITexture> DummyDeviceCommandContext::CreateTexture(uint32_t width, uint32_t height,
                                                                   const void* data,
                                                                   size_t dataSize)
{
    return std::make_shared<DummyTexture>(width, height, data, dataSize);
}

void DummyDeviceCommandContext::BeginFrame()
{
    m_InFrame = true;
}

void DummyDeviceCommandContext::EndFrame()
{
    m_InFrame = false;
}

void DummyDeviceCommandContext::Clear(float, float, float, float)
{
    // Dummy implementation
}

void DummyDeviceCommandContext::DrawTexture(ITexture*, int, int, int, int)
{
    // Dummy implementation
}

void DummyDeviceCommandContext::Flush()
{
    // Dummy implementation
}

}  // namespace Renderer
}  // namespace GifBolt
