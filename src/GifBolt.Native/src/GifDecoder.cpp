#include "GifDecoder.h"
#include <gif_lib.h>
#include <fstream>
#include <stdexcept>
#include <cstring>

namespace GifBolt {

class GifDecoder::Impl {
public:
    std::vector<GifFrame> frames;
    uint32_t width = 0;
    uint32_t height = 0;
    bool looping = false;

    bool LoadGif(const std::string& filePath);
    void DecodeFrame(GifFileType* gif, int frameIndex);
    void ApplyColorMap(const GifByteType* raster, const ColorMapObject* colorMap,
                       std::vector<uint32_t>& pixels, int width, int height);
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
    frame.delayMs = 100; // Default delay

    // Get delay from graphics control extension
    for (int i = 0; i < image->ExtensionBlockCount; ++i) {
        ExtensionBlock* ext = &image->ExtensionBlocks[i];
        if (ext->Function == GRAPHICS_EXT_FUNC_CODE && ext->ByteCount >= 4) {
            int delay = (ext->Bytes[2] << 8) | ext->Bytes[1];
            frame.delayMs = delay * 10; // Convert to milliseconds
            break;
        }
    }

    // Decode pixel data
    ColorMapObject* colorMap = desc->ColorMap ? desc->ColorMap : gif->SColorMap;
    frame.pixels.resize(desc->Width * desc->Height);

    ApplyColorMap(image->RasterBits, colorMap, frame.pixels,
                  desc->Width, desc->Height);

    frames.push_back(std::move(frame));
}

void GifDecoder::Impl::ApplyColorMap(const GifByteType* raster,
                                      const ColorMapObject* colorMap,
                                      std::vector<uint32_t>& pixels,
                                      int width, int height) {
    for (int i = 0; i < width * height; ++i) {
        int colorIndex = raster[i];
        if (colorMap && colorIndex < colorMap->ColorCount) {
            GifColorType color = colorMap->Colors[colorIndex];
            // Convert to RGBA
            pixels[i] = (0xFF << 24) | (color.Blue << 16) |
                        (color.Green << 8) | color.Red;
        } else {
            pixels[i] = 0xFF000000; // Black
        }
    }
}

GifDecoder::GifDecoder()
    : pImpl(std::make_unique<Impl>()) {
}

GifDecoder::~GifDecoder() = default;

bool GifDecoder::LoadFromFile(const std::string& filePath) {
    return pImpl->LoadGif(filePath);
}

bool GifDecoder::LoadFromUrl(const std::string& url) {
    // TODO: Implement URL loading with HTTP download
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

bool GifDecoder::IsLooping() const {
    return pImpl->looping;
}

}  // namespace GifBolt
