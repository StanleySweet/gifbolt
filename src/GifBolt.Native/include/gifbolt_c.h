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

#ifdef _WIN32
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

    /// \enum gb_backend_e
    /// \brief Rendering backend types.
    typedef enum
    {
        GB_BACKEND_DUMMY = 0,    ///< Dummy/CPU-only backend
        GB_BACKEND_D3D11 = 1,    ///< DirectX 11 backend
        GB_BACKEND_METAL = 2,    ///< Metal backend (macOS/iOS)
        GB_BACKEND_D3D9EX = 3    ///< DirectX 9Ex backend (WPF D3DImage)
    } gb_backend_e;

    /// \defgroup Decoder GIF Decoder Functions
    /// @{

    /// \brief Creates a new GIF decoder instance with default backend.
    /// \return A handle to the decoder, or NULL if creation fails.
    GB_API gb_decoder_t gb_decoder_create(void);

    /// \brief Creates a new GIF decoder instance with a specified backend.
    /// \param backend The backend to use (GB_BACKEND_DUMMY, GB_BACKEND_D3D11, GB_BACKEND_METAL, GB_BACKEND_D3D9EX).
    /// \return A handle to the decoder, or NULL if creation fails or backend is unavailable.
    GB_API gb_decoder_t gb_decoder_create_with_backend(int backend);

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

    /// \brief Checks if any frame in the GIF has transparency (alpha < 255).
    /// \param decoder The decoder handle.
    /// \return 1 if the GIF contains transparent pixels, 0 otherwise.
    /// \remarks Note: This checks for actual transparent pixels in decoded frames,
    ///         not just the presence of a transparency color index.
    ///         For GIFs with transparency, CPU rendering (WriteableBitmap) should be used
    ///         instead of GPU rendering (D3DImage) as GPU paths have limited alpha support.
    GB_API int gb_decoder_has_transparency(gb_decoder_t decoder);
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

    /// \brief Sets the maximum number of frames to cache in memory.
    /// \param decoder The decoder handle.
    /// \param maxFrames Maximum number of frames to keep in the LRU cache (must be > 0).
    GB_API void gb_decoder_set_max_cached_frames(gb_decoder_t decoder, unsigned int maxFrames);

    /// \brief Gets the maximum number of frames cached in memory.
    /// \param decoder The decoder handle.
    /// \return The maximum number of cached frames, or 0 on error.
    GB_API unsigned int gb_decoder_get_max_cached_frames(gb_decoder_t decoder);

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

    /// \typedef gb_pixel_buffer_t
    /// \brief Opaque handle to a pixel buffer with reference counting.
    typedef void* gb_pixel_buffer_t;

    /// \brief Gets RGBA32 pixel data as a reference-counted buffer.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param[out] buffer Receives the pixel buffer handle.
    /// \param[out] byteCount Receives the size of pixel data in bytes.
    /// \return 1 if successful; 0 otherwise.
    /// \remarks The buffer is reference-counted. Call gb_pixel_buffer_release() when done.
    ///          The returned buffer is owned by the caller and must be released.
    GB_API int gb_decoder_get_frame_pixels_rgba32_buffer(gb_decoder_t decoder, int index,
                                                         gb_pixel_buffer_t* buffer, int* byteCount);

    /// \brief Gets BGRA32 premultiplied pixel data as a reference-counted buffer.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param[out] buffer Receives the pixel buffer handle.
    /// \param[out] byteCount Receives the size of pixel data in bytes.
    /// \return 1 if successful; 0 otherwise.
    /// \remarks The buffer is reference-counted. Call gb_pixel_buffer_release() when done.
    ///          The returned buffer is owned by the caller and must be released.
    GB_API int gb_decoder_get_frame_pixels_bgra32_premultiplied_buffer(gb_decoder_t decoder, int index,
                                                                       gb_pixel_buffer_t* buffer, int* byteCount);

    /// \brief Gets scaled BGRA32 premultiplied pixel data as a reference-counted buffer.
    /// \param decoder The decoder handle.
    /// \param index The zero-based frame index.
    /// \param targetWidth The desired output width in pixels.
    /// \param targetHeight The desired output height in pixels.
    /// \param[out] buffer Receives the pixel buffer handle.
    /// \param[out] outWidth Receives the actual output width.
    /// \param[out] outHeight Receives the actual output height.
    /// \param[out] byteCount Receives the size of pixel data in bytes.
    /// \param filterType The scaling filter to use (0=Nearest, 1=Bilinear, 2=Bicubic, 3=Lanczos).
    /// \return 1 if successful; 0 otherwise.
    /// \remarks The buffer is reference-counted. Call gb_pixel_buffer_release() when done.
    GB_API int gb_decoder_get_frame_pixels_bgra32_premultiplied_scaled_buffer(
        gb_decoder_t decoder, int index, int targetWidth, int targetHeight,
        gb_pixel_buffer_t* buffer, int* outWidth, int* outHeight, int* byteCount, int filterType);

    /// \brief Gets the pixel data pointer from a buffer.
    /// \param buffer The pixel buffer handle.
    /// \return Pointer to pixel data, or NULL if invalid.
    /// \remarks The pointer is valid as long as the buffer exists.
    GB_API const void* gb_pixel_buffer_get_data(gb_pixel_buffer_t buffer);

    /// \brief Gets the size of a pixel buffer in bytes.
    /// \param buffer The pixel buffer handle.
    /// \return Size in bytes, or 0 if invalid.
    GB_API int gb_pixel_buffer_get_size(gb_pixel_buffer_t buffer);

    /// \brief Increments the reference count on a pixel buffer.
    /// \param buffer The pixel buffer handle.
    /// \remarks Call this if you need an additional reference to the buffer.
    GB_API void gb_pixel_buffer_add_ref(gb_pixel_buffer_t buffer);

    /// \brief Decrements the reference count and releases the pixel buffer if ref count reaches zero.
    /// \param buffer The pixel buffer handle (can be NULL).
    /// \remarks Always call this when done with a buffer to avoid memory leaks.
    GB_API void gb_pixel_buffer_release(gb_pixel_buffer_t buffer);

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

    /// \brief Resets the canvas composition state for looping.
    /// \param decoder The decoder handle.
    /// \remarks Call this when looping back to frame 0 to ensure proper frame composition.
    GB_API void gb_decoder_reset_canvas(gb_decoder_t decoder);

    /// \brief Gets the rendering backend type.
    /// \param decoder The decoder handle.
    /// \return Backend ID: 0=Dummy, 1=D3D11, 2=Metal, 3=D3D9Ex; -1 on error.
    GB_API int gb_decoder_get_backend(gb_decoder_t decoder);

    /// \brief Gets the native GPU texture pointer for zero-copy rendering.
    /// \param decoder The decoder handle.
    /// \param frameIndex The frame index to get the texture for.
    /// \return Native texture pointer based on active backend:
    ///         - D3D9Ex: IDirect3DSurface9* (for WPF D3DImage)
    ///         - D3D11: ID3D11Texture2D*
    ///         - Metal: MTLTexture*
    ///         - Dummy: nullptr
    GB_API void* gb_decoder_get_native_texture_ptr(gb_decoder_t decoder, int frameIndex);
    /// \brief Updates and synchronizes the GPU texture with frame pixel data.
    /// \param decoder The decoder handle.
    /// \param frameIndex The frame index to update in the GPU texture.
    /// \return 1 if update succeeded; 0 if no GPU texture available or error occurred.
    /// \remarks This function must be called before using GetNativeTexturePtr() to ensure
    ///          the GPU texture contains the correct frame data. This is critical for 
    ///          GPU-accelerated rendering on D3D backends.
    GB_API int gb_decoder_update_gpu_texture(gb_decoder_t decoder, int frameIndex);

    /// \brief Advances to the next frame and updates the GPU texture with automatic looping.
    /// \param decoder A valid GIF decoder handle from gb_decoder_create_from_file or similar.
    /// \return 1 if frame advanced and GPU texture updated; 0 if error occurred.
    /// \remarks Internally manages frame index with modulo wrapping for infinite looping.
    ///          Call this repeatedly in your render loop to animate the GIF.
    ///          Must have a device context set up for GPU rendering.
    GB_API int gb_decoder_advance_and_update_gpu_texture(gb_decoder_t decoder);

    /// \brief Gets the native GPU texture pointer for the current frame.
    /// \param decoder A valid GIF decoder handle.
    /// \return Native texture pointer (ID3D9Surface* for D3D, MTLTexture* for Metal), or nullptr on error.
    /// \remarks Call gb_decoder_advance_and_update_gpu_texture() first to ensure texture is current.
    ///          This function performs no GPU operations, just returns the current pointer.
    GB_API void* gb_decoder_get_current_gpu_texture_ptr(gb_decoder_t decoder);

    /// \brief Gets the last error message from a backend initialization failure.
    /// \return Error message string, or empty string if no error. Valid until next decoder call.
    GB_API const char* gb_decoder_get_last_error(void);
    /// @}

    /// \defgroup FrameControl Frame Control and Timing Functions
    /// @{

    /// \brief Timing constants for GIF animation playback.
    /// \details These constants define standard timing values used across GifBolt.
    enum gb_timing_constants_e
    {
        /// \brief Default minimum frame delay in milliseconds (10 ms).
        /// Most GIFs are created with delays of 10-100ms; we use 10ms as a reasonable
        /// minimum to prevent GIFs with very small delays from playing too fast.
        GB_DEFAULT_MIN_FRAME_DELAY_MS = 10,

        /// \brief Minimum render interval for UI thread timer (16 ms = 60 FPS).
        /// This is the fastest the UI can be updated while staying responsive.
        GB_MIN_RENDER_INTERVAL_MS = 16
    };

    /// \brief Computes the effective frame delay, applying a minimum threshold.
    /// \param frameDelayMs The frame delay from GIF metadata (in milliseconds).
    /// \param minDelayMs The minimum frame delay to enforce (in milliseconds).
    /// \return The effective frame delay in milliseconds.
    /// \remarks Use 0 for minDelayMs to get the raw delay without threshold enforcement.
    GB_API int gb_decoder_get_effective_frame_delay(int frameDelayMs, int minDelayMs);

    /// \struct gb_decoder_metadata_s
    /// \brief Consolidated GIF metadata.
    /// \remarks More efficient than making separate calls to get width, height, frame count, and loop count.
    /// All configuration parameters are returned in a single call during Load().
    typedef struct
    {
        int width;            ///< Image width in pixels.
        int height;           ///< Image height in pixels.
        int frameCount;       ///< Total number of frames.
        int loopCount;        ///< Loop count (-1=infinite, >=0=specific count).
        int minFrameDelayMs;  ///< Minimum frame delay threshold in milliseconds.
        unsigned int maxCachedFrames; ///< Maximum number of frames to cache.
    } gb_decoder_metadata_s;

    /// \brief Gets consolidated GIF metadata in a single call.
    /// \param decoder The decoder handle.
    /// \return A gb_decoder_metadata_s struct containing all metadata.
    /// \remarks This is more efficient than making separate calls to
    ///          gb_decoder_get_width, gb_decoder_get_height,
    ///          gb_decoder_get_frame_count, and gb_decoder_get_loop_count.
    GB_API gb_decoder_metadata_s gb_decoder_get_metadata(gb_decoder_t decoder);

    /// \struct gb_frame_advance_result_s
    /// \brief Result of a frame advance operation.
    typedef struct
    {
        int nextFrame;          ///< The next frame index.
        int isComplete;         ///< 1 if animation has completed, 0 otherwise.
        int updatedRepeatCount; ///< The updated repeat count (-1=infinite, 0=stop, >0=remaining repeats).
    } gb_frame_advance_result_s;

    /// \struct gb_frame_advance_timed_result_s
    /// \brief Consolidated result of a timed frame advance operation.
    /// \remarks Combines frame advancement, timing, and repeat count management in a single operation.
    typedef struct
    {
        int nextFrame;          ///< The next frame index after advancement.
        int isComplete;         ///< 1 if animation has completed, 0 otherwise.
        int updatedRepeatCount; ///< The updated repeat count (-1=infinite, 0=stop, >0=remaining repeats).
        int effectiveDelayMs;   ///< The effective frame delay in milliseconds (after minimum threshold applied).
    } gb_frame_advance_timed_result_s;

    /// \brief Advances to the next frame in a GIF animation.
    /// \param currentFrame The current frame index (0-based).
    /// \param frameCount The total number of frames in the GIF.
    /// \param repeatCount The current repeat count (-1 = infinite, 0 = stop, >0 = repeat N times).
    /// \return A gb_frame_advance_result_s containing the next frame and updated state.
    /// \remarks frameCount must be >= 1. Otherwise, returns with isComplete=1.
    /// \deprecated Use gb_decoder_advance_frame_timed() for consolidated frame advancement.
    GB_API gb_frame_advance_result_s gb_decoder_advance_frame(
        int currentFrame, int frameCount, int repeatCount);

    /// \brief Performs consolidated frame advancement with timing and repeat count management.
    /// \param currentFrame The current frame index (0-based).
    /// \param frameCount The total number of frames in the GIF.
    /// \param repeatCount The current repeat count (-1 = infinite, 0 = stop, >0 = repeat N times).
    /// \param rawFrameDelayMs The raw frame delay from GIF metadata (in milliseconds).
    /// \param minFrameDelayMs The minimum frame delay threshold (in milliseconds).
    /// \return A gb_frame_advance_timed_result_s containing the next frame, timing info, and updated state.
    /// \remarks This function consolidates frame advancement, delay computation, and repeat count
    ///          management into a single operation, reducing the overhead of multiple P/Invoke calls.
    ///          Equivalent to calling gb_decoder_advance_frame(), gb_decoder_get_effective_frame_delay(),
    ///          and managing repeat counts manually, but in one efficient call.
    GB_API gb_frame_advance_timed_result_s gb_decoder_advance_frame_timed(
        int currentFrame, int frameCount, int repeatCount, int rawFrameDelayMs, int minFrameDelayMs);

    /// \brief Computes the repeat count from a repeat behavior string.
    /// \param repeatBehavior The repeat behavior string (e.g., "Forever", "3x", "0x", or NULL).
    /// \param isLooping Whether the GIF metadata indicates infinite looping.
    /// \return -1 for infinite repeat, positive integer for finite repeats.
    /// \remarks Supported formats:
    ///          - "Forever": infinite loop (-1)
    ///          - "Nx": repeat N times (where N > 0)
    ///          - "0x", NULL, or empty: use GIF metadata (returns -1 if isLooping, else 1)
    GB_API int gb_decoder_compute_repeat_count(const char* repeatBehavior, int isLooping);

    /// \brief Calculates adaptive cache size based on frame count and percentage.
    /// \param frameCount Total number of frames in the GIF.
    /// \param cachePercentage Percentage of frames to cache (0.0 to 1.0).
    /// \param minCachedFrames Minimum frames to keep cached.
    /// \param maxCachedFrames Maximum frames to keep cached.
    /// \return The recommended cache size in frames.
    /// \remarks Result is clamped between minCachedFrames and maxCachedFrames.
    GB_API unsigned int gb_decoder_calculate_adaptive_cache_size(
        int frameCount, float cachePercentage, unsigned int minCachedFrames,
        unsigned int maxCachedFrames);
    /// @}

#ifdef __cplusplus
}
#endif
