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
    /// @}

#ifdef __cplusplus
}
#endif
