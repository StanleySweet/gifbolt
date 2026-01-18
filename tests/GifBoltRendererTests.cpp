// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>

#include "DummyDeviceCommandContext.h"
#include "GifBoltRenderer.h"

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
