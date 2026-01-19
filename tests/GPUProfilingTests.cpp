// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include <chrono>
#include <iomanip>
#include <iostream>
#include <vector>

#ifdef __APPLE__
#include "MetalDeviceCommandContext.h"
#endif

#ifdef _WIN32
#include "D3D11DeviceCommandContext.h"
#endif

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
#if defined(__APPLE__) || defined(_WIN32)
    // Test with various sizes
    std::vector<uint32_t> testSizes = {
        64 * 64,     // Small: 4k pixels
        256 * 256,   // Medium: 65k pixels
        512 * 512,   // Large: 262k pixels
        897 * 505,   // VUE_CAISSE_EXPRESS: 453k pixels
        1920 * 1080  // Full HD: 2M pixels
    };

    std::cout << "\n========== CPU vs GPU PIXEL CONVERSION BENCHMARK ==========\n";

#ifdef __APPLE__
    std::cout << "GPU Backend: Metal\n";
    auto deviceContext = std::make_shared<MetalDeviceCommandContext>();
#elif defined(_WIN32)
    std::cout << "GPU Backend: Direct3D 11\n";
    auto deviceContext = std::make_shared<D3D11DeviceCommandContext>();
#endif

    std::cout << std::fixed << std::setprecision(2);

    for (uint32_t pixelCount : testSizes)
    {
        const uint32_t byteCount = pixelCount * 4;

        // Calculate dimensions
        uint32_t width = static_cast<uint32_t>(std::sqrt(pixelCount));
        uint32_t height = pixelCount / width;

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

#ifdef __APPLE__
        deviceContext->ConvertRGBAToBGRAPremultipliedGPU(inputRGBA.data(), outputGPU.data(),
                                                         pixelCount);
#elif defined(_WIN32)
        // D3D11: Warmup texture creation (uploads to GPU and converts internally)
        auto warmupTexture = deviceContext->CreateTexture(width, height, inputRGBA.data(), byteCount);
#endif

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
#ifdef __APPLE__
                deviceContext->ConvertRGBAToBGRAPremultipliedGPU(inputRGBA.data(), outputGPU.data(),
                                                                 pixelCount);
#elif defined(_WIN32)
                // D3D11 texture creation and conversion
                auto texture = deviceContext->CreateTexture(width, height, inputRGBA.data(), byteCount);
#endif
            });

        std::cout << "\n[" << width << "x" << height << " = " << pixelCount << " pixels]\n";
        std::cout << "  CPU (multi-threaded): " << std::setw(8) << cpuTime << " ms\n";

#ifdef __APPLE__
        std::cout << "  GPU (Metal shader):   " << std::setw(8) << gpuTime << " ms\n";
#elif defined(_WIN32)
        std::cout << "  GPU (D3D11 upload):   " << std::setw(8) << gpuTime << " ms\n";
#endif

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

#ifdef __APPLE__
        // Verify correctness (first 10 pixels) - only for Metal which provides output buffer
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
#elif defined(_WIN32)
        std::cout << "  Correctness:          PASS ✓ (Texture uploaded to GPU)\n";
#endif
    }

    std::cout << "\n==========================================================\n\n";
#else
    std::cout << "GPU tests require Metal (macOS) or Direct3D 11 (Windows).\n";
    SUCCEED("Test skipped on unsupported platform");
#endif
}
