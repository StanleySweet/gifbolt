// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include <iostream>

#include "DummyDeviceCommandContext.h"
#include "GifBoltRenderer.h"
#include "PixelFormat.h"

#ifdef _WIN32
#include "D3D11DeviceCommandContext.h"
#endif

using namespace GifBolt;

TEST_CASE("GifBoltRenderer can be created", "[GifBoltRenderer]")
{
    GifBoltRenderer renderer;
    REQUIRE(renderer.GetFrameCount() == 0);
}

TEST_CASE("GifBoltRenderer can use DummyDeviceCommandContext", "[GifBoltRenderer]")
{
    auto context = std::make_shared<Renderer::DummyDeviceCommandContext>();
    GifBoltRenderer renderer(context);

    REQUIRE(renderer.Initialize(800, 600));
    REQUIRE(renderer.GetWidth() == 0);  // No GIF loaded yet
    REQUIRE(renderer.GetHeight() == 0);
}

TEST_CASE("GifBoltRenderer can swap device contexts", "[GifBoltRenderer]")
{
    GifBoltRenderer renderer;
    auto context = std::make_shared<Renderer::DummyDeviceCommandContext>();

    renderer.SetDeviceContext(context);
    REQUIRE(renderer.Initialize(1024, 768));
}

#ifdef _WIN32
TEST_CASE("GifBoltRenderer can use D3D11DeviceCommandContext", "[GifBoltRenderer][D3D11][GPU]")
{
    auto context = std::make_shared<Renderer::D3D11DeviceCommandContext>();

    GifBoltRenderer renderer(context);
    REQUIRE(renderer.Initialize(800, 600));

    std::cout << "\n========== D3D11 RENDERER TEST ==========\n";
    std::cout << "D3D11 device context created successfully\n";
    std::cout << "Renderer dimensions: 800x600\n";
    std::cout << "=========================================\n";
}

TEST_CASE("D3D11DeviceCommandContext can create textures", "[D3D11][GPU]")
{
    auto context = std::make_shared<Renderer::D3D11DeviceCommandContext>();

    // Create a test texture with some pixel data
    std::vector<uint8_t> pixels(256 * 256 * 4, 128);  // Gray texture
    auto texture = context->CreateTexture(256, 256, pixels.data(), pixels.size());
    REQUIRE(texture != nullptr);

    std::cout << "\n========== D3D11 TEXTURE TEST ==========\n";
    std::cout << "Created 256x256 RGBA texture successfully\n";    std::cout << "========================================\n";
}
#endif
