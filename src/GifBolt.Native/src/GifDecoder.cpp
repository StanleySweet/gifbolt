#include "GifDecoder.h"

namespace GifBolt {

class GifDecoder::Impl {
public:
    // Implementation details here
};

GifDecoder::GifDecoder()
    : pImpl(std::make_unique<Impl>()) {
}

GifDecoder::~GifDecoder() = default;

bool GifDecoder::LoadFromFile(const std::string& filePath) {
    // TODO: Implement file loading with GIF decoding
    return true;
}

bool GifDecoder::LoadFromUrl(const std::string& url) {
    // TODO: Implement URL loading with GIF decoding
    return true;
}

uint32_t GifDecoder::GetFrameCount() const {
    return 0;
}

const GifFrame& GifDecoder::GetFrame(uint32_t index) const {
    static GifFrame dummy;
    return dummy;
}

uint32_t GifDecoder::GetWidth() const {
    return 0;
}

uint32_t GifDecoder::GetHeight() const {
    return 0;
}

bool GifDecoder::IsLooping() const {
    return true;
}

}  // namespace GifBolt
