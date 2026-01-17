#pragma once

#include <memory>
#include <string>
#include <cstdint>

namespace GifBolt {

class GifBoltRenderer {
public:
    GifBoltRenderer();
    ~GifBoltRenderer();

    // Initialize the renderer with a window handle
    bool Initialize(void* hwnd, uint32_t width, uint32_t height);

    // Load a GIF from file or URL
    bool LoadGif(const std::string& path);

    // Playback control
    void Play();
    void Stop();
    void Pause();
    void SetLooping(bool loop);

    // Rendering
    bool Render();

    // Frame control
    void SetCurrentFrame(uint32_t frameIndex);
    uint32_t GetCurrentFrame() const;
    uint32_t GetFrameCount() const;

    // Getters
    uint32_t GetWidth() const;
    uint32_t GetHeight() const;

private:
    class Impl;
    std::unique_ptr<Impl> pImpl;
};

}  // namespace GifBolt
