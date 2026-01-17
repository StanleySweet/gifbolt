#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace GifBolt
{

struct GifFrame
{
    std::vector<uint32_t> pixels;  // RGBA pixel data
    uint32_t width;
    uint32_t height;
    uint32_t delayMs;
};

class GifDecoder
{
   public:
    GifDecoder();
    ~GifDecoder();

    // Load GIF from file
    bool LoadFromFile(const std::string& filePath);

    // Load GIF from URL
    bool LoadFromUrl(const std::string& url);

    // Get frame information
    uint32_t GetFrameCount() const;
    const GifFrame& GetFrame(uint32_t index) const;

    // Properties
    uint32_t GetWidth() const;
    uint32_t GetHeight() const;
    bool IsLooping() const;

   private:
    class Impl;
    std::unique_ptr<Impl> pImpl;
};

}  // namespace GifBolt
