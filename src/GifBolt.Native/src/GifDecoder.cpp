// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "GifDecoder.h"
#include "PixelConversion.h"
#include <gif_lib.h>
#include <fstream>
#include <stdexcept>
#include <cstring>
#include <thread>
#include <mutex>
#include <atomic>

namespace GifBolt {

class GifDecoder::Impl
{
   public:
    std::vector<GifFrame> frames;  ///< Decoded frames cache
    std::vector<bool> frameDecoded;  ///< Track which frames have been decoded
    std::vector<uint32_t> canvas;  ///< Accumulated canvas for frame composition
    DisposalMethod previousDisposal = DisposalMethod::None;  ///< Previous frame disposal
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
    std::vector<uint8_t> bgraPremultipliedCache;  ///< Cache for BGRA premultiplied pixels

    // Background loading support
    GifFileType* gif = nullptr;  ///< GIF file handle after slurp
    uint32_t frameCount = 0;  ///< Total number of frames
    std::string filePath;  ///< Stored for background loading

    std::thread backgroundLoader;  ///< Background thread for DGifSlurp
    std::mutex gifMutex;  ///< Protect gif pointer access
    std::atomic<bool> slurpComplete{false};  ///< Whether DGifSlurp finished
    std::atomic<bool> slurpFailed{false};  ///< Whether DGifSlurp failed

    bool LoadGif(const std::string& filePath);
    void BackgroundSlurp();  ///< Background thread function
    void WaitForSlurp();  ///< Wait for background slurp to complete
    void EnsureFrameDecoded(uint32_t frameIndex);  ///< Decode frame on-demand
    void DecodeFrame(GifFileType* gif, uint32_t frameIndex);
    void ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                       std::vector<uint32_t>& pixels, int width, int height,
                       int transparentIndex = -1);
    void ComposeFrame(const GifFrame& frame, std::vector<uint32_t>& canvas);

    ~Impl()
    {
        if (backgroundLoader.joinable())
        {
            backgroundLoader.join();
        }
        if (this->gif)
        {
            int error = 0;
            DGifCloseFile(this->gif, &error);
            this->gif = nullptr;
        }
    }
};

bool GifDecoder::Impl::LoadGif(const std::string& filePath) {
    this->filePath = filePath;
    int error = 0;
    GifFileType* tempGif = DGifOpenFileName(filePath.c_str(), &error);

    if (!tempGif) {
        return false;
    }

    // Read ONLY header info (instant)
    this->width = tempGif->SWidth;
    this->height = tempGif->SHeight;

    // Extract background color
    if (tempGif->SColorMap && tempGif->SBackGroundColor < tempGif->SColorMap->ColorCount)
    {
        const GifColorType& bgColor = tempGif->SColorMap->Colors[tempGif->SBackGroundColor];
        this->backgroundColor = 0xFF000000 | (bgColor.Blue << 16) | (bgColor.Green << 8) | bgColor.Red;
    }
    else
    {
        this->backgroundColor = 0x00000000;
    }

    DGifCloseFile(tempGif, &error);

    // Launch background thread to do DGifSlurp (heavy operation)
    this->backgroundLoader = std::thread(&Impl::BackgroundSlurp, this);

    // Return IMMEDIATELY - DGifSlurp runs in background
    return true;
}

void GifDecoder::Impl::BackgroundSlurp() {
    int error = 0;
    GifFileType* gif = DGifOpenFileName(this->filePath.c_str(), &error);

    if (!gif) {
        this->slurpFailed = true;
        return;
    }

    // Do the heavy DGifSlurp in background thread
    if (DGifSlurp(gif) == GIF_ERROR) {
        DGifCloseFile(gif, &error);
        this->slurpFailed = true;
        return;
    }

    // Store results under mutex
    {
        std::lock_guard<std::mutex> lock(this->gifMutex);
        this->gif = gif;
        this->frameCount = gif->ImageCount;

        // Check for looping extension
        for (int i = 0; i < gif->ImageCount; ++i) {
            SavedImage* image = &gif->SavedImages[i];
            for (int j = 0; j < image->ExtensionBlockCount; ++j) {
                ExtensionBlock* ext = &image->ExtensionBlocks[j];
                if (ext->Function == APPLICATION_EXT_FUNC_CODE) {
                    if (std::memcmp(ext->Bytes, "NETSCAPE2.0", 11) == 0) {
                        this->looping = true;
                        break;
                    }
                }
            }
        }

        // Initialize frame storage
        this->frames.resize(this->frameCount);
        this->frameDecoded.resize(this->frameCount, false);
        this->canvas.resize(this->width * this->height, 0x00000000);
    }

    this->slurpComplete = true;
}

void GifDecoder::Impl::WaitForSlurp() {
    if (this->backgroundLoader.joinable()) {
        this->backgroundLoader.join();
    }
}

void GifDecoder::Impl::EnsureFrameDecoded(uint32_t frameIndex)
{
    // Wait for background slurp to complete
    this->WaitForSlurp();

    if (this->slurpFailed || !this->gif) {
        return;
    }

    if (frameIndex >= this->frameCount || this->frameDecoded[frameIndex])
    {
        return;
    }

    // Decode all frames up to requested one (for proper composition)
    std::lock_guard<std::mutex> lock(this->gifMutex);
    for (uint32_t i = 0; i <= frameIndex; ++i)
    {
        if (!this->frameDecoded[i])
        {
            this->DecodeFrame(this->gif, i);
            this->frameDecoded[i] = true;
        }
    }
}

void GifDecoder::Impl::DecodeFrame(GifFileType* gif, uint32_t frameIndex) {
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

    frames[frameIndex] = composedFrame;  // Store at specific index instead of push_back
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
    // Wait for background slurp to complete
    pImpl->WaitForSlurp();
    return pImpl->frameCount;
}

const GifFrame& GifDecoder::GetFrame(uint32_t index) const {
    if (index >= pImpl->frameCount) {
        throw std::out_of_range("Frame index out of range");
    }
    // Ensure frame is decoded before returning (lazy loading)
    pImpl->EnsureFrameDecoded(index);
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

const uint8_t* GifDecoder::GetFramePixelsBGRA32Premultiplied(uint32_t index)
{
    if (index >= pImpl->frameCount)
    {
        return nullptr;
    }

    // Ensure frame is decoded (lazy loading)
    pImpl->EnsureFrameDecoded(index);

    const GifFrame& frame = pImpl->frames[index];
    const size_t pixelCount = frame.pixels.size();
    const size_t byteCount = pixelCount * 4;

    // Resize cache if needed
    if (pImpl->bgraPremultipliedCache.size() != byteCount)
    {
        pImpl->bgraPremultipliedCache.resize(byteCount);
    }

    // Convert RGBA to BGRA with premultiplied alpha in one pass
    const uint8_t* sourceRGBA = reinterpret_cast<const uint8_t*>(frame.pixels.data());
    Renderer::PixelFormats::ConvertRGBAToBGRAPremultiplied(sourceRGBA, pImpl->bgraPremultipliedCache.data(), pixelCount);

    return pImpl->bgraPremultipliedCache.data();
}

const uint8_t* GifDecoder::GetFramePixelsBGRA32PremultipliedScaled(uint32_t index, uint32_t targetWidth,
                                                                     uint32_t targetHeight,
                                                                     uint32_t& outWidth,
                                                                     uint32_t& outHeight)
{
    if (index >= pImpl->frameCount)
    {
        return nullptr;
    }

    // Ensure frame is decoded (lazy loading)
    pImpl->EnsureFrameDecoded(index);

    const GifFrame& frame = pImpl->frames[index];
    const uint32_t sourceWidth = frame.width;
    const uint32_t sourceHeight = frame.height;

    // If target size matches source, use non-scaled version
    if (targetWidth == sourceWidth && targetHeight == sourceHeight)
    {
        outWidth = sourceWidth;
        outHeight = sourceHeight;
        return this->GetFramePixelsBGRA32Premultiplied(index);
    }

    outWidth = targetWidth;
    outHeight = targetHeight;
    const size_t outputPixelCount = targetWidth * targetHeight;
    const size_t outputByteCount = outputPixelCount * 4;

    // Resize cache if needed (separate from non-scaled cache)
    static std::vector<uint8_t> scaledCache;
    if (scaledCache.size() != outputByteCount)
    {
        scaledCache.resize(outputByteCount);
    }

    // First, get BGRA premultiplied source (uses existing cache)
    const uint8_t* sourceBGRA = this->GetFramePixelsBGRA32Premultiplied(index);
    if (!sourceBGRA)
    {
        return nullptr;
    }

    // Bilinear scaling (CPU implementation, fast for typical UI sizes)
    const float xRatio = static_cast<float>(sourceWidth) / targetWidth;
    const float yRatio = static_cast<float>(sourceHeight) / targetHeight;

    for (uint32_t y = 0; y < targetHeight; ++y)
    {
        for (uint32_t x = 0; x < targetWidth; ++x)
        {
            // Calculate source position
            const float srcX = x * xRatio;
            const float srcY = y * yRatio;

            // Get integer and fractional parts
            const uint32_t x0 = static_cast<uint32_t>(srcX);
            const uint32_t y0 = static_cast<uint32_t>(srcY);
            const uint32_t x1 = (x0 + 1 < sourceWidth) ? (x0 + 1) : x0;
            const uint32_t y1 = (y0 + 1 < sourceHeight) ? (y0 + 1) : y0;

            const float fracX = srcX - x0;
            const float fracY = srcY - y0;

            // Get four surrounding pixels
            const uint32_t idx00 = (y0 * sourceWidth + x0) * 4;
            const uint32_t idx10 = (y0 * sourceWidth + x1) * 4;
            const uint32_t idx01 = (y1 * sourceWidth + x0) * 4;
            const uint32_t idx11 = (y1 * sourceWidth + x1) * 4;

            // Bilinear interpolation for each channel (BGRA)
            for (int c = 0; c < 4; ++c)
            {
                const float v00 = sourceBGRA[idx00 + c];
                const float v10 = sourceBGRA[idx10 + c];
                const float v01 = sourceBGRA[idx01 + c];
                const float v11 = sourceBGRA[idx11 + c];

                const float vTop = v00 * (1.0f - fracX) + v10 * fracX;
                const float vBottom = v01 * (1.0f - fracX) + v11 * fracX;
                const float vFinal = vTop * (1.0f - fracY) + vBottom * fracY;

                scaledCache[(y * targetWidth + x) * 4 + c] = static_cast<uint8_t>(vFinal + 0.5f);
            }
        }
    }

    return scaledCache.data();
}

}  // namespace GifBolt
