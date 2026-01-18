// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include <chrono>
#include <iomanip>
#include <iostream>
#include <thread>
#include <vector>

#include "GifDecoder.h"
#include "ScalingFilter.h"

using namespace GifBolt;
using namespace std::chrono;

/// \brief Helper to measure execution time in milliseconds
template <typename Func>
double MeasureMs(Func&& func)
{
    auto start = high_resolution_clock::now();
    func();
    auto end = high_resolution_clock::now();
    return duration_cast<microseconds>(end - start).count() / 1000.0;
}

TEST_CASE("Benchmark scaling filters - Quality vs Performance", "[Benchmark][Scaling]")
{
    const char* gifPath = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";

    GifDecoder decoder;
    REQUIRE(decoder.LoadFromFile(gifPath));

    const uint32_t sourceWidth = decoder.GetWidth();
    const uint32_t sourceHeight = decoder.GetHeight();
    const uint32_t frameCount = decoder.GetFrameCount();

    std::cout << "\n========== SCALING FILTER BENCHMARKS ==========\n";
    std::cout << "Source: " << sourceWidth << "x" << sourceHeight << " (" << frameCount
              << " frames)\n\n";

    // Test configurations: different target resolutions
    struct TestConfig
    {
        const char* name;
        uint32_t targetWidth;
        uint32_t targetHeight;
        double scale;
    };

    std::vector<TestConfig> configs = {{"Downscale 2x", sourceWidth / 2, sourceHeight / 2, 0.5},
                                       {"Upscale 1.5x", static_cast<uint32_t>(sourceWidth * 1.5),
                                        static_cast<uint32_t>(sourceHeight * 1.5), 1.5},
                                       {"Upscale 2x", sourceWidth * 2, sourceHeight * 2, 2.0}};

    // Test each filter type
    struct FilterInfo
    {
        ScalingFilter filter;
        const char* name;
        const char* description;
    };

    std::vector<FilterInfo> filters = {
        {ScalingFilter::Nearest, "Nearest", "Point sampling - fastest, lowest quality"},
        {ScalingFilter::Bilinear, "Bilinear", "Linear interpolation - good balance"},
        {ScalingFilter::Bicubic, "Bicubic", "Cubic interpolation - higher quality"},
        {ScalingFilter::Lanczos, "Lanczos-3", "Sinc resampling - highest quality"}};

    for (const auto& config : configs)
    {
        std::cout << "\n--- " << config.name << " (" << config.targetWidth << "x"
                  << config.targetHeight << ", " << config.scale << "x) ---\n";
        std::cout << std::fixed << std::setprecision(2);

        // Baseline: use first filter as reference
        double baselineTime = 0.0;

        for (size_t i = 0; i < filters.size(); ++i)
        {
            const auto& filterInfo = filters[i];
            uint32_t outWidth = 0, outHeight = 0;
            const uint8_t* scaledPixels = nullptr;

            // Warm-up run
            scaledPixels = decoder.GetFramePixelsBGRA32PremultipliedScaled(
                0, config.targetWidth, config.targetHeight, outWidth, outHeight, filterInfo.filter);
            REQUIRE(scaledPixels != nullptr);

            // Benchmark: scale frame 0 multiple times to get reliable average
            const int iterations = 10;
            double totalTime = 0.0;

            for (int iter = 0; iter < iterations; ++iter)
            {
                double time = MeasureMs(
                    [&]()
                    {
                        scaledPixels = decoder.GetFramePixelsBGRA32PremultipliedScaled(
                            0, config.targetWidth, config.targetHeight, outWidth, outHeight,
                            filterInfo.filter);
                    });
                totalTime += time;
            }

            double avgTime = totalTime / iterations;

            if (i == 0)
            {
                baselineTime = avgTime;
            }

            double slowdown = (baselineTime > 0.0) ? (avgTime / baselineTime) : 1.0;

            std::cout << std::left << std::setw(12) << filterInfo.name << ": " << std::right
                      << std::setw(8) << avgTime << " ms/frame" << std::setw(8)
                      << (slowdown - 1.0) * 100.0 << "% slower"
                      << "  |  " << filterInfo.description << "\n";
        }
    }

    std::cout << "\n========== FULL ANIMATION BENCHMARK ==========\n";
    std::cout << "Scaling all " << frameCount << " frames with each filter...\n\n";

    // Test on target resolution: 1920x1080 (typical HD display)
    const uint32_t hdWidth = 1920;
    const uint32_t hdHeight = 1080;

    for (const auto& filterInfo : filters)
    {
        double totalTime = MeasureMs(
            [&]()
            {
                for (uint32_t frame = 0; frame < frameCount; ++frame)
                {
                    uint32_t outWidth = 0, outHeight = 0;
                    const uint8_t* scaledPixels = decoder.GetFramePixelsBGRA32PremultipliedScaled(
                        frame, hdWidth, hdHeight, outWidth, outHeight, filterInfo.filter);
                    REQUIRE(scaledPixels != nullptr);
                }
            });

        double avgPerFrame = totalTime / frameCount;
        double fps = 1000.0 / avgPerFrame;

        std::cout << std::left << std::setw(12) << filterInfo.name << ": " << std::right
                  << std::setw(8) << std::setprecision(2) << totalTime << " ms total, "
                  << std::setw(6) << avgPerFrame << " ms/frame, " << std::setw(5)
                  << std::setprecision(1) << fps << " FPS\n";
    }

    std::cout << "\n========== RECOMMENDATIONS ==========\n";
    std::cout << "- Nearest:  Use for retro/pixel-art GIFs or real-time high FPS\n";
    std::cout << "- Bilinear: Best default for most use cases (good quality/speed)\n";
    std::cout << "- Bicubic:  Use for photo-realistic content at upscaling\n";
    std::cout << "- Lanczos:  Use for maximum quality when performance allows\n";
    std::cout << "\nGPU optimization automatically activates for images > 256x256\n";
}

TEST_CASE("Benchmark prefetch impact on sequential access", "[Benchmark][Prefetch]")
{
    const char* gifPath = "assets/sample.gif";

    std::cout << "\n========== PREFETCH IMPACT BENCHMARK ==========\n";

    // Test without prefetch
    {
        GifDecoder decoder;
        REQUIRE(decoder.LoadFromFile(gifPath));
        // Prefetch is NOT auto-started in C++ (only in C# bindings)

        const uint32_t frameCount = decoder.GetFrameCount();

        double totalTime = MeasureMs(
            [&]()
            {
                for (uint32_t i = 0; i < std::min(frameCount, 50u); ++i)
                {
                    const uint8_t* pixels = decoder.GetFramePixelsBGRA32Premultiplied(i);
                    REQUIRE(pixels != nullptr);
                }
            });

        double avgPerFrame = totalTime / std::min(frameCount, 50u);
        std::cout << "Without prefetch: " << std::setw(8) << std::setprecision(2) << totalTime
                  << " ms total, " << std::setw(6) << avgPerFrame << " ms/frame\n";
    }

    // Test with prefetch
    {
        GifDecoder decoder;
        REQUIRE(decoder.LoadFromFile(gifPath));

        const uint32_t frameCount = decoder.GetFrameCount();

        // Start prefetch explicitly
        decoder.StartPrefetching(0);

        // Give prefetch thread time to decode ahead
        std::this_thread::sleep_for(std::chrono::milliseconds(200));

        double totalTime = MeasureMs(
            [&]()
            {
                for (uint32_t i = 0; i < std::min(frameCount, 50u); ++i)
                {
                    decoder.SetCurrentFrame(i);  // Update prefetch position
                    const uint8_t* pixels = decoder.GetFramePixelsBGRA32Premultiplied(i);
                    REQUIRE(pixels != nullptr);
                }
            });

        double avgPerFrame = totalTime / std::min(frameCount, 50u);
        std::cout << "With prefetch:    " << std::setw(8) << std::setprecision(2) << totalTime
                  << " ms total, " << std::setw(6) << avgPerFrame << " ms/frame\n";

        // Clean shutdown
        decoder.StopPrefetching();
    }

    std::cout << "\nNote: Prefetch benefits increase with sequential playback patterns\n";
}
