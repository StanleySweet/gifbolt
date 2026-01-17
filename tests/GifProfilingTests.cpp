// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include "GifDecoder.h"
#include <chrono>
#include <iostream>
#include <iomanip>

using namespace GifBolt;
using namespace std::chrono;

/// \brief Helper to measure execution time in milliseconds
template<typename Func>
double MeasureMs(Func&& func)
{
    auto start = high_resolution_clock::now();
    func();
    auto end = high_resolution_clock::now();
    return duration_cast<microseconds>(end - start).count() / 1000.0;
}

TEST_CASE("Profile VUE_CAISSE_EXPRESS 897x504_01.gif loading and conversion", "[Profiling]")
{
    const char* gifPath = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";

    std::cout << "\n========== GIF LOADING PROFILE ==========\n";
    std::cout << std::fixed << std::setprecision(2);

    // 1. Decoder creation
    GifDecoder decoder;

    // 2. LoadFromFile (includes file I/O + GIF parsing + frame decoding)
    double loadTime = MeasureMs([&]() {
        REQUIRE(decoder.LoadFromFile(gifPath));
    });
    std::cout << "[LOAD FILE]          " << loadTime << " ms (file I/O + parse + decode first frame ONLY)\n";

    const uint32_t width = decoder.GetWidth();
    const uint32_t height = decoder.GetHeight();
    const uint32_t frameCount = decoder.GetFrameCount();
    const uint32_t pixelCount = width * height;

    std::cout << "[DIMENSIONS]         " << width << "x" << height << " = " << pixelCount << " pixels\n";
    std::cout << "[FRAME COUNT]        " << frameCount << " frames\n";

    // 3. Get BGRA pixels for first frame (includes RGBA->BGRA conversion + premultiply)
    const uint8_t* bgraPixels = nullptr;
    double getFirstFrameTime = MeasureMs([&]() {
        bgraPixels = decoder.GetFramePixelsBGRA32Premultiplied(0);
    });
    REQUIRE(bgraPixels != nullptr);
    std::cout << "[GET BGRA FRAME 0]   " << getFirstFrameTime << " ms (RGBA->BGRA convert + premultiply)\n";

    // 4. Get BGRA pixels again (should be fully cached)
    double getCachedTime = MeasureMs([&]() {
        bgraPixels = decoder.GetFramePixelsBGRA32Premultiplied(0);
    });
    REQUIRE(bgraPixels != nullptr);
    std::cout << "[GET BGRA CACHED]    " << getCachedTime << " ms (fully cached, memory copy only)\n";

    // 5. Access all frames to check if they're decoded
    double accessAllTime = MeasureMs([&]() {
        for (uint32_t i = 0; i < frameCount; ++i)
        {
            const GifFrame& frame = decoder.GetFrame(i);
            REQUIRE(frame.width > 0);
        }
    });
    std::cout << "[ACCESS ALL FRAMES]  " << accessAllTime << " ms (" << frameCount << " frames, lazy decode triggered)\n";

    // 6. Convert all frames to BGRA
    double convertAllTime = MeasureMs([&]() {
        for (uint32_t i = 1; i < frameCount; ++i)
        {
            decoder.GetFramePixelsBGRA32Premultiplied(i);
        }
    });
    std::cout << "[CONVERT ALL FRAMES] " << convertAllTime << " ms (" << (frameCount - 1) << " frames)\n";

    // Total time analysis
    double totalColdLoad = loadTime + getFirstFrameTime;
    double avgConversionPerFrame = (getFirstFrameTime + convertAllTime) / frameCount;

    std::cout << "\n========== TIMING BREAKDOWN ==========\n";
    std::cout << "File I/O + Parse + Decode: " << std::setw(8) << loadTime << " ms ("
              << std::setw(5) << std::setprecision(1) << (loadTime / totalColdLoad * 100) << "%)\n";
    std::cout << "First BGRA Convert:        " << std::setw(8) << std::setprecision(2) << getFirstFrameTime << " ms ("
              << std::setw(5) << std::setprecision(1) << (getFirstFrameTime / totalColdLoad * 100) << "%)\n";
    std::cout << "Cached BGRA Access:        " << std::setw(8) << std::setprecision(2) << getCachedTime << " ms (cache hit)\n";
    std::cout << "Average Convert/Frame:     " << std::setw(8) << avgConversionPerFrame << " ms\n";

    std::cout << "\n========== TOTAL & TARGET ==========\n";
    std::cout << std::fixed << std::setprecision(2);
    std::cout << "Cold load (file + 1st frame): " << totalColdLoad << " ms\n";
    std::cout << "Full animation ready:         " << (loadTime + getFirstFrameTime + convertAllTime) << " ms\n";
    std::cout << "\nTarget:                       < 500 ms (ideally < 250 ms)\n";

    if (totalColdLoad > 500.0)
    {
        std::cout << "Status:                       ❌ NEEDS OPTIMIZATION (" << (totalColdLoad - 500.0) << " ms over)\n";
        std::cout << "\nBottleneck Analysis:\n";
        if (loadTime > 250.0)
        {
            std::cout << "  - File I/O + Decode is slow (" << loadTime << " ms)\n";
            std::cout << "    → Consider lazy frame decoding (decode on-demand)\n";
            std::cout << "    → Consider caching decoded GIFs\n";
        }
        if (getFirstFrameTime > 100.0)
        {
            std::cout << "  - BGRA conversion is slow (" << getFirstFrameTime << " ms)\n";
            std::cout << "    → Already multi-threaded, check thread count\n";
        }
    }
    else if (totalColdLoad > 250.0)
    {
        std::cout << "Status:                       ⚠️  ACCEPTABLE (" << (totalColdLoad - 250.0) << " ms over ideal)\n";
    }
    else
    {
        std::cout << "Status:                       ✅ EXCELLENT\n";
    }
    std::cout << "=========================================\n\n";
}

TEST_CASE("Profile frame-by-frame BGRA conversion performance", "[Profiling]")
{
    const char* gifPath = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";

    GifDecoder decoder;
    REQUIRE(decoder.LoadFromFile(gifPath));

    const uint32_t frameCount = decoder.GetFrameCount();
    std::cout << "\n========== FRAME-BY-FRAME BGRA CONVERSION PROFILE ==========\n";
    std::cout << std::fixed << std::setprecision(2);

    double totalConversionTime = 0.0;
    double minTime = 1e9;
    double maxTime = 0.0;

    const uint32_t framesToProfile = std::min(frameCount, 20u);

    for (uint32_t i = 0; i < framesToProfile; ++i)
    {
        double frameTime = MeasureMs([&]() {
            decoder.GetFramePixelsBGRA32Premultiplied(i);
        });

        totalConversionTime += frameTime;
        minTime = std::min(minTime, frameTime);
        maxTime = std::max(maxTime, frameTime);

        std::cout << "Frame " << std::setw(3) << i << ": " << std::setw(8) << frameTime << " ms";
        if (i == 0)
        {
            std::cout << " (cold, first conversion)";
        }
        else
        {
            std::cout << " (cached)";
        }
        std::cout << "\n";
    }

    std::cout << "\n[CONVERSION STATISTICS]\n";
    std::cout << "Min:     " << minTime << " ms\n";
    std::cout << "Max:     " << maxTime << " ms\n";
    std::cout << "Average: " << (totalConversionTime / framesToProfile) << " ms\n";
    std::cout << "First:   " << maxTime << " ms (should be highest)\n";
    std::cout << "Cached:  ~" << minTime << " ms (subsequent accesses)\n";
    std::cout << "===========================================================\n\n";
}

TEST_CASE("Measure LoadFromFile breakdown components", "[Profiling]")
{
    const char* gifPath = "/Users/stan/Dev/GifBolt/VUE_CAISSE_EXPRESS 897x504_01.gif";

    std::cout << "\n========== LOADFROMFILE DETAILED BREAKDOWN ==========\n";
    std::cout << std::fixed << std::setprecision(2);

    // This test attempts to isolate components within LoadFromFile
    // Note: We can't easily separate internal steps without modifying GifDecoder

    GifDecoder decoder1, decoder2, decoder3;

    // Measure first load (cold, includes potential OS caching)
    double firstLoad = MeasureMs([&]() {
        REQUIRE(decoder1.LoadFromFile(gifPath));
    });
    std::cout << "[FIRST LOAD]         " << firstLoad << " ms (cold, no OS cache)\n";

    // Measure second load (OS file cache warm)
    double secondLoad = MeasureMs([&]() {
        REQUIRE(decoder2.LoadFromFile(gifPath));
    });
    std::cout << "[SECOND LOAD]        " << secondLoad << " ms (warm OS file cache)\n";

    // Measure third load
    double thirdLoad = MeasureMs([&]() {
        REQUIRE(decoder3.LoadFromFile(gifPath));
    });
    std::cout << "[THIRD LOAD]         " << thirdLoad << " ms (warm OS file cache)\n";

    double avgWarmLoad = (secondLoad + thirdLoad) / 2.0;
    double estimatedIOTime = firstLoad - avgWarmLoad;

    std::cout << "\n[ANALYSIS]\n";
    std::cout << "Average warm load:   " << avgWarmLoad << " ms (GIF parsing + decoding)\n";
    std::cout << "Estimated file I/O:  " << estimatedIOTime << " ms (disk read time)\n";
    std::cout << "File I/O overhead:   " << std::setprecision(1) << (estimatedIOTime / firstLoad * 100) << "%\n";
    std::cout << "===================================================\n\n";
}
