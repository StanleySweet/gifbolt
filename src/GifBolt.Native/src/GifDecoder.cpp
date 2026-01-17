// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "GifDecoder.h"
#include <gif_lib.h>
#include <fstream>
#include <stdexcept>
#include <cstring>

namespace GifBolt {

class GifDecoder::Impl
{
   public:
    std::vector<GifFrame> frames;
    std::vector<uint32_t> canvas;  ///< Accumulated canvas for frame composition
    DisposalMethod previousDisposal = DisposalMethod::None;  ///< Previous frame disposal
    // Track previous frame rectangle for proper RestoreBackground handling
    uint32_t prevFrameWidth = 0;
    uint32_t prevFrameHeight = 0;
    uint32_t prevFrameOffsetX = 0;
    uint32_t prevFrameOffsetY = 0;
    uint32_t minFrameDelayMs = 10; ///< Délai minimal configurable
    std::vector<uint32_t> previousCanvas;  ///< Saved canvas for RestorePrevious
    uint32_t width = 0;
    uint32_t height = 0;
    uint32_t backgroundColor = 0xFF000000;  ///< Default: opaque black
    bool looping = false;

    bool LoadGif(const std::string& filePath);
    void DecodeFrame(GifFileType* gif, int frameIndex);
    void ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                       std::vector<uint32_t>& pixels, int width, int height,
                       int transparentIndex = -1);
    void ComposeFrame(const GifFrame& frame, std::vector<uint32_t>& canvas);
    DisposalMethod GetDisposalMethod(const SavedImage* image);
};

bool GifDecoder::Impl::LoadGif(const std::string& filePath) {
    int error = 0;
    GifFileType* gif = DGifOpenFileName(filePath.c_str(), &error);

    if (!gif) {
        return false;
    }

    if (DGifSlurp(gif) == GIF_ERROR) {
        DGifCloseFile(gif, &error);
        return false;
    }

    width = gif->SWidth;
    height = gif->SHeight;

    // Extract background color from global color map
    if (gif->SColorMap && gif->SBackGroundColor < gif->SColorMap->ColorCount)
    {
        const GifColorType& bgColor = gif->SColorMap->Colors[gif->SBackGroundColor];
        // Convert to RGBA32 format: 0xAABBGGRR (opaque)
        backgroundColor = 0xFF000000 | (bgColor.Blue << 16) | (bgColor.Green << 8) | bgColor.Red;
    }
    else
    {
        // Default to transparent for proper composition with UI gradients/backgrounds
        backgroundColor = 0x00000000;
    }

    // Initialize canvas with transparent pixels for proper alpha composition
    canvas.resize(width * height, 0x00000000);

    // Decode all frames
    frames.clear();
    frames.reserve(gif->ImageCount);

    for (int i = 0; i < gif->ImageCount; ++i) {
        DecodeFrame(gif, i);
    }

    // Check for looping extension
    for (int i = 0; i < gif->ImageCount; ++i) {
        SavedImage* image = &gif->SavedImages[i];
        for (int j = 0; j < image->ExtensionBlockCount; ++j) {
            ExtensionBlock* ext = &image->ExtensionBlocks[j];
            if (ext->Function == APPLICATION_EXT_FUNC_CODE) {
                if (std::memcmp(ext->Bytes, "NETSCAPE2.0", 11) == 0) {
                    looping = true;
                    break;
                }
            }
        }
    }

    DGifCloseFile(gif, &error);
    return true;
}

void GifDecoder::Impl::DecodeFrame(GifFileType* gif, int frameIndex) {
    SavedImage* image = &gif->SavedImages[frameIndex];
    GifImageDesc* desc = &image->ImageDesc;

    GifFrame frame;
    frame.width = desc->Width;
    frame.height = desc->Height;
    frame.offsetX = desc->Left;
    frame.offsetY = desc->Top;
    frame.delayMs = 100;               // Default delay
    frame.disposal = DisposalMethod::None;
    frame.transparentIndex = -1;       // No transparency by default

    // Get delay, disposal method, and transparency info from graphics control extension
    for (int i = 0; i < image->ExtensionBlockCount; ++i)
    {
        ExtensionBlock* ext = &image->ExtensionBlocks[i];
        if (ext->Function == GRAPHICS_EXT_FUNC_CODE && ext->ByteCount >= 4)
        {
            // Packed field: bits 2-4 = disposal method, bit 0 = transparent color flag
            uint8_t packed = ext->Bytes[0];
            frame.disposal = static_cast<DisposalMethod>((packed >> 2) & 0x07);

            int delay = (ext->Bytes[2] << 8) | ext->Bytes[1];
            frame.delayMs = std::max(delay * 10, static_cast<int>(minFrameDelayMs)); // Minimum configurable

            // Check transparency flag (bit 0 of packed field)
            if (packed & 0x01)
            {
                frame.transparentIndex = ext->Bytes[3];
            }
            break;
        }
    }

    // Decode pixel data
    ColorMapObject* colorMap = desc->ColorMap ? desc->ColorMap : gif->SColorMap;
    frame.pixels.resize(desc->Width * desc->Height);

    ApplyColorMap(image->RasterBits, colorMap, frame.pixels, desc->Width, desc->Height,
                  frame.transparentIndex);

    // Compose frame onto canvas for this frame
    ComposeFrame(frame, canvas);

    // Store the composed frame result as the final frame pixels
    GifFrame composedFrame = frame;
    composedFrame.width = width;
    composedFrame.height = height;
    composedFrame.offsetX = 0;
    composedFrame.offsetY = 0;
    composedFrame.pixels = canvas;  // Store the full canvas as frame result

    // Debug log
    printf("[GifDecoder] Frame %d: frame.pixels=%zu, canvas.size=%zu, composedFrame.pixels=%zu\n",
           frameIndex, frame.pixels.size(), canvas.size(), composedFrame.pixels.size());

    frames.push_back(composedFrame);  // Copy instead of move to preserve pixels
}

void GifDecoder::Impl::ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                                      std::vector<uint32_t>& pixels, int width, int height,
                                      int transparentIndex)
{
    for (int i = 0; i < width * height; ++i)
    {
        int colorIndex = raster[i];

        // Check if this pixel is transparent
        if (transparentIndex >= 0 && colorIndex == transparentIndex)
        {
            pixels[i] = 0x00000000;  // Fully transparent
        }
        else if (colorMap && colorIndex < colorMap->ColorCount)
        {
            const GifColorType& color = colorMap->Colors[colorIndex];
            // Convert to RGBA32 format: 0xAABBGGRR (little-endian ABGR in memory = RGBA)
            pixels[i] = 0xFF000000 | (color.Blue << 16) | (color.Green << 8) | color.Red;
        }
        else
        {
            pixels[i] = 0xFF000000;  // Opaque black
        }
    }
}

void GifDecoder::Impl::ComposeFrame(const GifFrame& frame, std::vector<uint32_t>& canvas)
{
    // Handle disposal method from previous frame BEFORE compositing new frame
    if (previousDisposal == DisposalMethod::RestoreBackground)
    {
        // Clear only the area of the previous frame to TRANSPARENT to avoid color bleed
        // (UI composes over app background; GIF logical background color can cause fringing)
        for (uint32_t y = 0; y < prevFrameHeight; ++y)
        {
            uint32_t canvasY = prevFrameOffsetY + y;
            if (canvasY >= height)
            {
                continue;
            }
            for (uint32_t x = 0; x < prevFrameWidth; ++x)
            {
                uint32_t canvasX = prevFrameOffsetX + x;
                if (canvasX >= width)
                {
                    continue;
                }
                uint32_t canvasIndex = canvasY * width + canvasX;
                canvas[canvasIndex] = 0x00000000; // fully transparent
            }
        }
    }
    else if (previousDisposal == DisposalMethod::RestorePrevious)
    {
        // Restore to previous state
        if (!previousCanvas.empty())
        {
            canvas = previousCanvas;
        }
    }
    // Note: DoNotDispose and None just leave canvas as-is

    // Save current canvas BEFORE compositing if next frame might need it
    if (frame.disposal == DisposalMethod::RestorePrevious)
    {
        previousCanvas = canvas;
    }

    // Composite current frame onto canvas
    for (uint32_t y = 0; y < frame.height; ++y)
    {
        for (uint32_t x = 0; x < frame.width; ++x)
        {
            uint32_t canvasX = frame.offsetX + x;
            uint32_t canvasY = frame.offsetY + y;

            if (canvasX >= width || canvasY >= height)
            {
                continue;
            }

            uint32_t srcPixel = frame.pixels[y * frame.width + x];
            uint32_t alpha = (srcPixel >> 24) & 0xFF;

            // Skip fully transparent pixels - don't overwrite canvas
            if (alpha == 0)
            {
                continue;
            }

            // Write pixel to canvas
            uint32_t canvasIndex = canvasY * width + canvasX;
            canvas[canvasIndex] = srcPixel;
        }
    }

    // Update disposal method for next iteration
    previousDisposal = frame.disposal;
    // Track current frame rectangle for next RestoreBackground
    prevFrameWidth = frame.width;
    prevFrameHeight = frame.height;
    prevFrameOffsetX = frame.offsetX;
    prevFrameOffsetY = frame.offsetY;
}

GifDecoder::GifDecoder()
    : pImpl(std::make_unique<Impl>()) {
}

GifDecoder::~GifDecoder() = default;

bool GifDecoder::LoadFromFile(const std::string& filePath) {
    return pImpl->LoadGif(filePath);
}

bool GifDecoder::LoadFromUrl(const std::string& url) {
    (void)url;
    // NOTE: URL loading with HTTP download will be implemented in a future release.
    // For now, use LoadFromFile() with local file paths.
    return false;
}

uint32_t GifDecoder::GetFrameCount() const {
    return static_cast<uint32_t>(pImpl->frames.size());
}

const GifFrame& GifDecoder::GetFrame(uint32_t index) const {
    if (index >= pImpl->frames.size()) {
        throw std::out_of_range("Frame index out of range");
    }
    return pImpl->frames[index];
}

uint32_t GifDecoder::GetWidth() const {
    return pImpl->width;
}

uint32_t GifDecoder::GetHeight() const {
    return pImpl->height;
}

bool GifDecoder::IsLooping() const
{
    return pImpl->looping;
}

uint32_t GifDecoder::GetBackgroundColor() const
{
    return pImpl->backgroundColor;
}

void GifBolt::GifDecoder::SetMinFrameDelayMs(uint32_t minDelayMs)
{
    pImpl->minFrameDelayMs = minDelayMs;
}

uint32_t GifBolt::GifDecoder::GetMinFrameDelayMs() const
{
    return pImpl->minFrameDelayMs;
}

}  // namespace GifBolt
