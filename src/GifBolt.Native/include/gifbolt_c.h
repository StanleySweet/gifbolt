// SPDX-License-Identifier: MIT
// C ABI for GifBolt native library
#pragma once

#ifdef __cplusplus
extern "C"
{
#endif

#if defined(_WIN32) || defined(_WIN64)
#ifdef GIFBOLT_NATIVE_EXPORTS
#define GB_API __declspec(dllexport)
#else
#define GB_API __declspec(dllimport)
#endif
#else
#define GB_API
#endif

    // Opaque decoder handle
    typedef void* gb_decoder_t;

    // Lifecycle
    GB_API gb_decoder_t gb_decoder_create(void);
    GB_API void gb_decoder_destroy(gb_decoder_t decoder);

    // Loading
    GB_API int gb_decoder_load_from_path(gb_decoder_t decoder, const char* path);

    // Infos
    GB_API int gb_decoder_get_frame_count(gb_decoder_t decoder);
    GB_API int gb_decoder_get_width(gb_decoder_t decoder);
    GB_API int gb_decoder_get_height(gb_decoder_t decoder);
    // -1 if looping (infinite/unknown count), 0 otherwise
    GB_API int gb_decoder_get_loop_count(gb_decoder_t decoder);

    // Frame data
    GB_API int gb_decoder_get_frame_delay_ms(gb_decoder_t decoder, int index);
    // Returns pointer to RGBA32 pixels for the frame; byteCount is set to size in bytes
    GB_API const void* gb_decoder_get_frame_pixels_rgba32(gb_decoder_t decoder, int index,
                                                          int* byteCount);

    // Opaque renderer handle
    typedef void* gb_renderer_t;

    // Renderer lifecycle
    GB_API gb_renderer_t GifBolt_Create(void);
    GB_API void GifBolt_Destroy(gb_renderer_t renderer);

    // Renderer control
    GB_API int GifBolt_Initialize(gb_renderer_t renderer, unsigned int width, unsigned int height);
    GB_API int GifBolt_LoadGif(gb_renderer_t renderer, const char* path);
    GB_API void GifBolt_Play(gb_renderer_t renderer);
    GB_API void GifBolt_Pause(gb_renderer_t renderer);
    GB_API void GifBolt_Stop(gb_renderer_t renderer);
    GB_API void GifBolt_SetLooping(gb_renderer_t renderer, int loop);
    GB_API int GifBolt_Render(gb_renderer_t renderer);

#ifdef __cplusplus
}
#endif
