// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "GifDecoder.h"

#include "DebugLog.h"
#include "IDeviceCommandContext.h"
#include "ITexture.h"
#include "MemoryPool.h"
#include "PixelConversion.h"
#include "ThreadPool.h"
#include "DummyDeviceCommandContext.h"
#if defined(__APPLE__)
#include "MetalDeviceCommandContext.h"
#endif
#ifdef _WIN32
#include "D3D11DeviceCommandContext.h"
#include "D3D9ExDeviceCommandContext.h"
#include <windows.h>
#endif
#include <gif_lib.h>

#include <algorithm>
#include <atomic>
#include <cstring>
#include <fstream>
#include <mutex>
#include <stdexcept>
#include <thread>
#include <unordered_map>

namespace GifBolt
{

namespace
{
struct MemoryBufferContext
{
    const uint8_t* data = nullptr;
    size_t length = 0;
    size_t offset = 0;
};

int ReadFromMemory(GifFileType* gif, GifByteType* destination, int size)
{
    if ((gif == nullptr) || (destination == nullptr) || (size <= 0))
    {
        return GIF_ERROR;
    }

    auto* context = static_cast<MemoryBufferContext*>(gif->UserData);
    if (context == nullptr)
    {
        return GIF_ERROR;
    }

    size_t requested = static_cast<size_t>(size);
    size_t remaining = 0;
    if (context->offset <= context->length)
    {
        remaining = context->length - context->offset;
    }

    size_t toCopy = std::min(requested, remaining);
    if (toCopy == 0)
    {
        return 0;  // EOF
    }

    std::memcpy(destination, context->data + context->offset, toCopy);
    context->offset += toCopy;
    return static_cast<int>(toCopy);
}
}  // namespace

class GifDecoder::Impl
{
   public:
    enum class SourceKind
    {
        None = 0,
        File = 1,
        Memory = 2
    };

    // Lazy frame caching: store only N frames instead of all frames
    uint32_t MAX_CACHED_FRAMES = 10;  ///< Maximum frames to cache in memory
    std::vector<GifFrame> _frameCache;   ///< LRU cache for decoded frames
    std::vector<uint32_t> _cachedFrameIndices;  ///< Indices of frames in cache (for LRU tracking)
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
    std::unordered_map<uint32_t, std::shared_ptr<Renderer::ITexture>> _textureCache;  ///< GPU texture cache per frame

    // Background loading support
    GifFileType* _gif = nullptr;  ///< GIF file handle after slurp
    uint32_t _frameCount = 0;     ///< Total number of frames
    std::string _filePath;        ///< Stored for background loading
    std::shared_ptr<void> _gifUserData;  ///< Keeps memory source alive for giflib callbacks

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

    SourceKind _sourceKind = SourceKind::None;  ///< Current source type
    std::vector<uint8_t> _memoryData;           ///< Memory-backed GIF bytes

    bool LoadGif(const std::string& filePath);
    bool LoadGifFromMemory(const uint8_t* data, size_t length);
    bool LoadFromCurrentSource();
    GifFileType* OpenGif(int& error, std::shared_ptr<void>& userDataHolder);
    void BackgroundSlurp();                        ///< Background thread function
    void WaitForSlurp();                           ///< Wait for background slurp to complete
    void EnsureFrameDecoded(uint32_t frameIndex);  ///< Decode frame on-demand
    void DecodeFrame(GifFileType* gif, uint32_t frameIndex);
    void ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                       std::vector<uint32_t>& pixels, int width, int height,
                       int transparentIndex = -1);
    void ComposeFrame(const GifFrame& frame, std::vector<uint32_t>& canvas);

    /// \brief Retrieve a frame from cache, loading if necessary.
    /// Uses LRU eviction to maintain memory bounds.
    GifFrame& GetOrDecodeFrame(uint32_t frameIndex);

    // Async prefetching methods
    void StartPrefetching(uint32_t startFrame);  ///< Start background prefetch
    void StopPrefetching();                      ///< Stop background prefetch thread
    void PrefetchLoop();                         ///< Prefetch thread worker function

    /// \brief Gets or creates a GPU texture for the specified frame.
    /// \param frameIndex The frame index.
    /// \return Shared pointer to the texture, or nullptr if unavailable.
    std::shared_ptr<Renderer::ITexture> GetOrCreateTexture(uint32_t frameIndex);

    ~Impl()
    {
        // Stop prefetch thread first
        this->StopPrefetching();

        // Clear texture cache before destroying device context
        _textureCache.clear();

        // Reset device context to release GPU resources
        _deviceContext.reset();

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
        this->_gifUserData.reset();
    }
};

bool GifDecoder::Impl::LoadGif(const std::string& filePath)
{
    this->_sourceKind = SourceKind::File;
    this->_filePath = filePath;
    this->_memoryData.clear();
    return this->LoadFromCurrentSource();
}

bool GifDecoder::Impl::LoadGifFromMemory(const uint8_t* data, size_t length)
{
    if ((data == nullptr) || (length == 0))
    {
        return false;
    }

    this->_sourceKind = SourceKind::Memory;
    this->_filePath.clear();
    this->_memoryData.assign(data, data + length);
    return this->LoadFromCurrentSource();
}

GifFileType* GifDecoder::Impl::OpenGif(int& error, std::shared_ptr<void>& userDataHolder)
{
    switch (this->_sourceKind)
    {
        case SourceKind::File:
            return DGifOpenFileName(this->_filePath.c_str(), &error);

        case SourceKind::Memory:
        {
            if (this->_memoryData.empty())
            {
                return nullptr;
            }

            auto context = std::make_shared<MemoryBufferContext>();
            context->data = this->_memoryData.data();
            context->length = this->_memoryData.size();
            context->offset = 0;

            GifFileType* gif =
                DGifOpen(static_cast<void*>(context.get()), &ReadFromMemory, &error);
            if (gif != nullptr)
            {
                gif->UserData = context.get();
                userDataHolder = context;
            }

            return gif;
        }

        default:
            return nullptr;
    }
}

bool GifDecoder::Impl::LoadFromCurrentSource()
{
    if (this->_sourceKind == SourceKind::None)
    {
        return false;
    }

    this->StopPrefetching();
    this->WaitForSlurp();

    if (this->_gif != nullptr)
    {
        int closeError = 0;
        DGifCloseFile(this->_gif, &closeError);
        this->_gif = nullptr;
    }

    this->_gifUserData.reset();
    this->_frameCache.clear();
    this->_cachedFrameIndices.clear();
    this->_frameDecoded.clear();
    this->_canvas.clear();
    this->_bgraPremultipliedCache.clear();
    this->_looping = false;
    this->_frameCount = 0;
    this->_width = 0;
    this->_height = 0;
    this->_slurpComplete = false;
    this->_slurpFailed = false;

    int error = 0;
    std::shared_ptr<void> headerUserData;
    GifFileType* tempGif = this->OpenGif(error, headerUserData);

    if (tempGif == nullptr)
    {
        return false;
    }

    this->_width = tempGif->SWidth;
    this->_height = tempGif->SHeight;

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
    headerUserData.reset();

    size_t numThreads = std::max(1u, std::thread::hardware_concurrency() - 1);
    this->_threadPool = std::make_unique<ThreadPool>(numThreads);

    this->_backgroundLoader = std::thread(&Impl::BackgroundSlurp, this);

    return true;
}

void GifDecoder::Impl::BackgroundSlurp()
{
    int error = 0;
    std::shared_ptr<void> userData;
    GifFileType* gif = this->OpenGif(error, userData);

    if (gif == nullptr)
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
        this->_gifUserData = userData;
        this->_frameCount = gif->ImageCount;

        // Check for looping extension
        for (int i = 0; i < gif->ImageCount && !this->_looping; ++i)
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

        // Initialize frame storage: use LRU cache instead of storing all frames
        this->_frameDecoded.resize(this->_frameCount, false);
        this->_frameCache.clear();
        this->_cachedFrameIndices.clear();
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

GifFrame& GifDecoder::Impl::GetOrDecodeFrame(uint32_t frameIndex)
{
    // Check if frame is already in cache
    for (size_t i = 0; i < this->_cachedFrameIndices.size(); ++i)
    {
        if (this->_cachedFrameIndices[i] == frameIndex)
        {
            // Move to end (most recently used)
            std::rotate(this->_cachedFrameIndices.begin() + i,
                       this->_cachedFrameIndices.begin() + i + 1,
                       this->_cachedFrameIndices.end());
            std::rotate(this->_frameCache.begin() + i,
                       this->_frameCache.begin() + i + 1,
                       this->_frameCache.end());
            return this->_frameCache.back();
        }
    }

    // Frame not in cache - need to decode it
    // First ensure frame is decoded to get raw pixel data
    this->EnsureFrameDecoded(frameIndex);

    // Create or get the frame from the full decode buffer
    GifFrame newFrame;
    if (frameIndex < this->_frameDecoded.size() && this->_frameDecoded[frameIndex])
    {
        // Frame was decoded - get it from the GIF structure
        SavedImage* image = &this->_gif->SavedImages[frameIndex];

        // Copy pixel data (assuming it was already set in EnsureFrameDecoded)
        newFrame.width = this->_width;      // Full canvas width for composed frame
        newFrame.height = this->_height;    // Full canvas height for composed frame
        newFrame.offsetX = 0;               // Composed frame is already on full canvas
        newFrame.offsetY = 0;               // Composed frame is already on full canvas
        newFrame.transparentIndex = -1;
        newFrame.disposal = DisposalMethod::None;

        // Get the actual frame delay from the GIF extension data
        newFrame.delayMs = 10;  // Default fallback: 10ms (GIF standard minimum)
        if (image->ExtensionBlockCount > 0)
        {
            for (int i = 0; i < image->ExtensionBlockCount; ++i)
            {
                if (image->ExtensionBlocks[i].Function == GRAPHICS_EXT_FUNC_CODE &&
                    image->ExtensionBlocks[i].ByteCount >= 4)
                {
                    int delay = (image->ExtensionBlocks[i].Bytes[2] << 8) | image->ExtensionBlocks[i].Bytes[1];
                    newFrame.delayMs = std::max(delay * 10, static_cast<int>(this->_minFrameDelayMs));
                    break;
                }
            }
        }

        // Deep copy the pixel data from _canvas to prevent stale data on loop
        // _canvas is reused across frame compositions, so we must copy the data
        newFrame.pixels = std::vector<uint32_t>(this->_canvas.begin(), this->_canvas.end());
    }

    // Add to cache
    this->_frameCache.push_back(newFrame);
    this->_cachedFrameIndices.push_back(frameIndex);

    // Evict least recently used if cache is full
    if (this->_cachedFrameIndices.size() > this->MAX_CACHED_FRAMES)
    {
        this->_frameCache.erase(this->_frameCache.begin());
        this->_cachedFrameIndices.erase(this->_cachedFrameIndices.begin());
    }

    return this->_frameCache.back();
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
    frame.delayMs = 10;  // Default delay: 10ms (GIF standard minimum)
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

    // Note: We don't store in _frames anymore - that's handled by GetOrDecodeFrame in the cache
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
    // No device context - CPU-only decoding
    _pImpl->_deviceContext = nullptr;
}

GifDecoder::GifDecoder(Renderer::Backend backend) : _pImpl(std::make_unique<Impl>())
{
    // Create the device context based on the requested backend
    // NOTE: Backend enum order must match C# GifPlayer.Backend: DUMMY=0, D3D11=1, Metal=2, D3D9Ex=3
    switch (backend)
    {
        case Renderer::Backend::DUMMY:
            this->_pImpl->_deviceContext = std::make_shared<Renderer::DummyDeviceCommandContext>();
            break;

#ifdef _WIN32
        case Renderer::Backend::D3D11:
            this->_pImpl->_deviceContext = std::make_shared<Renderer::D3D11DeviceCommandContext>();
            break;

        case Renderer::Backend::D3D9Ex:
        {
            this->_pImpl->_deviceContext = std::make_shared<Renderer::D3D9ExDeviceCommandContext>();
            break;
        }
#endif

#ifdef __APPLE__
        case Renderer::Backend::Metal:
            this->_pImpl->_deviceContext = std::make_shared<Renderer::MetalDeviceCommandContext>();
            break;
#endif

        default:
            throw std::runtime_error("Unsupported or unavailable backend for this platform");
    }
}

GifDecoder::~GifDecoder() = default;

bool GifDecoder::LoadFromFile(const std::string& filePath)
{
    return _pImpl->LoadGif(filePath);
}

bool GifDecoder::LoadFromMemory(const uint8_t* data, size_t length)
{
    return _pImpl->LoadGifFromMemory(data, length);
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
    // Lazy loading with LRU cache - decode only when needed
    return _pImpl->GetOrDecodeFrame(index);
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

void GifBolt::GifDecoder::SetMaxCachedFrames(uint32_t maxFrames)
{
    if (maxFrames > 0)
    {
        _pImpl->MAX_CACHED_FRAMES = maxFrames;
    }
}

uint32_t GifBolt::GifDecoder::GetMaxCachedFrames() const
{
    return _pImpl->MAX_CACHED_FRAMES;
}

const uint8_t* GifDecoder::GetFramePixelsBGRA32Premultiplied(uint32_t index)
{
    if (index >= _pImpl->_frameCount)
    {
        return nullptr;
    }

    // Get frame from LRU cache (lazy loading)
    const GifFrame& frame = _pImpl->GetOrDecodeFrame(index);

    // Check if frame has pixel data
    if (frame.pixels.empty())
    {
        return nullptr;
    }

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

    // Get frame from LRU cache (lazy loading)
    const GifFrame& frame = _pImpl->GetOrDecodeFrame(index);
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

void GifDecoder::ResetCanvas()
{
    if (this->_pImpl)
    {
        std::lock_guard<std::mutex> lock(this->_pImpl->_decodeMutex);

        // Clear canvas to transparent (0x00000000)
        // Note: GIF background color is NOT used here because modern renderers
        // compose GIFs over their own backgrounds. Using transparent allows proper compositing.
        std::fill(this->_pImpl->_canvas.begin(), this->_pImpl->_canvas.end(), 0x00000000);

        // Reset disposal state
        this->_pImpl->_previousDisposal = DisposalMethod::None;
        this->_pImpl->_previousCanvas.clear();
        this->_pImpl->_prevFrameWidth = 0;
        this->_pImpl->_prevFrameHeight = 0;
        this->_pImpl->_prevFrameOffsetX = 0;
        this->_pImpl->_prevFrameOffsetY = 0;

        // Clear ALL caches to force complete re-composition from clean canvas
        this->_pImpl->_frameCache.clear();
        this->_pImpl->_cachedFrameIndices.clear();
        this->_pImpl->_bgraPremultipliedCache.clear();
        std::fill(this->_pImpl->_frameDecoded.begin(), this->_pImpl->_frameDecoded.end(), false);
    }
}

Renderer::Backend GifDecoder::GetBackend() const
{
    if (this->_pImpl && this->_pImpl->_deviceContext)
    {
        return this->_pImpl->_deviceContext->GetBackend();
    }
    return Renderer::Backend::DUMMY;
}

void* GifDecoder::GetNativeTexturePtr(int frameIndex)
{
    if (!this->_pImpl || frameIndex < 0 || static_cast<uint32_t>(frameIndex) >= this->_pImpl->_frameCount)
    {
        return nullptr;
    }

    // Check if we already have this texture cached
    auto it = this->_pImpl->_textureCache.find(frameIndex);
    if (it != this->_pImpl->_textureCache.end() && it->second)
    {
        void* ptr = it->second->GetNativeTexturePtr();
        return ptr;
    }

    // Get the frame pixels to ensure it's cached
    const uint8_t* pixels = this->GetFramePixelsBGRA32Premultiplied(static_cast<uint32_t>(frameIndex));
    if (!pixels)
    {
        return nullptr;
    }

    // Create or retrieve a texture for this frame
    if (!this->_pImpl->_deviceContext)
    {
        return nullptr;
    }

    auto texture = this->_pImpl->_deviceContext->CreateTexture(
        this->_pImpl->_width,
        this->_pImpl->_height,
        pixels,
        this->_pImpl->_width * this->_pImpl->_height * 4);

    if (texture)
    {
        // Cache the texture so it stays alive
        this->_pImpl->_textureCache[frameIndex] = texture;
        void* ptr = texture->GetNativeTexturePtr();
        return ptr;
    }

#ifdef _WIN32
    OutputDebugStringA("[GifDecoder] GetNativeTexturePtr: CRITICAL - CreateTexture returned null\n");
#endif
    return nullptr;
}

}  // namespace GifBolt
