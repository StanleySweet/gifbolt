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
#include "PixelBuffer.h"

using namespace GifBolt;

// Thread-local storage for error messages
thread_local char g_lastError[512] = {0};

/// \class AnimationContext
/// \brief Manages animation state for playback control and frame advancement.
/// Encapsulates frame index, repeat count, and playback state to reduce
/// P/Invoke overhead and simplify C# animation controllers.
class AnimationContext
{
   public:
    /// \brief Initializes a new animation context with GIF metadata.
    /// \param frameCount Total number of frames in the GIF.
    /// \param loopCount Loop count from GIF metadata (-1=infinite, 0=no loop, >0=specific count).
    /// \param repeatBehavior Optional repeat behavior override (nullptr uses loopCount).
    AnimationContext(int frameCount, int loopCount, const char* repeatBehavior)
        : m_frameCount(frameCount), m_currentFrame(0), m_isPlaying(0), m_isLooping(0)
    {
        // Compute repeat count from behavior string or metadata
        m_repeatCount = gb_decoder_compute_repeat_count(repeatBehavior, loopCount != 0 ? 1 : 0);
        m_isLooping = (loopCount != 0) ? 1 : 0;
    }

    /// \brief Gets the current animation state.
    gb_animation_state_s GetState() const
    {
        return {m_currentFrame, m_repeatCount, m_isPlaying, m_isLooping};
    }

    /// \brief Sets the playback state.
    void SetPlaying(int isPlaying, int doReset)
    {
        m_isPlaying = isPlaying;
        if (doReset != 0)
        {
            m_currentFrame = 0;
            // Reset repeat count based on looping behavior
            m_repeatCount = m_isLooping ? -1 : 1;
        }
    }

    /// \brief Gets the current frame index.
    int GetCurrentFrame() const
    {
        return m_currentFrame;
    }

    /// \brief Sets the current frame index.
    void SetCurrentFrame(int frameIndex)
    {
        m_currentFrame = frameIndex;
    }

    /// \brief Gets the current repeat count.
    int GetRepeatCount() const
    {
        return m_repeatCount;
    }

    /// \brief Sets the repeat count.
    void SetRepeatCount(int repeatCount)
    {
        m_repeatCount = repeatCount;
    }

    /// \brief Advances to the next frame with consolidated state management.
    /// \param rawFrameDelayMs Raw frame delay from GIF metadata.
    /// \param minFrameDelayMs Minimum frame delay threshold.
    /// \param[out] result Updated animation state and timing info.
    /// \return 1 if frame advanced, 0 on error or completion.
    int Advance(int rawFrameDelayMs, int minFrameDelayMs, gb_animation_advance_result_s* result)
    {
        if (result == nullptr || m_frameCount < 1)
        {
            return 0;
        }

        // Compute effective frame delay
        result->effectiveDelayMs = rawFrameDelayMs < minFrameDelayMs ? minFrameDelayMs : rawFrameDelayMs;

        // Perform frame advancement
        int nextFrame = m_currentFrame + 1;

        // Check if we've reached the end of the frame sequence
        if (nextFrame >= m_frameCount)
        {
            // Determine if we should loop
            if (m_repeatCount == -1)
            {
                // Infinite loop
                result->nextFrame = 0;
                result->isComplete = 0;
                result->updatedRepeatCount = -1;
            }
            else if (m_repeatCount > 0)
            {
                // Finite repeats remaining
                result->nextFrame = 0;
                result->isComplete = 0;
                result->updatedRepeatCount = m_repeatCount - 1;
            }
            else
            {
                // No more repeats; animation is complete
                result->nextFrame = m_currentFrame;
                result->isComplete = 1;
                result->updatedRepeatCount = 0;
            }
        }
        else
        {
            // Normal frame advance within the sequence
            result->nextFrame = nextFrame;
            result->isComplete = 0;
            result->updatedRepeatCount = m_repeatCount;
        }

        // Update internal state
        m_currentFrame = result->nextFrame;
        m_repeatCount = result->updatedRepeatCount;

        return 1;
    }

   private:
    int m_frameCount;        ///< Total frames in the GIF
    int m_currentFrame;      ///< Currently displayed frame (0-based)
    int m_repeatCount;       ///< Repeat count (-1=infinite, 0=complete, >0=remaining)
    int m_isPlaying;         ///< 1 if playing, 0 if paused/stopped
    int m_isLooping;         ///< 1 if GIF loops, 0 for single playback
};

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
            auto* decoderPtr = new GifDecoder(static_cast<Renderer::Backend>(backend));
            return reinterpret_cast<gb_decoder_t>(decoderPtr);
        }
        catch (const std::exception& ex)
        {
            // Store error message in thread-local storage
            strncpy_s(g_lastError, sizeof(g_lastError), ex.what(), sizeof(g_lastError) - 1);
            g_lastError[sizeof(g_lastError) - 1] = '\0';
            return nullptr;
        }
        catch (...)
        {
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

    GB_API gb_decoder_metadata_s gb_decoder_get_metadata(gb_decoder_t decoder)
    {
        gb_decoder_metadata_s metadata = {0, 0, 0, 0};
        if (decoder == nullptr)
        {
            return metadata;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        metadata.width = static_cast<int>(ptr->GetWidth());
        metadata.height = static_cast<int>(ptr->GetHeight());
        metadata.frameCount = static_cast<int>(ptr->GetFrameCount());
        metadata.loopCount = ptr->IsLooping() ? -1 : 0;        metadata.minFrameDelayMs = static_cast<int>(ptr->GetMinFrameDelayMs());
        metadata.maxCachedFrames = static_cast<unsigned int>(ptr->GetMaxCachedFrames());        return metadata;
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

    GB_API int gb_decoder_get_frame_pixels_rgba32_buffer(gb_decoder_t decoder, int index,
                                                          gb_pixel_buffer_t* buffer, int* byteCount)
    {
        if (buffer == nullptr || byteCount == nullptr)
        {
            return 0;
        }
        if (decoder == nullptr)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
        try
        {
            const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
            // Create a pixel buffer with a copy of the pixel data
            auto pixelBuf = new PixelBuffer(f.pixels.size() * sizeof(uint32_t));
            if (!pixelBuf)
            {
                *buffer = nullptr;
                *byteCount = 0;
                return 0;
            }
            pixelBuf->CopyFrom(f.pixels.data(), f.pixels.size() * sizeof(uint32_t));
            *buffer = reinterpret_cast<gb_pixel_buffer_t>(pixelBuf);
            *byteCount = static_cast<int>(f.pixels.size() * sizeof(uint32_t));
            return 1;
        }
        catch (...)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
    }

    GB_API int gb_decoder_get_frame_pixels_bgra32_premultiplied_buffer(gb_decoder_t decoder, int index,
                                                                        gb_pixel_buffer_t* buffer, int* byteCount)
    {
        if (buffer == nullptr || byteCount == nullptr)
        {
            return 0;
        }
        if (decoder == nullptr)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
        try
        {
            const uint8_t* bgraPixels = ptr->GetFramePixelsBGRA32Premultiplied(static_cast<uint32_t>(index));
            if (bgraPixels == nullptr)
            {
                *buffer = nullptr;
                *byteCount = 0;
                return 0;
            }

            // Get frame dimensions to calculate byte count
            const GifFrame& f = ptr->GetFrame(static_cast<uint32_t>(index));
            size_t sizeInBytes = f.pixels.size() * sizeof(uint32_t);
            
            // Create a pixel buffer with the BGRA data
            auto pixelBuf = new PixelBuffer(sizeInBytes);
            if (!pixelBuf)
            {
                *buffer = nullptr;
                *byteCount = 0;
                return 0;
            }
            pixelBuf->CopyFrom(bgraPixels, sizeInBytes);
            *buffer = reinterpret_cast<gb_pixel_buffer_t>(pixelBuf);
            *byteCount = static_cast<int>(sizeInBytes);
            return 1;
        }
        catch (...)
        {
            *buffer = nullptr;
            *byteCount = 0;
            return 0;
        }
    }

    GB_API int gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled_buffer(
        gb_decoder_t decoder, int index, int targetWidth, int targetHeight,
        gb_pixel_buffer_t* buffer, int* outWidth, int* outHeight, int* byteCount, int filterType)
    {
        if (buffer == nullptr || outWidth == nullptr || outHeight == nullptr || byteCount == nullptr)
        {
            return 0;
        }
        if (decoder == nullptr)
        {
            *buffer = nullptr;
            *outWidth = 0;
            *outHeight = 0;
            *byteCount = 0;
            return 0;
        }
        auto* ptr = reinterpret_cast<GifDecoder*>(decoder);
        if (index < 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            *buffer = nullptr;
            *outWidth = 0;
            *outHeight = 0;
            *byteCount = 0;
            return 0;
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
                *buffer = nullptr;
                *outWidth = 0;
                *outHeight = 0;
                *byteCount = 0;
                return 0;
            }

            size_t sizeInBytes = static_cast<size_t>(actualWidth) * actualHeight * 4;
            
            // Create a pixel buffer with the scaled BGRA data
            auto pixelBuf = new PixelBuffer(sizeInBytes);
            if (!pixelBuf)
            {
                *buffer = nullptr;
                *outWidth = 0;
                *outHeight = 0;
                *byteCount = 0;
                return 0;
            }
            pixelBuf->CopyFrom(bgraPixels, sizeInBytes);
            *buffer = reinterpret_cast<gb_pixel_buffer_t>(pixelBuf);
            *outWidth = static_cast<int>(actualWidth);
            *outHeight = static_cast<int>(actualHeight);
            *byteCount = static_cast<int>(sizeInBytes);
            return 1;
        }
        catch (...)
        {
            *buffer = nullptr;
            *outWidth = 0;
            *outHeight = 0;
            *byteCount = 0;
            return 0;
        }
    }

    GB_API const void* gb_pixel_buffer_get_data(gb_pixel_buffer_t buffer)
    {
        if (buffer == nullptr)
        {
            return nullptr;
        }
        auto* pixelBuf = reinterpret_cast<PixelBuffer*>(buffer);
        return pixelBuf->Data();
    }

    GB_API int gb_pixel_buffer_get_size(gb_pixel_buffer_t buffer)
    {
        if (buffer == nullptr)
        {
            return 0;
        }
        auto* pixelBuf = reinterpret_cast<PixelBuffer*>(buffer);
        return static_cast<int>(pixelBuf->SizeInBytes());
    }

    GB_API void gb_pixel_buffer_add_ref(gb_pixel_buffer_t buffer)
    {
        // No-op with current design (managed by C# GC)
        (void)buffer;
    }

    GB_API void gb_pixel_buffer_release(gb_pixel_buffer_t buffer)
    {
        if (buffer == nullptr)
        {
            return;
        }
        auto* pixelBuf = reinterpret_cast<PixelBuffer*>(buffer);
        delete pixelBuf;
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

    GB_API gb_frame_advance_timed_result_s gb_decoder_advance_frame_timed(
        int currentFrame, int frameCount, int repeatCount, int rawFrameDelayMs, int minFrameDelayMs)
    {
        gb_frame_advance_timed_result_s result = {currentFrame, 0, repeatCount, rawFrameDelayMs};

        // Compute effective frame delay (applying minimum threshold)
        result.effectiveDelayMs = rawFrameDelayMs < minFrameDelayMs ? minFrameDelayMs : rawFrameDelayMs;

        // Validate frame count
        if (frameCount < 1)
        {
            result.isComplete = 1;
            return result;
        }

        // Perform frame advancement
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

    /// \defgroup AnimationContext Animation State Management Functions
    /// @{

    GB_API gb_animation_context_t gb_animation_context_create(int frameCount, int loopCount,
                                                              const char* repeatBehavior)
    {
        try
        {
            return reinterpret_cast<gb_animation_context_t>(
                new AnimationContext(frameCount, loopCount, repeatBehavior));
        }
        catch (...)
        {
            return nullptr;
        }
    }

    GB_API void gb_animation_context_destroy(gb_animation_context_t context)
    {
        if (context == nullptr)
        {
            return;
        }
        delete reinterpret_cast<AnimationContext*>(context);
    }

    GB_API gb_animation_state_s gb_animation_context_get_state(gb_animation_context_t context)
    {
        if (context == nullptr)
        {
            return {0, 1, 0, 0};
        }
        return reinterpret_cast<AnimationContext*>(context)->GetState();
    }

    GB_API void gb_animation_context_set_playing(gb_animation_context_t context, int isPlaying,
                                                 int doReset)
    {
        if (context == nullptr)
        {
            return;
        }
        reinterpret_cast<AnimationContext*>(context)->SetPlaying(isPlaying, doReset);
    }

    GB_API int gb_animation_context_advance(gb_animation_context_t context, int rawFrameDelayMs,
                                           int minFrameDelayMs,
                                           gb_animation_advance_result_s* result)
    {
        if (context == nullptr || result == nullptr)
        {
            return 0;
        }
        return reinterpret_cast<AnimationContext*>(context)->Advance(rawFrameDelayMs, minFrameDelayMs,
                                                                      result);
    }

    GB_API void gb_animation_context_set_repeat_count(gb_animation_context_t context,
                                                      int repeatCount)
    {
        if (context == nullptr)
        {
            return;
        }
        reinterpret_cast<AnimationContext*>(context)->SetRepeatCount(repeatCount);
    }

    GB_API int gb_animation_context_get_repeat_count(gb_animation_context_t context)
    {
        if (context == nullptr)
        {
            return 1;
        }
        return reinterpret_cast<AnimationContext*>(context)->GetRepeatCount();
    }

    GB_API int gb_animation_context_get_current_frame(gb_animation_context_t context)
    {
        if (context == nullptr)
        {
            return 0;
        }
        return reinterpret_cast<AnimationContext*>(context)->GetCurrentFrame();
    }

    GB_API void gb_animation_context_set_current_frame(gb_animation_context_t context, int frameIndex)
    {
        if (context == nullptr)
        {
            return;
        }
        reinterpret_cast<AnimationContext*>(context)->SetCurrentFrame(frameIndex);
    }

    /// @}

}  // extern "C"
