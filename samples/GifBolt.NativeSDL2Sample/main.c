// SPDX-License-Identifier: MIT
// Simple SDL2-based GIF player using GifBolt native C API
// Cross-platform test program to diagnose GPU rendering issues

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <time.h>
#include <assert.h>

#include <SDL2/SDL.h>

// Include GifBolt C API
#include "gifbolt_c.h"

typedef struct {
    SDL_Window* window;
    SDL_Renderer* renderer;
    SDL_Texture* texture;
    int width;
    int height;
    uint32_t* pixelBuffer;
} SDLContext;

// Forward declarations
static int InitializeSDL(SDLContext* ctx, int width, int height);
static void CleanupSDL(SDLContext* ctx);
static void RenderFrame(SDLContext* ctx, const uint8_t* bgra32Data, int dataSize);

// ============================================================================
// Main Program
// ============================================================================

int main(int argc, char* argv[])
{
    if (argc < 2) {
        fprintf(stderr, "Usage: %s <gif_file>\n", argv[0]);
        fprintf(stderr, "Example: %s sample.gif\n", argv[0]);
        return 1;
    }

    const char* gifPath = argv[1];
    printf("GifBolt SDL2 Sample - Loading: %s\n", gifPath);

    // Create decoder
    printf("\nInitializing GifBolt decoder...\n");
    gb_decoder_t decoder = gb_decoder_create();
    if (!decoder) {
        fprintf(stderr, "ERROR: Failed to create GIF decoder\n");
        fprintf(stderr, "Last error: %s\n", gb_decoder_get_last_error());
        return 1;
    }

    // Load GIF file
    printf("Loading GIF file: %s\n", gifPath);
    if (!gb_decoder_load_from_path(decoder, gifPath)) {
        fprintf(stderr, "ERROR: Failed to load GIF file\n");
        fprintf(stderr, "Last error: %s\n", gb_decoder_get_last_error());
        gb_decoder_destroy(decoder);
        return 1;
    }

    // Get GIF properties
    int frameCount = gb_decoder_get_frame_count(decoder);
    int width = gb_decoder_get_width(decoder);
    int height = gb_decoder_get_height(decoder);
    int loopCount = gb_decoder_get_loop_count(decoder);
    int hasTransparency = gb_decoder_has_transparency(decoder);
    int backend = gb_decoder_get_backend(decoder);

    printf("\nGIF Properties:\n");
    printf("  Dimensions: %dx%d\n", width, height);
    printf("  Frames: %d\n", frameCount);
    printf("  Loop count: %d\n", loopCount);
    printf("  Has transparency: %s\n", hasTransparency ? "yes" : "no");
    printf("  Backend: ");
    switch (backend) {
        case 0: printf("Dummy (CPU)\n"); break;
        case 1: printf("D3D11\n"); break;
        case 2: printf("Metal\n"); break;
        case 3: printf("D3D9Ex\n"); break;
        default: printf("Unknown (%d)\n", backend); break;
    }

    // Initialize SDL2
    printf("\nInitializing SDL2...\n");
    SDLContext sdl = {0};
    if (!InitializeSDL(&sdl, width, height)) {
        fprintf(stderr, "Failed to initialize SDL2\n");
        gb_decoder_destroy(decoder);
        return 1;
    }

    printf("SDL2 initialized: %dx%d window\n", sdl.width, sdl.height);

    // Main render loop
    printf("\nStarting animation playback...\n");
    printf("Press ESC or close window to exit.\n\n");

    gb_decoder_set_current_frame(decoder, 0);
    gb_decoder_reset_canvas(decoder);
    gb_decoder_start_prefetching(decoder, 0);

    int running = 1;
    SDL_Event event;
    uint64_t frameStart = SDL_GetTicks64();
    uint32_t displayedFrames = 0;
    int currentFrame = 0;

    while (running && currentFrame < frameCount) {
        // Handle events
        while (SDL_PollEvent(&event)) {
            if (event.type == SDL_QUIT) {
                running = 0;
            } else if (event.type == SDL_KEYDOWN) {
                if (event.key.keysym.sym == SDLK_ESCAPE) {
                    running = 0;
                }
            }
        }

        if (!running) break;

        // Get frame data
        int byteCount = 0;
        const uint8_t* frameData = (const uint8_t*)gb_decoder_get_frame_pixels_bgra32_premultiplied(
            decoder, currentFrame, &byteCount);

        if (frameData && byteCount > 0) {
            // Render frame to SDL texture
            RenderFrame(&sdl, frameData, byteCount);

            // Get frame delay
            int delayMs = gb_decoder_get_frame_delay_ms(decoder, currentFrame);
            if (delayMs < 10) delayMs = 10;  // Minimum 10ms

            // Update prefetch position
            gb_decoder_set_current_frame(decoder, currentFrame);

            displayedFrames++;
            currentFrame++;

            // Frame timing
            uint64_t now = SDL_GetTicks64();
            if (now - frameStart >= 1000) {
                printf("FPS: %u | Frame: %d/%d | Delay: %dms\n",
                       displayedFrames, currentFrame, frameCount, delayMs);
                displayedFrames = 0;
                frameStart = now;
            }

            // Sleep for frame delay
            SDL_Delay(delayMs);
        } else {
            fprintf(stderr, "ERROR: Failed to get frame %d data\n", currentFrame);
            break;
        }
    }

    // Cleanup
    printf("\nAnimation complete.\n");
    gb_decoder_stop_prefetching(decoder);
    gb_decoder_destroy(decoder);
    CleanupSDL(&sdl);

    printf("Clean exit\n");
    return 0;
}

// ============================================================================
// SDL2 Helper Functions
// ============================================================================

static int InitializeSDL(SDLContext* ctx, int width, int height)
{
    if (SDL_Init(SDL_INIT_VIDEO) < 0) {
        fprintf(stderr, "SDL_Init failed: %s\n", SDL_GetError());
        return 0;
    }

    ctx->width = width;
    ctx->height = height;

    ctx->window = SDL_CreateWindow(
        "GifBolt SDL2 Sample",
        SDL_WINDOWPOS_CENTERED,
        SDL_WINDOWPOS_CENTERED,
        width,
        height,
        SDL_WINDOW_SHOWN
    );

    if (!ctx->window) {
        fprintf(stderr, "SDL_CreateWindow failed: %s\n", SDL_GetError());
        SDL_Quit();
        return 0;
    }

    ctx->renderer = SDL_CreateRenderer(
        ctx->window, -1,
        SDL_RENDERER_ACCELERATED
    );

    if (!ctx->renderer) {
        fprintf(stderr, "SDL_CreateRenderer failed: %s\n", SDL_GetError());
        SDL_DestroyWindow(ctx->window);
        SDL_Quit();
        return 0;
    }

    // Create BGRA texture for GIF frames
    ctx->texture = SDL_CreateTexture(
        ctx->renderer,
        SDL_PIXELFORMAT_ARGB8888,  // SDL stores as BGRA
        SDL_TEXTUREACCESS_STREAMING,
        width,
        height
    );

    if (!ctx->texture) {
        fprintf(stderr, "SDL_CreateTexture failed: %s\n", SDL_GetError());
        SDL_DestroyRenderer(ctx->renderer);
        SDL_DestroyWindow(ctx->window);
        SDL_Quit();
        return 0;
    }

    // Enable alpha blending
    SDL_SetTextureBlendMode(ctx->texture, SDL_BLENDMODE_BLEND);

    // Allocate pixel buffer for row copying
    ctx->pixelBuffer = (uint32_t*)malloc(width * height * 4);
    if (!ctx->pixelBuffer) {
        fprintf(stderr, "Failed to allocate pixel buffer\n");
        SDL_DestroyTexture(ctx->texture);
        SDL_DestroyRenderer(ctx->renderer);
        SDL_DestroyWindow(ctx->window);
        SDL_Quit();
        return 0;
    }

    return 1;
}

static void CleanupSDL(SDLContext* ctx)
{
    if (ctx->pixelBuffer) free(ctx->pixelBuffer);
    if (ctx->texture) SDL_DestroyTexture(ctx->texture);
    if (ctx->renderer) SDL_DestroyRenderer(ctx->renderer);
    if (ctx->window) SDL_DestroyWindow(ctx->window);
    SDL_Quit();
}

static void RenderFrame(SDLContext* ctx, const uint8_t* bgra32Data, int dataSize)
{
    if (!ctx->texture || !bgra32Data) return;

    int expectedSize = ctx->width * ctx->height * 4;
    if (dataSize < expectedSize) {
        fprintf(stderr, "WARNING: Frame data size mismatch (%d bytes, expected %d)\n",
                dataSize, expectedSize);
        return;
    }

    // Update texture with frame data
    SDL_UpdateTexture(ctx->texture, NULL, bgra32Data, ctx->width * 4);

    // Render to screen
    SDL_RenderClear(ctx->renderer);
    SDL_RenderCopy(ctx->renderer, ctx->texture, NULL, NULL);
    SDL_RenderPresent(ctx->renderer);
}

