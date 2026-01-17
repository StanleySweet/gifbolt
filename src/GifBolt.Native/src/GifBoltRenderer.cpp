// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "GifBoltRenderer.h"
#include "GifDecoder.h"
#include "DummyDeviceCommandContext.h"
#include "ITexture.h"
#include <chrono>

namespace GifBolt
{

class GifBoltRenderer::Impl
{
   public:
    Impl() : m_DeviceContext(std::make_shared<Renderer::DummyDeviceCommandContext>()) {}
    explicit Impl(std::shared_ptr<Renderer::IDeviceCommandContext> context)
        : m_DeviceContext(context) {}

    std::shared_ptr<Renderer::IDeviceCommandContext> m_DeviceContext;
    std::unique_ptr<GifDecoder> m_Decoder;
    std::shared_ptr<Renderer::ITexture> m_CurrentTexture;

    uint32_t m_Width = 0;
    uint32_t m_Height = 0;
    uint32_t m_CurrentFrame = 0;
    bool m_Playing = false;
    bool m_Looping = false;

    std::chrono::steady_clock::time_point m_LastFrameTime;
};

GifBoltRenderer::GifBoltRenderer() : pImpl(std::make_unique<Impl>())
{
}

GifBoltRenderer::GifBoltRenderer(std::shared_ptr<Renderer::IDeviceCommandContext> context)
    : pImpl(std::make_unique<Impl>(context))
{
}

GifBoltRenderer::~GifBoltRenderer() = default;

bool GifBoltRenderer::Initialize(uint32_t width, uint32_t height)
{
    pImpl->m_Width = width;
    pImpl->m_Height = height;
    pImpl->m_Decoder = std::make_unique<GifDecoder>();
    return true;
}

void GifBoltRenderer::SetDeviceContext(std::shared_ptr<Renderer::IDeviceCommandContext> context)
{
    pImpl->m_DeviceContext = context;
}

bool GifBoltRenderer::LoadGif(const std::string& path)
{
    if (!pImpl->m_Decoder)
    {
        return false;
    }

    if (!pImpl->m_Decoder->LoadFromFile(path))
    {
        return false;
    }

    pImpl->m_CurrentFrame = 0;
    pImpl->m_Looping = pImpl->m_Decoder->IsLooping();
    return true;
}

void GifBoltRenderer::Play()
{
    pImpl->m_Playing = true;
    pImpl->m_LastFrameTime = std::chrono::steady_clock::now();
}

void GifBoltRenderer::Stop()
{
    pImpl->m_Playing = false;
    pImpl->m_CurrentFrame = 0;
}

void GifBoltRenderer::Pause()
{
    pImpl->m_Playing = false;
}

void GifBoltRenderer::SetLooping(bool loop)
{
    pImpl->m_Looping = loop;
}

bool GifBoltRenderer::Render()
{
    if (!pImpl->m_Decoder || pImpl->m_Decoder->GetFrameCount() == 0)
    {
        return false;
    }

    // Update frame if playing
    if (pImpl->m_Playing)
    {
        auto now = std::chrono::steady_clock::now();
        auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
            now - pImpl->m_LastFrameTime);

        const auto& currentFrame = pImpl->m_Decoder->GetFrame(pImpl->m_CurrentFrame);

        if (elapsed.count() >= currentFrame.delayMs)
        {
            pImpl->m_CurrentFrame++;
            if (pImpl->m_CurrentFrame >= pImpl->m_Decoder->GetFrameCount())
            {
                if (pImpl->m_Looping)
                {
                    pImpl->m_CurrentFrame = 0;
                }
                else
                {
                    pImpl->m_CurrentFrame = pImpl->m_Decoder->GetFrameCount() - 1;
                    pImpl->m_Playing = false;
                }
            }
            pImpl->m_LastFrameTime = now;
        }
    }

    // Get current frame
    const auto& frame = pImpl->m_Decoder->GetFrame(pImpl->m_CurrentFrame);

    // Create or update texture
    if (!pImpl->m_CurrentTexture)
    {
        pImpl->m_CurrentTexture = pImpl->m_DeviceContext->CreateTexture(
            frame.width, frame.height,
            frame.pixels.data(), frame.pixels.size() * sizeof(uint32_t));
    }
    else
    {
        pImpl->m_CurrentTexture->Update(
            frame.pixels.data(), frame.pixels.size() * sizeof(uint32_t));
    }

    // Render frame
    pImpl->m_DeviceContext->BeginFrame();
    pImpl->m_DeviceContext->Clear(0.0f, 0.0f, 0.0f, 1.0f);
    pImpl->m_DeviceContext->DrawTexture(
        pImpl->m_CurrentTexture.get(), 0, 0, pImpl->m_Width, pImpl->m_Height);
    pImpl->m_DeviceContext->EndFrame();

    return true;
}

void GifBoltRenderer::SetCurrentFrame(uint32_t frameIndex)
{
    if (pImpl->m_Decoder && frameIndex < pImpl->m_Decoder->GetFrameCount())
    {
        pImpl->m_CurrentFrame = frameIndex;
    }
}

uint32_t GifBoltRenderer::GetCurrentFrame() const
{
    return pImpl->m_CurrentFrame;
}

uint32_t GifBoltRenderer::GetFrameCount() const
{
    return pImpl->m_Decoder ? pImpl->m_Decoder->GetFrameCount() : 0;
}

uint32_t GifBoltRenderer::GetWidth() const
{
    return pImpl->m_Decoder ? pImpl->m_Decoder->GetWidth() : 0;
}

uint32_t GifBoltRenderer::GetHeight() const
{
    return pImpl->m_Decoder ? pImpl->m_Decoder->GetHeight() : 0;
}

}  // namespace GifBolt
