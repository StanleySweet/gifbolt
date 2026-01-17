#include "src/GifBolt.Native/include/GifDecoder.h"
#include <iostream>

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::cerr << "Usage: test_gif_decode <path-to-gif>\n";
        return 1;
    }

    const std::string path = argv[1];
    GifBolt::GifDecoder decoder;
    if (!decoder.LoadFromFile(path))
    {
        std::cerr << "Failed to load GIF: " << path << "\n";
        return 1;
    }

    std::cout << "GIF loaded successfully!\n";
    std::cout << "Dimensions: " << decoder.GetWidth() << "x" << decoder.GetHeight() << "\n";
    std::cout << "Frames: " << decoder.GetFrameCount() << "\n";
    std::cout << "Looping: " << (decoder.IsLooping() ? "Yes" : "No") << "\n";
    std::cout << "Background: 0x" << std::hex << decoder.GetBackgroundColor() << std::dec << "\n";

    const auto& frame = decoder.GetFrame(0);
    std::cout << "Frame 0: " << frame.width << "x" << frame.height
              << " @ (" << frame.offsetX << "," << frame.offsetY << ")\n";
    std::cout << "Delay: " << frame.delayMs << "ms\n";
    std::cout << "Disposal: " << static_cast<int>(frame.disposal) << "\n";

    if (!frame.pixels.empty())
    {
        uint32_t pixel = frame.pixels[0];
        uint8_t r = static_cast<uint8_t>(pixel & 0xFF);
        uint8_t g = static_cast<uint8_t>((pixel >> 8) & 0xFF);
        uint8_t b = static_cast<uint8_t>((pixel >> 16) & 0xFF);
        uint8_t a = static_cast<uint8_t>((pixel >> 24) & 0xFF);
        std::cout << "First pixel (RGBA): "
                  << (int)r << "," << (int)g << "," << (int)b << "," << (int)a << "\n";
    }

    return 0;
}
