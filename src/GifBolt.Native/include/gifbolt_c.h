// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors
// C ABI for GifBolt native library

/// \file gifbolt_c.h
/// \brief C API for GifBolt renderer and decoder.
///
/// This header defines the C ABI (Application Binary Interface) for GifBolt,
/// enabling interoperability with C# P/Invoke and other C-compatible languages.
/// All functions are thread-safe unless otherwise noted.

#pragma once

#include "gifbolt_version.h"

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

    /// \typedef gb_decoder_t
    /// \brief Opaque handle to a GIF decoder instance.
    typedef void* gb_decoder_t;

    /// \defgroup Decoder GIF Decoder Functions
    /// @{

    /// \brief Creates a new GIF decoder instance.
    /// \return A handle to the decoder, or NULL if creation fails.
    GB_API gb_decoder_t gb_decoder_create(void);

    /// \brief Destroys a GIF decoder instance and releases resources.
    /// \param decoder The decoder handle to destroy (can be NULL).
    GB_API void gb_decoder_destroy(gb_decoder_t decoder);

    /// \brief Loads a GIF from the specified file path.
    /// \param decoder The decoder handle.
    /// \param path The file path to the GIF image.
    /// \return 1 if successful; 0 otherwise.
    GB_API int gb_decoder_load_from_path(gb_decoder_t decoder, const char* path);

    /// \brief Loads a GIF from an in-memory buffer.
    /// \param decoder The decoder handle.
    /// \param data Pointer to the GIF data buffer.
    /// \param length Length of the buffer in bytes.
    /// \return 1 if successful; 0 otherwise.
    GB_API int gb_decoder_load_from_memory(gb_decoder_t decoder, const void* data, int length);

    /// \brief Gets the total number of frames in the loaded GIF.
    /// \param decoder The decoder handle.
    /// \return The frame count, or 0 if no GIF is loaded or on error.
    GB_API int gb_decoder_get_frame_count(gb_decoder_t decoder);

    /// \brief Gets the width of the GIF image.
    /// \param decoder The decoder handle.
    /// \return The width in pixels, or 0 if no GIF is loaded or on error.
    GB_API int gb_decoder_get_width(gb_decoder_t decoder);

    /// \brief Gets the height of the GIF image.
    /// \param decoder The decoder handle.
    /// \return The height in pixels, or 0 if no GIF is loaded or on error.
    GB_API int gb_decoder_get_height(gb_decoder_t decoder);

    /// \brief Gets the looping behavior of the GIF.
    /// \param decoder The decoder handle.
    /// \return -1 if the GIF loops indefinitely (or loop count is unknown),
    ///         0 if the GIF does not loop, or positive value for specific loop count.
    GB_API int gb_decoder_get_loop_count(gb_decoder_t decoder);

    /// \brief Gets the display duration of the specified frame.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \return The frame delay in milliseconds, or 0 on error.
    GB_API int gb_decoder_get_frame_delay_ms(gb_decoder_t decoder, int index);

    /// \brief Gets the RGBA32 pixel data for the specified frame.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param[out] byteCount Pointer to receive the size of pixel data in bytes.
    /// \return Pointer to RGBA32 pixel data, or NULL on error.
    ///         The pointer is valid until the next decoder operation.
    GB_API const void* gb_decoder_get_frame_pixels_rgba32(gb_decoder_t decoder, int index,
                                                          int* byteCount);

    /// \brief Gets BGRA32 pixel data with premultiplied alpha for the specified frame.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param[out] byteCount Pointer to receive the size of pixel data in bytes.
    /// \return Pointer to BGRA32 premultiplied pixel data, or NULL on error.
    ///         The pointer is valid until the next decoder operation.
    ///         This is optimized for Avalonia and other frameworks requiring premultiplied alpha.
    GB_API const void* gb_decoder_get_frame_pixels_bgra32_premultiplied(gb_decoder_t decoder,
                                                                        int index, int* byteCount);

    /// \brief Gets the background color of the GIF.
    /// \param decoder The decoder handle.
    /// \return The background color as RGBA32 (0xAABBGGRR), or 0xFF000000 (black) on error.
    GB_API unsigned int gb_decoder_get_background_color(gb_decoder_t decoder);
    /// @}

    /// \typedef gb_renderer_t
    /// \brief Opaque handle to a GIF renderer instance.
    typedef void* gb_renderer_t;

    /// \defgroup Renderer GIF Renderer Functions
    /// @{

    /// \brief Creates a new GIF renderer instance.
    /// \return A handle to the renderer, or NULL if creation fails.
    GB_API gb_renderer_t GifBolt_Create(void);

    /// \brief Destroys a GIF renderer instance and releases resources.
    /// \param renderer The renderer handle to destroy (can be NULL).
    GB_API void GifBolt_Destroy(gb_renderer_t renderer);

    /// \brief Initializes the renderer with the specified dimensions.
    /// \param renderer The renderer handle.
    /// \param width The rendering surface width in pixels.
    /// \param height The rendering surface height in pixels.
    /// \return 1 if successful; 0 otherwise.
    GB_API int GifBolt_Initialize(gb_renderer_t renderer, unsigned int width, unsigned int height);

    /// \brief Loads a GIF from the specified file path.
    /// \param renderer The renderer handle.
    /// \param path The file path to the GIF image.
    /// \return 1 if successful; 0 otherwise.
    GB_API int GifBolt_LoadGif(gb_renderer_t renderer, const char* path);

    /// \brief Loads a GIF from an in-memory buffer.
    /// \param renderer The renderer handle.
    /// \param data Pointer to the GIF data buffer.
    /// \param length Length of the buffer in bytes.
    /// \return 1 if successful; 0 otherwise.
    GB_API int GifBolt_LoadGifFromMemory(gb_renderer_t renderer, const void* data, int length);

    /// \brief Starts playback of the loaded GIF.
    /// \param renderer The renderer handle.
    GB_API void GifBolt_Play(gb_renderer_t renderer);

    /// \brief Pauses playback of the GIF.
    /// \param renderer The renderer handle.
    GB_API void GifBolt_Pause(gb_renderer_t renderer);

    /// \brief Stops playback and resets to the first frame.
    /// \param renderer The renderer handle.
    GB_API void GifBolt_Stop(gb_renderer_t renderer);

    /// \brief Sets the looping behavior of the GIF.
    /// \param renderer The renderer handle.
    /// \param loop 1 to enable looping; 0 to disable.
    GB_API void GifBolt_SetLooping(gb_renderer_t renderer, int loop);

    /// \brief Renders the current frame.
    /// \param renderer The renderer handle.
    /// \return 1 if rendering succeeded; 0 otherwise.
    GB_API int GifBolt_Render(gb_renderer_t renderer);

    GB_API void gb_decoder_set_min_frame_delay_ms(gb_decoder_t decoder, int minDelayMs);
    GB_API int gb_decoder_get_min_frame_delay_ms(gb_decoder_t decoder);

    /// \brief Gets BGRA32 pixel data with premultiplied alpha for the specified frame, scaled to
    /// target dimensions.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param targetWidth The desired output width in pixels.
    /// \param targetHeight The desired output height in pixels.
    /// \param[out] outWidth Pointer to receive the actual output width.
    /// \param[out] outHeight Pointer to receive the actual output height.
    /// \param[out] byteCount Pointer to receive the size of pixel data in bytes.
    /// \param filterType The scaling filter to use (0=Nearest, 1=Bilinear, 2=Bicubic, 3=Lanczos).
    /// \return Pointer to BGRA32 premultiplied scaled pixel data, or NULL on error.
    ///         The pointer is valid until the next decoder operation.
    GB_API const void* gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled(
        gb_decoder_t decoder, int index, int targetWidth, int targetHeight, int* outWidth,
        int* outHeight, int* byteCount, int filterType);

    /// \brief Starts background prefetching of frames ahead of the current playback position.
    /// \param decoder The decoder handle.
    /// \param startFrame The frame to start prefetching from.
    /// \remarks This starts a background thread that decodes frames ahead of playback,
    ///          reducing latency for sequential frame access.
    GB_API void gb_decoder_start_prefetching(gb_decoder_t decoder, int startFrame);

    /// \brief Stops background prefetching and joins the prefetch thread.
    /// \param decoder The decoder handle.
    GB_API void gb_decoder_stop_prefetching(gb_decoder_t decoder);

    /// \brief Updates the current playback position for prefetch lookahead.
    /// \param decoder The decoder handle.
    /// \param currentFrame The current frame being displayed.
    /// \remarks The prefetch thread uses this to determine which frames to decode next.
    GB_API void gb_decoder_set_current_frame(gb_decoder_t decoder, int currentFrame);
    /// @}

#ifdef __cplusplus
}
#endif
