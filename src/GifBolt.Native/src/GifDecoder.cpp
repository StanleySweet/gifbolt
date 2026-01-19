// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "GifDecoder.h"

#include "IDeviceCommandContext.h"
#include "MemoryPool.h"
#include "PixelConversion.h"
#include "ThreadPool.h"
#if defined(__APPLE__)
#include "MetalDeviceCommandContext.h"
#endif
#ifdef _WIN32
#include "D3D11DeviceCommandContext.h"
#endif
#include <gif_lib.h>

#include <atomic>
#include <cstring>
#include <fstream>
#include <mutex>
#include <stdexcept>
#include <thread>

namespace GifBolt
{

class GifDecoder::Impl
{
   public:
    std::vector<GifFrame> _frames;    ///< Decoded frames cache
    std::vector<bool> _frameDecoded;  ///< Track which frames have been decoded
    std::vector<uint32_t> _canvas;    ///< Accumulated canvas for frame composition
    DisposalMethod _previousDisposal = DisposalMethod::None;  ///< Previous frame disposal
    uint32_t _prevFrameWidth = 0;
    uint32_t _prevFrameHeight = 0;
    uint32_t _prevFrameOffsetX = 0;
    uint32_t _prevFrameOffsetY = 0;
    uint32_t _minFrameDelayMs = 10;         ///< Délai minimal configurable
    std::vector<uint32_t> _previousCanvas;  ///< Saved canvas for RestorePrevious
    uint32_t _width = 0;
    uint32_t _height = 0;
    uint32_t _backgroundColor = 0xFF000000;  ///< Default: opaque black
    bool _looping = false;
    std::vector<uint8_t> _bgraPremultipliedCache;  ///< Cache for BGRA premultiplied pixels
    std::shared_ptr<Renderer::IDeviceCommandContext> _deviceContext;  ///< GPU context for scaling

    // Background loading support
    GifFileType* _gif = nullptr;  ///< GIF file handle after slurp
    uint32_t _frameCount = 0;     ///< Total number of frames
    std::string _filePath;        ///< Stored for background loading

    std::thread _backgroundLoader;            ///< Background thread for DGifSlurp
    std::mutex _gifMutex;                     ///< Protect gif pointer access
    std::atomic<bool> _slurpComplete{false};  ///< Whether DGifSlurp finished
    std::atomic<bool> _slurpFailed{false};    ///< Whether DGifSlurp failed

    // Memory optimization: PMR allocator pool for frame data
    Memory::FrameMemoryPool _framePool;  ///< PMR pool for frame allocations
    Memory::ArenaAllocator _tempArena;   ///< Arena for temporary decode buffers

    // Thread pool for parallel frame decoding
    std::unique_ptr<ThreadPool> _threadPool;  ///< Thread pool for parallel decoding
    std::mutex _decodeMutex;                  ///< Protect frame decoding state

    // Async prefetching support
    std::atomic<bool> _prefetchingEnabled{true};      ///< Enable/disable prefetching
    std::atomic<uint32_t> _currentPlaybackFrame{0};   ///< Current frame being displayed
    std::thread _prefetchThread;                      ///< Background thread for frame prefetching
    std::atomic<bool> _prefetchThreadRunning{false};  ///< Prefetch thread active
    std::mutex _prefetchMutex;                        ///< Protect prefetch state
    static constexpr uint32_t PREFETCH_AHEAD = 5;     ///< Number of frames to decode ahead

    bool LoadGif(const std::string& filePath);
    void BackgroundSlurp();                        ///< Background thread function
    void WaitForSlurp();                           ///< Wait for background slurp to complete
    void EnsureFrameDecoded(uint32_t frameIndex);  ///< Decode frame on-demand
    void DecodeFrame(GifFileType* gif, uint32_t frameIndex);
    void ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                       std::vector<uint32_t>& pixels, int width, int height,
                       int transparentIndex = -1);
    void ComposeFrame(const GifFrame& frame, std::vector<uint32_t>& canvas);

    // Async prefetching methods
    void StartPrefetching(uint32_t startFrame);  ///< Start background prefetch
    void StopPrefetching();                      ///< Stop background prefetch thread
    void PrefetchLoop();                         ///< Prefetch thread worker function

    ~Impl()
    {
        // Stop prefetch thread first
        this->StopPrefetching();

        if (_backgroundLoader.joinable())
        {
            _backgroundLoader.join();
        }
        if (this->_gif)
        {
            int error = 0;
            DGifCloseFile(this->_gif, &error);
            this->_gif = nullptr;
        }
    }
};

bool GifDecoder::Impl::LoadGif(const std::string& filePath)
{
    this->_filePath = filePath;
    int error = 0;
    GifFileType* tempGif = DGifOpenFileName(filePath.c_str(), &error);

    if (!tempGif)
    {
        return false;
    }

    // Read ONLY header info (instant)
    this->_width = tempGif->SWidth;
    this->_height = tempGif->SHeight;

    // Extract background color
    if (tempGif->SColorMap && tempGif->SBackGroundColor < tempGif->SColorMap->ColorCount)
    {
        const GifColorType& bgColor = tempGif->SColorMap->Colors[tempGif->SBackGroundColor];
        this->_backgroundColor =
            0xFF000000 | (bgColor.Blue << 16) | (bgColor.Green << 8) | bgColor.Red;
    }
    else
    {
        this->_backgroundColor = 0x00000000;
    }

    DGifCloseFile(tempGif, &error);

    // Initialize thread pool for parallel frame decoding
    // Use hardware_concurrency - 1 to leave one thread for main work
    size_t numThreads = std::max(1u, std::thread::hardware_concurrency() - 1);
    this->_threadPool = std::make_unique<ThreadPool>(numThreads);

    // Launch background thread to do DGifSlurp (heavy operation)
    this->_backgroundLoader = std::thread(&Impl::BackgroundSlurp, this);

    // Return IMMEDIATELY - DGifSlurp runs in background
    return true;
}

void GifDecoder::Impl::BackgroundSlurp()
{
    int error = 0;
    GifFileType* gif = DGifOpenFileName(this->_filePath.c_str(), &error);

    if (!gif)
    {
        this->_slurpFailed = true;
        return;
    }

    // Do the heavy DGifSlurp in background thread
    if (DGifSlurp(gif) == GIF_ERROR)
    {
        DGifCloseFile(gif, &error);
        this->_slurpFailed = true;
        return;
    }

    // Store results under mutex
    {
        std::lock_guard<std::mutex> lock(this->_gifMutex);
        this->_gif = gif;
        this->_frameCount = gif->ImageCount;

        // Check for looping extension
        for (int i = 0; i < gif->ImageCount; ++i)
        {
            SavedImage* image = &gif->SavedImages[i];
            for (int j = 0; j < image->ExtensionBlockCount; ++j)
            {
                ExtensionBlock* ext = &image->ExtensionBlocks[j];
                if (ext->Function == APPLICATION_EXT_FUNC_CODE)
                {
                    if (std::memcmp(ext->Bytes, "NETSCAPE2.0", 11) == 0)
                    {
                        this->_looping = true;
                        break;
                    }
                }
            }
        }

        // Initialize frame storage
        this->_frames.resize(this->_frameCount);
        this->_frameDecoded.resize(this->_frameCount, false);
        this->_canvas.resize(this->_width * this->_height, 0x00000000);
    }

    this->_slurpComplete = true;
}

void GifDecoder::Impl::WaitForSlurp()
{
    if (this->_backgroundLoader.joinable())
    {
        this->_backgroundLoader.join();
    }
}

void GifDecoder::Impl::EnsureFrameDecoded(uint32_t frameIndex)
{
    // Wait for background slurp to complete
    this->WaitForSlurp();

    if (this->_slurpFailed || !this->_gif)
    {
        return;
    }

    if (frameIndex >= this->_frameCount || this->_frameDecoded[frameIndex])
    {
        return;
    }

    // Decode frames in parallel batches
    // We need to decode sequentially due to frame composition (each frame depends on previous)
    // BUT we can decode multiple frames ahead in parallel once dependencies are met
    std::lock_guard<std::mutex> lock(this->_decodeMutex);

    // Sequential decode up to requested frame (required for correct composition)
    for (uint32_t i = 0; i <= frameIndex; ++i)
    {
        if (!this->_frameDecoded[i])
        {
            this->DecodeFrame(this->_gif, i);
            this->_frameDecoded[i] = true;
        }
    }

    // Opportunistic background decode: submit next few frames to thread pool
    // This only helps for sequential access patterns (common in GIF playback)
    constexpr uint32_t OPPORTUNISTIC_AHEAD = 3;
    for (uint32_t ahead = 1;
         ahead <= OPPORTUNISTIC_AHEAD && (frameIndex + ahead) < this->_frameCount; ++ahead)
    {
        uint32_t nextFrame = frameIndex + ahead;
        if (!this->_frameDecoded[nextFrame] && this->_threadPool)
        {
            // Check if all previous frames are decoded (dependency check)
            bool canDecode = true;
            for (uint32_t dep = 0; dep < nextFrame; ++dep)
            {
                if (!this->_frameDecoded[dep])
                {
                    canDecode = false;
                    break;
                }
            }

            if (canDecode)
            {
                // Submit to thread pool - will execute when worker is available
                this->_threadPool->Enqueue(
                    [this, nextFrame]()
                    {
                        std::lock_guard<std::mutex> decodeLock(this->_decodeMutex);
                        if (!this->_frameDecoded[nextFrame])
                        {
                            this->DecodeFrame(this->_gif, nextFrame);
                            this->_frameDecoded[nextFrame] = true;
                        }
                    });
            }
        }
    }
}

void GifDecoder::Impl::DecodeFrame(GifFileType* gif, uint32_t frameIndex)
{
    SavedImage* image = &gif->SavedImages[frameIndex];
    GifImageDesc* desc = &image->ImageDesc;

    GifFrame frame;
    frame.width = desc->Width;
    frame.height = desc->Height;
    frame.offsetX = desc->Left;
    frame.offsetY = desc->Top;
    frame.delayMs = 100;  // Default delay
    frame.disposal = DisposalMethod::None;
    frame.transparentIndex = -1;  // No transparency by default

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
            frame.delayMs =
                std::max(delay * 10, static_cast<int>(_minFrameDelayMs));  // Minimum configurable

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
    const size_t pixelCount = desc->Width * desc->Height;
    frame.pixels.reserve(pixelCount);  // Pre-allocate to avoid reallocation
    frame.pixels.resize(pixelCount);

    ApplyColorMap(image->RasterBits, colorMap, frame.pixels, desc->Width, desc->Height,
                  frame.transparentIndex);

    // Compose frame onto canvas for this frame
    ComposeFrame(frame, _canvas);

    // Store the composed frame result as the final frame pixels
    GifFrame composedFrame = frame;
    composedFrame.width = _width;
    composedFrame.height = _height;
    composedFrame.offsetX = 0;
    composedFrame.offsetY = 0;
    // Move canvas to avoid copying millions of pixels
    composedFrame.pixels = _canvas;  // Still copy here for composition continuity

    _frames[frameIndex] = std::move(composedFrame);  // Move instead of copy
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
    if (_previousDisposal == DisposalMethod::RestoreBackground)
    {
        // Clear only the area of the previous frame to TRANSPARENT to avoid color bleed
        // (UI composes over app background; GIF logical background color can cause fringing)
        for (uint32_t y = 0; y < _prevFrameHeight; ++y)
        {
            uint32_t canvasY = _prevFrameOffsetY + y;
            if (canvasY >= _height)
            {
                continue;
            }
            for (uint32_t x = 0; x < _prevFrameWidth; ++x)
            {
                uint32_t canvasX = _prevFrameOffsetX + x;
                if (canvasX >= _width)
                {
                    continue;
                }
                uint32_t canvasIndex = canvasY * _width + canvasX;
                canvas[canvasIndex] = 0x00000000;  // fully transparent
            }
        }
    }
    else if (_previousDisposal == DisposalMethod::RestorePrevious)
    {
        // Restore to previous state
        if (!_previousCanvas.empty())
        {
            canvas = _previousCanvas;
        }
    }
    // Note: DoNotDispose and None just leave canvas as-is

    // Save current canvas BEFORE compositing if next frame might need it
    if (frame.disposal == DisposalMethod::RestorePrevious)
    {
        _previousCanvas = canvas;
    }

    // Composite current frame onto canvas
    for (uint32_t y = 0; y < frame.height; ++y)
    {
        for (uint32_t x = 0; x < frame.width; ++x)
        {
            uint32_t canvasX = frame.offsetX + x;
            uint32_t canvasY = frame.offsetY + y;

            if (canvasX >= _width || canvasY >= _height)
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
            uint32_t canvasIndex = canvasY * _width + canvasX;
            canvas[canvasIndex] = srcPixel;
        }
    }

    // Update disposal method for next iteration
    _previousDisposal = frame.disposal;
    // Track current frame rectangle for next RestoreBackground
    _prevFrameWidth = frame.width;
    _prevFrameHeight = frame.height;
    _prevFrameOffsetX = frame.offsetX;
    _prevFrameOffsetY = frame.offsetY;
}

GifDecoder::GifDecoder() : _pImpl(std::make_unique<Impl>())
{
    // Initialize GPU context for hardware-accelerated scaling
#if defined(__APPLE__)
    try
    {
        _pImpl->_deviceContext = std::make_shared<Renderer::MetalDeviceCommandContext>();
    }
    catch (...)
    {
        // GPU context initialization failed, will use CPU fallback
        _pImpl->_deviceContext = nullptr;
    }
    // Initialize GPU context for hardware-accelerated scaling
#elif defined(WIN32)
    try
    {
        _pImpl->_deviceContext = std::make_shared<Renderer::D3D11DeviceCommandContext>();
    }
    catch (...)
    {
        // GPU context initialization failed, will use CPU fallback
        _pImpl->_deviceContext = nullptr;
    }
#endif
}

GifDecoder::~GifDecoder() = default;

bool GifDecoder::LoadFromFile(const std::string& filePath)
{
    return _pImpl->LoadGif(filePath);
}

bool GifDecoder::LoadFromUrl(const std::string& url)
{
    (void)url;
    // NOTE: URL loading with HTTP download will be implemented in a future release.
    // For now, use LoadFromFile() with local file paths.
    return false;
}

uint32_t GifDecoder::GetFrameCount() const
{
    // Wait for background slurp to complete
    _pImpl->WaitForSlurp();
    return _pImpl->_frameCount;
}

const GifFrame& GifDecoder::GetFrame(uint32_t index) const
{
    if (index >= _pImpl->_frameCount)
    {
        throw std::out_of_range("Frame index out of range");
    }
    // Ensure frame is decoded before returning (lazy loading)
    _pImpl->EnsureFrameDecoded(index);
    return _pImpl->_frames[index];
}

uint32_t GifDecoder::GetWidth() const
{
    return _pImpl->_width;
}

uint32_t GifDecoder::GetHeight() const
{
    return _pImpl->_height;
}

bool GifDecoder::IsLooping() const
{
    return _pImpl->_looping;
}

uint32_t GifDecoder::GetBackgroundColor() const
{
    return _pImpl->_backgroundColor;
}

void GifBolt::GifDecoder::SetMinFrameDelayMs(uint32_t minDelayMs)
{
    _pImpl->_minFrameDelayMs = minDelayMs;
}

uint32_t GifBolt::GifDecoder::GetMinFrameDelayMs() const
{
    return _pImpl->_minFrameDelayMs;
}

const uint8_t* GifDecoder::GetFramePixelsBGRA32Premultiplied(uint32_t index)
{
    if (index >= _pImpl->_frameCount)
    {
        return nullptr;
    }

    // Ensure frame is decoded (lazy loading)
    _pImpl->EnsureFrameDecoded(index);

    // Check if decode succeeded
    if (!_pImpl->_frameDecoded[index] || _pImpl->_frames[index].pixels.empty())
    {
        return nullptr;
    }

    const GifFrame& frame = _pImpl->_frames[index];
    const size_t pixelCount = frame.pixels.size();
    const size_t byteCount = pixelCount * 4;

    // Resize cache if needed
    if (_pImpl->_bgraPremultipliedCache.size() != byteCount)
    {
        _pImpl->_bgraPremultipliedCache.resize(byteCount);
    }

    // Convert RGBA to BGRA with premultiplied alpha in one pass
    const uint8_t* sourceRGBA = reinterpret_cast<const uint8_t*>(frame.pixels.data());
    Renderer::PixelFormats::ConvertRGBAToBGRAPremultiplied(
        sourceRGBA, _pImpl->_bgraPremultipliedCache.data(), pixelCount);

    return _pImpl->_bgraPremultipliedCache.data();
}

const uint8_t* GifDecoder::GetFramePixelsBGRA32PremultipliedScaled(
    uint32_t index, uint32_t targetWidth, uint32_t targetHeight, uint32_t& outWidth,
    uint32_t& outHeight, ScalingFilter filter)
{
    if (index >= _pImpl->_frameCount)
    {
        return nullptr;
    }

    // Ensure frame is decoded (lazy loading)
    _pImpl->EnsureFrameDecoded(index);

    const GifFrame& frame = _pImpl->_frames[index];
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

    // Try GPU scaling first if available
    if (_pImpl->_deviceContext)
    {
        bool gpuSuccess = _pImpl->_deviceContext->ScaleImageGPU(
            sourceBGRA, sourceWidth, sourceHeight, scaledCache.data(), targetWidth, targetHeight,
            static_cast<int>(filter));

        if (gpuSuccess)
        {
            return scaledCache.data();
        }
        // If GPU fails, fall back to CPU
    }

    // CPU scaling implementation based on filter type
    const float xRatio = static_cast<float>(sourceWidth) / targetWidth;
    const float yRatio = static_cast<float>(sourceHeight) / targetHeight;

    switch (filter)
    {
        case ScalingFilter::Nearest:
            // Nearest-neighbor (point sampling) - fastest
            for (uint32_t y = 0; y < targetHeight; ++y)
            {
                for (uint32_t x = 0; x < targetWidth; ++x)
                {
                    const uint32_t srcX = static_cast<uint32_t>(x * xRatio);
                    const uint32_t srcY = static_cast<uint32_t>(y * yRatio);
                    const uint32_t srcIdx = (srcY * sourceWidth + srcX) * 4;
                    const uint32_t dstIdx = (y * targetWidth + x) * 4;

                    scaledCache[dstIdx + 0] = sourceBGRA[srcIdx + 0];
                    scaledCache[dstIdx + 1] = sourceBGRA[srcIdx + 1];
                    scaledCache[dstIdx + 2] = sourceBGRA[srcIdx + 2];
                    scaledCache[dstIdx + 3] = sourceBGRA[srcIdx + 3];
                }
            }
            break;

        case ScalingFilter::Bilinear:
        default:
            // Bilinear interpolation - good balance
            for (uint32_t y = 0; y < targetHeight; ++y)
            {
                for (uint32_t x = 0; x < targetWidth; ++x)
                {
                    const float srcX = x * xRatio;
                    const float srcY = y * yRatio;

                    const uint32_t x0 = static_cast<uint32_t>(srcX);
                    const uint32_t y0 = static_cast<uint32_t>(srcY);
                    const uint32_t x1 = (x0 + 1 < sourceWidth) ? (x0 + 1) : x0;
                    const uint32_t y1 = (y0 + 1 < sourceHeight) ? (y0 + 1) : y0;

                    const float fracX = srcX - x0;
                    const float fracY = srcY - y0;

                    const uint32_t idx00 = (y0 * sourceWidth + x0) * 4;
                    const uint32_t idx10 = (y0 * sourceWidth + x1) * 4;
                    const uint32_t idx01 = (y1 * sourceWidth + x0) * 4;
                    const uint32_t idx11 = (y1 * sourceWidth + x1) * 4;

                    for (int c = 0; c < 4; ++c)
                    {
                        const float v00 = sourceBGRA[idx00 + c];
                        const float v10 = sourceBGRA[idx10 + c];
                        const float v01 = sourceBGRA[idx01 + c];
                        const float v11 = sourceBGRA[idx11 + c];

                        const float vTop = v00 * (1.0f - fracX) + v10 * fracX;
                        const float vBottom = v01 * (1.0f - fracX) + v11 * fracX;
                        const float vFinal = vTop * (1.0f - fracY) + vBottom * fracY;

                        scaledCache[(y * targetWidth + x) * 4 + c] =
                            static_cast<uint8_t>(vFinal + 0.5f);
                    }
                }
            }
            break;

        case ScalingFilter::Bicubic:
        {
            // Bicubic (Catmull-Rom) interpolation - higher quality
            auto cubicWeight = [](float x) -> float
            {
                const float a = -0.5f;  // Catmull-Rom parameter
                const float absX = std::abs(x);
                if (absX <= 1.0f)
                {
                    return ((a + 2.0f) * absX - (a + 3.0f)) * absX * absX + 1.0f;
                }
                else if (absX < 2.0f)
                {
                    return ((a * absX - 5.0f * a) * absX + 8.0f * a) * absX - 4.0f * a;
                }
                return 0.0f;
            };

            for (uint32_t y = 0; y < targetHeight; ++y)
            {
                for (uint32_t x = 0; x < targetWidth; ++x)
                {
                    const float srcX = x * xRatio;
                    const float srcY = y * yRatio;
                    const int x0 = static_cast<int>(srcX);
                    const int y0 = static_cast<int>(srcY);
                    const float dx = srcX - x0;
                    const float dy = srcY - y0;

                    float result[4] = {0.0f, 0.0f, 0.0f, 0.0f};
                    float weightSum = 0.0f;

                    // Sample 4x4 neighborhood
                    for (int j = -1; j <= 2; ++j)
                    {
                        for (int i = -1; i <= 2; ++i)
                        {
                            const int sx =
                                std::min(std::max(x0 + i, 0), static_cast<int>(sourceWidth) - 1);
                            const int sy =
                                std::min(std::max(y0 + j, 0), static_cast<int>(sourceHeight) - 1);
                            const float wx = cubicWeight(i - dx);
                            const float wy = cubicWeight(j - dy);
                            const float weight = wx * wy;

                            const uint32_t srcIdx = (sy * sourceWidth + sx) * 4;
                            for (int c = 0; c < 4; ++c)
                            {
                                result[c] += sourceBGRA[srcIdx + c] * weight;
                            }
                            weightSum += weight;
                        }
                    }

                    const uint32_t dstIdx = (y * targetWidth + x) * 4;
                    if (weightSum > 0.0f)
                    {
                        for (int c = 0; c < 4; ++c)
                        {
                            scaledCache[dstIdx + c] = static_cast<uint8_t>(
                                std::min(std::max(result[c] / weightSum, 0.0f), 255.0f));
                        }
                    }
                }
            }
            break;
        }

        case ScalingFilter::Lanczos:
        {
            // Lanczos-3 resampling - highest quality
            const float a = 3.0f;
            auto lanczosWeight = [](float x, float a) -> float
            {
                if (std::abs(x) < 0.001f)
                    return 1.0f;
                if (std::abs(x) >= a)
                    return 0.0f;
                const float pi = 3.14159265359f;
                const float piX = pi * x;
                return a * std::sin(piX) * std::sin(piX / a) / (piX * piX);
            };

            for (uint32_t y = 0; y < targetHeight; ++y)
            {
                for (uint32_t x = 0; x < targetWidth; ++x)
                {
                    const float srcX = x * xRatio;
                    const float srcY = y * yRatio;
                    const int x0 = static_cast<int>(srcX);
                    const int y0 = static_cast<int>(srcY);
                    const float dx = srcX - x0;
                    const float dy = srcY - y0;

                    float result[4] = {0.0f, 0.0f, 0.0f, 0.0f};
                    float weightSum = 0.0f;

                    const int radius = static_cast<int>(std::ceil(a));
                    for (int j = -radius; j <= radius; ++j)
                    {
                        for (int i = -radius; i <= radius; ++i)
                        {
                            const int sx =
                                std::min(std::max(x0 + i, 0), static_cast<int>(sourceWidth) - 1);
                            const int sy =
                                std::min(std::max(y0 + j, 0), static_cast<int>(sourceHeight) - 1);
                            const float wx = lanczosWeight(i - dx, a);
                            const float wy = lanczosWeight(j - dy, a);
                            const float weight = wx * wy;

                            const uint32_t srcIdx = (sy * sourceWidth + sx) * 4;
                            for (int c = 0; c < 4; ++c)
                            {
                                result[c] += sourceBGRA[srcIdx + c] * weight;
                            }
                            weightSum += weight;
                        }
                    }

                    const uint32_t dstIdx = (y * targetWidth + x) * 4;
                    if (weightSum > 0.0f)
                    {
                        for (int c = 0; c < 4; ++c)
                        {
                            scaledCache[dstIdx + c] = static_cast<uint8_t>(
                                std::min(std::max(result[c] / weightSum, 0.0f), 255.0f));
                        }
                    }
                }
            }
            break;
        }
    }

    return scaledCache.data();
}

// Async prefetching implementations
void GifDecoder::Impl::StartPrefetching(uint32_t startFrame)
{
    if (!_prefetchingEnabled || _prefetchThreadRunning)
    {
        return;
    }

    _currentPlaybackFrame = startFrame;
    _prefetchThreadRunning = true;
    _prefetchThread = std::thread(&Impl::PrefetchLoop, this);
}

void GifDecoder::Impl::StopPrefetching()
{
    _prefetchThreadRunning = false;
    if (_prefetchThread.joinable())
    {
        _prefetchThread.join();
    }
}

void GifDecoder::Impl::PrefetchLoop()
{
    while (_prefetchThreadRunning)
    {
        uint32_t currentFrame = _currentPlaybackFrame.load();

        // Prefetch next N frames
        for (uint32_t ahead = 1; ahead <= PREFETCH_AHEAD && _prefetchThreadRunning; ++ahead)
        {
            uint32_t targetFrame = (currentFrame + ahead) % _frameCount;

            // Check if already decoded
            if (!_frameDecoded[targetFrame])
            {
                // Decode frame in background
                EnsureFrameDecoded(targetFrame);
            }
        }

        // Sleep briefly to avoid busy loop
        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
}

// Public wrapper methods for prefetch control
void GifDecoder::StartPrefetching(uint32_t startFrame)
{
    if (this->_pImpl)
    {
        this->_pImpl->StartPrefetching(startFrame);
    }
}

void GifDecoder::StopPrefetching()
{
    if (this->_pImpl)
    {
        this->_pImpl->StopPrefetching();
    }
}

void GifDecoder::SetCurrentFrame(uint32_t currentFrame)
{
    if (this->_pImpl)
    {
        this->_pImpl->_currentPlaybackFrame = currentFrame;
    }
}

}  // namespace GifBolt
