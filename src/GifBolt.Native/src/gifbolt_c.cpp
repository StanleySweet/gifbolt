// SPDX-License-Identifier: MIT
#include "gifbolt_c.h"

#include <cstdint>
#include <new>

#ifdef _WIN32
#include <windows.h>
#endif

#include <thread>
#include "GifBoltRenderer.h"
#include "GifDecoder.h"
#include "ITexture.h"

using namespace GifBolt;

// Thread-local storage for error messages
thread_local char g_lastError[512] = {0};

extern "C"
{
    GB_API void gb_decoder_set_min_frame_delay_ms(gb_decoder_t decoder, int minDelayMs)
    {
        if (decoder == nullptr)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->SetMinFrameDelayMs(static_cast<uint32_t>(minDelayMs));
    }

    GB_API int gb_decoder_get_min_frame_delay_ms(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetMinFrameDelayMs());
    }

    GB_API void gb_decoder_set_max_cached_frames(gb_decoder_t decoder, unsigned int maxFrames)
    {
        if (decoder == nullptr)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->SetMaxCachedFrames(static_cast<uint32_t>(maxFrames));
    }

    GB_API unsigned int gb_decoder_get_max_cached_frames(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<unsigned int>(ptr->GetMaxCachedFrames());
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

    GB_API gb_decoder_t gb_decoder_create_with_backend(int backend)
    {
        try
        {
            // Clear error message on successful attempt
            g_lastError[0] = '\0';
            
#ifdef _WIN32
            char dbgMsg[256];
            sprintf_s(dbgMsg, sizeof(dbgMsg), "[gifbolt_c] Attempting to create decoder with backend=%d\n", backend);
            OutputDebugStringA(dbgMsg);
            
            sprintf_s(dbgMsg, sizeof(dbgMsg), "[gifbolt_c::gb_decoder_create_with_backend] BEFORE new - backend=%d\n", backend);
            OutputDebugStringA(dbgMsg);
#endif
            
            auto* decoderPtr = new GifDecoder(static_cast<Renderer::Backend>(backend));
            
#ifdef _WIN32
            sprintf_s(dbgMsg, sizeof(dbgMsg), "[gifbolt_c::gb_decoder_create_with_backend] AFTER new - decoder ptr=%p\n", decoderPtr);
            OutputDebugStringA(dbgMsg);
#endif
            
            return reinterpret_cast<gb_decoder_t>(decoderPtr);
        }
        catch (const std::exception& ex)
        {
#ifdef _WIN32
            char dbgMsg[512];
            sprintf_s(dbgMsg, sizeof(dbgMsg), "[gifbolt_c] Exception caught: %s\n", ex.what());
            OutputDebugStringA(dbgMsg);
#endif
            
            // Store error message in thread-local storage
            strncpy_s(g_lastError, sizeof(g_lastError), ex.what(), sizeof(g_lastError) - 1);
            g_lastError[sizeof(g_lastError) - 1] = '\0';
            return nullptr;
        }
        catch (...)
        {
#ifdef _WIN32
            OutputDebugStringA("[gifbolt_c] Unknown exception caught\n");
#endif
            strncpy_s(g_lastError, sizeof(g_lastError), "Unknown error", sizeof(g_lastError) - 1);
            return nullptr;
        }
    }

    GB_API void gb_decoder_destroy(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        delete ptr;
    }

    GB_API int gb_decoder_load_from_path(gb_decoder_t decoder, const char* path)
    {
        if ((decoder == nullptr) || (path == nullptr))
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return ptr->LoadFromFile(path) ? 1 : 0;
    }

    GB_API int gb_decoder_load_from_memory(gb_decoder_t decoder, const void* data, int length)
    {
        if ((decoder == nullptr) || (data == nullptr) || (length <= 0))
        {
            return 0;
        }

        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        const auto* bytes = reinterpret_cast<const uint8_t*>(data);
        return ptr->LoadFromMemory(bytes, static_cast<size_t>(length)) ? 1 : 0;
    }

    GB_API int gb_decoder_get_frame_count(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetFrameCount());
    }

    GB_API int gb_decoder_get_width(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetWidth());
    }

    GB_API int gb_decoder_get_height(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return static_cast<int>(ptr->GetHeight());
    }

    GB_API int gb_decoder_get_loop_count(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        // We currently only know if it's looping; not the exact count.
        return ptr->IsLooping() ? -1 : 0;
    }

    GB_API int gb_decoder_get_frame_delay_ms(gb_decoder_t decoder, int index)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
        {
            return 0;
        }
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
        if (byteCount != nullptr)
        {
            *byteCount = 0;
        }
        if (decoder == nullptr)
        {
            return nullptr;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
        {
            return nullptr;
        }
        try
        {
            const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
            if (byteCount != nullptr)
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
        if (byteCount != nullptr)
        {
            *byteCount = 0;
        }
        if (decoder == nullptr)
        {
            return nullptr;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
        {
            return nullptr;
        }
        try
        {
            const uint8_t* bgraPixels =
                ptr->GetFramePixelsBGRA32Premultiplied(static_cast<uint32_t>(index));
            if (bgraPixels == nullptr)
            {
                return nullptr;
            }

            if (byteCount != nullptr)
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
        gb_decoder_t decoder, int index, int targetWidth, int targetHeight, int* outWidth,
        int* outHeight, int* byteCount, int filterType)
    {
        if (byteCount != nullptr)
        {
            *byteCount = 0;
        }
        if (outWidth != nullptr)
        {
            *outWidth = 0;
        }
        if (outHeight != nullptr)
        {
            *outHeight = 0;
        }
        if (decoder == nullptr)
        {
            return nullptr;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return nullptr;
        }
        try
        {
            uint32_t actualWidth = 0;
            uint32_t actualHeight = 0;
            const uint8_t* bgraPixels = ptr->GetFramePixelsBGRA32PremultipliedScaled(
                static_cast<uint32_t>(index), static_cast<uint32_t>(targetWidth),
                static_cast<uint32_t>(targetHeight), actualWidth, actualHeight,
                static_cast<ScalingFilter>(filterType));

            if (bgraPixels == nullptr)
            {
                return nullptr;
            }

            if (outWidth != nullptr)
            {
                *outWidth = static_cast<int>(actualWidth);
            }
            if (outHeight != nullptr)
            {
                *outHeight = static_cast<int>(actualHeight);
            }
            if (byteCount != nullptr)
            {
                *byteCount = static_cast<int>(actualWidth * actualHeight * 4);
            }

            return reinterpret_cast<const void*>(bgraPixels);
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API unsigned int gb_decoder_get_background_color(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0xFF000000;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return ptr->GetBackgroundColor();
    }

    GB_API int gb_decoder_has_transparency(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        return ptr->HasTransparency() ? 1 : 0;
    }

    GB_API void gb_decoder_start_prefetching(gb_decoder_t decoder, int startFrame)
    {
        if ((decoder == nullptr) || startFrame < 0)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->StartPrefetching(static_cast<uint32_t>(startFrame));
    }

    GB_API void gb_decoder_stop_prefetching(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->StopPrefetching();
    }

    GB_API void gb_decoder_set_current_frame(gb_decoder_t decoder, int currentFrame)
    {
        if ((decoder == nullptr) || currentFrame < 0)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->SetCurrentFrame(static_cast<uint32_t>(currentFrame));
    }

    GB_API void gb_decoder_reset_canvas(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        ptr->ResetCanvas();
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
        if (renderer == nullptr)
        {
            return;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        delete r;
    }

    GB_API int GifBolt_Initialize(gb_renderer_t renderer, unsigned int width, unsigned int height)
    {
        if (renderer == nullptr)
        {
            return 0;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->Initialize(width, height) ? 1 : 0;
    }

    GB_API int GifBolt_LoadGif(gb_renderer_t renderer, const char* path)
    {
        if ((renderer == nullptr) || (path == nullptr))
        {
            return 0;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->LoadGif(path) ? 1 : 0;
    }

    GB_API int GifBolt_LoadGifFromMemory(gb_renderer_t renderer, const void* data, int length)
    {
        if ((renderer == nullptr) || (data == nullptr) || (length <= 0))
        {
            return 0;
        }

        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        const auto* bytes = reinterpret_cast<const uint8_t*>(data);
        return r->LoadGifFromMemory(bytes, static_cast<std::size_t>(length)) ? 1 : 0;
    }

    GB_API void GifBolt_Play(gb_renderer_t renderer)
    {
        if (renderer == nullptr)
        {
            return;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Play();
    }

    GB_API void GifBolt_Pause(gb_renderer_t renderer)
    {
        if (renderer == nullptr)
        {
            return;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Pause();
    }

    GB_API void GifBolt_Stop(gb_renderer_t renderer)
    {
        if (renderer == nullptr)
        {
            return;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->Stop();
    }

    GB_API void GifBolt_SetLooping(gb_renderer_t renderer, int loop)
    {
        if (renderer == nullptr)
        {
            return;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        r->SetLooping(loop != 0);
    }

    GB_API int GifBolt_Render(gb_renderer_t renderer)
    {
        if (renderer == nullptr)
        {
            return 0;
        }
        auto* r = reinterpret_cast<GifBolt::GifBoltRenderer*>(renderer);
        return r->Render() ? 1 : 0;
    }

    GB_API int gb_decoder_get_backend(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return -1;
        }
        auto* dec = reinterpret_cast<GifDecoder*>(decoder);
        auto backend = dec->GetBackend();
        return static_cast<int>(backend);
    }

    GB_API void* gb_decoder_get_native_texture_ptr(gb_decoder_t decoder, int frameIndex)
    {
        if (decoder == nullptr)
        {
            return nullptr;
        }
        auto* dec = reinterpret_cast<GifDecoder*>(decoder);
#ifdef _WIN32
        char dbgMsg[256];
        sprintf_s(dbgMsg, sizeof(dbgMsg), 
            "[gb_decoder_get_native_texture_ptr] decoder handle=%p, dec ptr=%p, frameIndex=%d\n", 
            decoder, dec, frameIndex);
        OutputDebugStringA(dbgMsg);
#endif
        return dec->GetNativeTexturePtr(frameIndex);
    }

    GB_API int gb_decoder_update_gpu_texture(gb_decoder_t decoder, int frameIndex)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* dec = reinterpret_cast<GifDecoder*>(decoder);
        return dec->UpdateGpuTexture(frameIndex) ? 1 : 0;
    }

    GB_API int gb_decoder_advance_and_update_gpu_texture(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return 0;
        }
        auto* dec = reinterpret_cast<GifDecoder*>(decoder);
        return dec->AdvanceFrameAndUpdateGpuTexture() ? 1 : 0;
    }

    GB_API void* gb_decoder_get_current_gpu_texture_ptr(gb_decoder_t decoder)
    {
        if (decoder == nullptr)
        {
            return nullptr;
        }
        auto* dec = reinterpret_cast<GifDecoder*>(decoder);
        return dec->GetCurrentGpuTexturePtr();
    }

    GB_API const char* gb_decoder_get_last_error(void)
    {
        return g_lastError;
    }

    // Frame Control and Timing Functions

    GB_API int gb_decoder_get_effective_frame_delay(int frameDelayMs, int minDelayMs)
    {
        if (frameDelayMs < minDelayMs)
        {
            return minDelayMs;
        }
        return frameDelayMs;
    }

    GB_API gb_frame_advance_result_s gb_decoder_advance_frame(
        int currentFrame, int frameCount, int repeatCount)
    {
        gb_frame_advance_result_s result = {currentFrame, 0, repeatCount};

        if (frameCount < 1)
        {
            result.isComplete = 1;
            return result;
        }

        int nextFrame = currentFrame + 1;

        // Check if we've reached the end of the frame sequence
        if (nextFrame >= frameCount)
        {
            // Determine if we should loop
            if (repeatCount == -1)
            {
                // Infinite loop
                result.nextFrame = 0;
                result.isComplete = 0;
                result.updatedRepeatCount = -1;
            }
            else if (repeatCount > 0)
            {
                // Finite repeats remaining
                result.nextFrame = 0;
                result.isComplete = 0;
                result.updatedRepeatCount = repeatCount - 1;
            }
            else
            {
                // No more repeats; animation is complete
                result.nextFrame = currentFrame;
                result.isComplete = 1;
                result.updatedRepeatCount = 0;
            }
        }
        else
        {
            // Normal frame advance within the sequence
            result.nextFrame = nextFrame;
            result.isComplete = 0;
            result.updatedRepeatCount = repeatCount;
        }

        return result;
    }

    GB_API int gb_decoder_compute_repeat_count(const char* repeatBehavior, int isLooping)
    {
        // NULL, empty, or "0x" means use metadata
        if (repeatBehavior == nullptr || repeatBehavior[0] == '\0' || 
            (repeatBehavior[0] == '0' && repeatBehavior[1] == 'x' && repeatBehavior[2] == '\0'))
        {
            return isLooping ? -1 : 1;
        }

        // "Forever" means infinite loop
        if ((repeatBehavior[0] == 'F' || repeatBehavior[0] == 'f') &&
            (repeatBehavior[1] == 'o' || repeatBehavior[1] == 'O') &&
            (repeatBehavior[2] == 'r' || repeatBehavior[2] == 'R') &&
            (repeatBehavior[3] == 'e' || repeatBehavior[3] == 'E') &&
            (repeatBehavior[4] == 'v' || repeatBehavior[4] == 'V') &&
            (repeatBehavior[5] == 'e' || repeatBehavior[5] == 'E') &&
            (repeatBehavior[6] == 'r' || repeatBehavior[6] == 'R') &&
            repeatBehavior[7] == '\0')
        {
            return -1;
        }

        // "Nx" format where N is the count
        int len = 0;
        while (repeatBehavior[len] != '\0')
        {
            len++;
        }

        if (len >= 2 && (repeatBehavior[len - 1] == 'x' || repeatBehavior[len - 1] == 'X'))
        {
            // Try to parse the count
            int count = 0;
            int i = 0;
            while (i < len - 1 && repeatBehavior[i] >= '0' && repeatBehavior[i] <= '9')
            {
                count = count * 10 + (repeatBehavior[i] - '0');
                i++;
            }

            // If we successfully parsed the number and it's followed immediately by 'x'
            if (i == len - 1 && count > 0)
            {
                return count;
            }
        }

        // Default fallback to metadata
        return isLooping ? -1 : 1;
    }

    GB_API unsigned int gb_decoder_calculate_adaptive_cache_size(
        int frameCount, float cachePercentage, unsigned int minCachedFrames,
        unsigned int maxCachedFrames)
    {
        if (frameCount <= 0)
        {
            return minCachedFrames;
        }

        // Calculate percentage-based cache size
        float calculated = frameCount * cachePercentage;
        unsigned int cacheSize = (unsigned int)(calculated + 0.5f);  // Round to nearest

        // Clamp to min/max bounds
        if (cacheSize < minCachedFrames)
        {
            return minCachedFrames;
        }

        if (cacheSize > maxCachedFrames)
        {
            return maxCachedFrames;
        }

        return cacheSize;
    }

}  // extern "C"
