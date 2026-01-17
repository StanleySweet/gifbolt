// SPDX-License-Identifier: MIT
#include "gifbolt_c.h"

#include <cstdint>
#include <new>

#include "GifBoltRenderer.h"
#include "GifDecoder.h"

using namespace GifBolt;

extern "C"
{
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
