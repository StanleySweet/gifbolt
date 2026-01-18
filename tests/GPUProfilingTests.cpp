// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include <chrono>
#include <iomanip>
#include <iostream>
#include <vector>

#include "MetalDeviceCommandContext.h"
#include "PixelConversion.h"

using namespace GifBolt;
using namespace GifBolt::Renderer;
using namespace GifBolt::Renderer::PixelFormats;
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

TEST_CASE("Compare CPU vs GPU pixel conversion performance", "[GPU][Profiling]")
{
    // Test with various sizes
    std::vector<uint32_t> testSizes = {
        64 * 64,     // Small: 4k pixels
        256 * 256,   // Medium: 65k pixels
        512 * 512,   // Large: 262k pixels
        897 * 505,   // VUE_CAISSE_EXPRESS: 453k pixels
        1920 * 1080  // Full HD: 2M pixels
    };

    std::cout << "\n========== CPU vs GPU PIXEL CONVERSION BENCHMARK ==========\n";
    std::cout << std::fixed << std::setprecision(2);

    // Create Metal device context for GPU acceleration
    auto deviceContext = std::make_shared<MetalDeviceCommandContext>();

    for (uint32_t pixelCount : testSizes)
    {
        const uint32_t byteCount = pixelCount * 4;

        // Create test data
        std::vector<uint8_t> inputRGBA(byteCount);
        std::vector<uint8_t> outputCPU(byteCount);
        std::vector<uint8_t> outputGPU(byteCount);

        // Fill with test pattern (semi-transparent gradients)
        for (uint32_t i = 0; i < pixelCount; ++i)
        {
            inputRGBA[i * 4 + 0] = (i % 256);          // R
            inputRGBA[i * 4 + 1] = ((i / 256) % 256);  // G
            inputRGBA[i * 4 + 2] = ((i / 512) % 256);  // B
            inputRGBA[i * 4 + 3] = 200;                // A (semi-transparent)
        }

        // Warmup runs
        ConvertRGBAToBGRAPremultiplied(inputRGBA.data(), outputCPU.data(), pixelCount, nullptr);
        deviceContext->ConvertRGBAToBGRAPremultipliedGPU(inputRGBA.data(), outputGPU.data(),
                                                         pixelCount);

        // Measure CPU (multi-threaded)
        double cpuTime = MeasureMs(
            [&]()
            {
                ConvertRGBAToBGRAPremultiplied(inputRGBA.data(), outputCPU.data(), pixelCount,
                                               nullptr);
            });

        // Measure GPU
        double gpuTime = MeasureMs(
            [&]()
            {
                deviceContext->ConvertRGBAToBGRAPremultipliedGPU(inputRGBA.data(), outputGPU.data(),
                                                                 pixelCount);
            });

        // Calculate dimensions
        uint32_t width = static_cast<uint32_t>(std::sqrt(pixelCount));
        uint32_t height = pixelCount / width;

        std::cout << "\n[" << width << "x" << height << " = " << pixelCount << " pixels]\n";
        std::cout << "  CPU (multi-threaded): " << std::setw(8) << cpuTime << " ms\n";
        std::cout << "  GPU (compute shader): " << std::setw(8) << gpuTime << " ms\n";

        if (gpuTime > 0.0 && cpuTime > 0.0)
        {
            double speedup = cpuTime / gpuTime;
            std::cout << "  GPU Speedup:          " << std::setw(8) << speedup << "x ";
            if (speedup > 1.0)
            {
                std::cout << "(GPU faster) ✓\n";
            }
            else
            {
                std::cout << "(CPU faster)\n";
            }
        }

        // Verify correctness (first 10 pixels)
        bool correct = true;
        for (uint32_t i = 0; i < std::min(10u, pixelCount); ++i)
        {
            for (uint32_t c = 0; c < 4; ++c)
            {
                uint8_t cpuVal = outputCPU[i * 4 + c];
                uint8_t gpuVal = outputGPU[i * 4 + c];
                // Allow ±1 difference due to rounding
                if (std::abs(static_cast<int>(cpuVal) - static_cast<int>(gpuVal)) > 1)
                {
                    correct = false;
                    break;
                }
            }
            if (!correct)
                break;
        }

        if (correct)
        {
            std::cout << "  Correctness:          PASS ✓\n";
        }
        else
        {
            std::cout << "  Correctness:          FAIL ✗\n";
            REQUIRE(correct);
        }
    }

    std::cout << "\n==========================================================\n\n";
}
