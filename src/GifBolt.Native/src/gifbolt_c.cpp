// SPDX-License-Identifier: MIT
#include "gifbolt_c.h"

#include <cstdint>
#include <new>

#include "GifBoltRenderer.h"
#include "GifDecoder.h"

using namespace GifBolt;

extern "C" {
GB_API void gb_decoder_set_min_frame_delay_ms(gb_decoder_t decoder, int minDelayMs)
{
    if (!decoder) return;
    auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
    ptr->SetMinFrameDelayMs(static_cast<uint32_t>(minDelayMs));
}

GB_API int gb_decoder_get_min_frame_delay_ms(gb_decoder_t decoder)
{
    if (!decoder) return 0;
    auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
    return static_cast<int>(ptr->GetMinFrameDelayMs());
}
    GB_API gb_decoder_t gb_decoder_create(void)
    {
        try
        {
            return reinterpret_cast<gb_decoder_t>(new GifDecoder());
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API void gb_decoder_destroy(gb_decoder_t decoder)
    {
        if (!decoder)
            return;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        delete ptr;
    }

    GB_API int gb_decoder_load_from_path(gb_decoder_t decoder, const char* path)
    {
        if (!decoder || !path)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return ptr->LoadFromFile(path) ? 1 : 0;
    }

    GB_API int gb_decoder_get_frame_count(gb_decoder_t decoder)
    {
        if (!decoder)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetFrameCount());
    }

    GB_API int gb_decoder_get_width(gb_decoder_t decoder)
    {
        if (!decoder)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetWidth());
    }

    GB_API int gb_decoder_get_height(gb_decoder_t decoder)
    {
        if (!decoder)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetHeight());
    }

    GB_API int gb_decoder_get_loop_count(gb_decoder_t decoder)
    {
        if (!decoder)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        // We currently only know if it's looping; not the exact count.
        return ptr->IsLooping() ? -1 : 0;
    }

    GB_API int gb_decoder_get_frame_delay_ms(gb_decoder_t decoder, int index)
    {
        if (!decoder)
            return 0;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
            return 0;
        try
        {
            const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
            return static_cast<int>(f.delayMs);
        }
        catch (...)
        {
            return 0;
        }
    }

    GB_API const void* gb_decoder_get_frame_pixels_rgba32(gb_decoder_t decoder, int index,
                                                          int* byteCount)
    {
        if (byteCount)
            *byteCount = 0;
        if (!decoder)
            return nullptr;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
            return nullptr;
        try
        {
            const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
            if (byteCount)
            {
                // f.pixels.size() is in uint32_t units (RGBA32)
                *byteCount = static_cast<int>(f.pixels.size() * sizeof(uint32_t));
            }
            return reinterpret_cast<const void*>(f.pixels.data());
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API const void* gb_decoder_get_frame_pixels_bgra32_premultiplied(gb_decoder_t decoder,
                                                                        int index, int* byteCount)
    {
        if (byteCount)
            *byteCount = 0;
        if (!decoder)
            return nullptr;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
            return nullptr;
        try
        {
            const uint8_t* bgraPixels = ptr->GetFramePixelsBGRA32Premultiplied(static_cast<uint32_t>(index));
            if (!bgraPixels)
                return nullptr;

            if (byteCount)
            {
                // Get frame dimensions to calculate byte count
                const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
                *byteCount = static_cast<int>(f.pixels.size() * sizeof(uint32_t));
            }
            return reinterpret_cast<const void*>(bgraPixels);
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API const void* gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled(
        gb_decoder_t decoder, int index, int targetWidth, int targetHeight,
        int* outWidth, int* outHeight, int* byteCount, int filterType)
    {
        if (byteCount)
            *byteCount = 0;
        if (outWidth)
            *outWidth = 0;
        if (outHeight)
            *outHeight = 0;
        if (!decoder)
            return nullptr;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0 || targetWidth <= 0 || targetHeight <= 0)
            return nullptr;
        try
        {
            uint32_t actualWidth = 0;
            uint32_t actualHeight = 0;
            const uint8_t* bgraPixels = ptr->GetFramePixelsBGRA32PremultipliedScaled(
                static_cast<uint32_t>(index),
                static_cast<uint32_t>(targetWidth),
                static_cast<uint32_t>(targetHeight),
                actualWidth,
                actualHeight,
                static_cast<ScalingFilter>(filterType));

            if (!bgraPixels)
                return nullptr;

            if (outWidth)
                *outWidth = static_cast<int>(actualWidth);
            if (outHeight)
                *outHeight = static_cast<int>(actualHeight);
            if (byteCount)
                *byteCount = static_cast<int>(actualWidth * actualHeight * 4);

            return reinterpret_cast<const void*>(bgraPixels);
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API unsigned int gb_decoder_get_background_color(gb_decoder_t decoder)
    {
        if (!decoder)
            return 0xFF000000;
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return ptr->GetBackgroundColor();
    }

    // Renderer C API
    GB_API gb_renderer_t GifBolt_Create(void)
    {
        try
        {
            return reinterpret_cast<gb_renderer_t>(new GifBolt::GifBoltRenderer());
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API void GifBolt_Destroy(gb_renderer_t renderer)
    {
        if (!renderer)
            return;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        delete r;
    }

    GB_API int GifBolt_Initialize(gb_renderer_t renderer, unsigned int width, unsigned int height)
    {
        if (!renderer)
            return 0;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->Initialize(width, height) ? 1 : 0;
    }

    GB_API int GifBolt_LoadGif(gb_renderer_t renderer, const char* path)
    {
        if (!renderer || !path)
            return 0;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->LoadGif(path) ? 1 : 0;
    }

    GB_API void GifBolt_Play(gb_renderer_t renderer)
    {
        if (!renderer)
            return;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Play();
    }

    GB_API void GifBolt_Pause(gb_renderer_t renderer)
    {
        if (!renderer)
            return;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Pause();
    }

    GB_API void GifBolt_Stop(gb_renderer_t renderer)
    {
        if (!renderer)
            return;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Stop();
    }

    GB_API void GifBolt_SetLooping(gb_renderer_t renderer, int loop)
    {
        if (!renderer)
            return;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->SetLooping(loop != 0);
    }

    GB_API int GifBolt_Render(gb_renderer_t renderer)
    {
        if (!renderer)
            return 0;
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->Render() ? 1 : 0;
    }

}  // extern "C"
