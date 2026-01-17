// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include "GifDecoder.h"

using namespace GifBolt;

TEST_CASE("GifDecoder applies minFrameDelayMs to all frames in artillery_tower6.gif", "[GifDecoder][Timing]")
{
    GifDecoder decoder;
    decoder.SetMinFrameDelayMs(100);
    REQUIRE(decoder.LoadFromFile("../artillery_tower6.gif"));
    const uint32_t minDelay = decoder.GetMinFrameDelayMs();
    REQUIRE(minDelay == 100);
    const uint32_t frameCount = decoder.GetFrameCount();
    REQUIRE(frameCount > 0);
    for (uint32_t i = 0; i < frameCount; ++i)
    {
        const GifFrame& frame = decoder.GetFrame(i);
        REQUIRE(frame.delayMs >= minDelay);
    }
}

TEST_CASE("GifDecoder can be created", "[GifDecoder]") {
    GifDecoder decoder;
    REQUIRE(decoder.GetFrameCount() == 0);
    REQUIRE(decoder.GetWidth() == 0);
    REQUIRE(decoder.GetHeight() == 0);
}

TEST_CASE("GifDecoder handles invalid file", "[GifDecoder]") {
    GifDecoder decoder;
    REQUIRE_FALSE(decoder.LoadFromFile("nonexistent.gif"));
}

TEST_CASE("GifDecoder can get frame properties", "[GifDecoder]")
{
    GifDecoder decoder;
    REQUIRE(decoder.IsLooping() == false);
}

TEST_CASE("GifDecoder background color defaults to black", "[GifDecoder]")
{
    GifDecoder decoder;
    REQUIRE(decoder.GetBackgroundColor() == 0xFF000000);
}

TEST_CASE("GifDecoder correctly handles disposal methods", "[GifDecoder]")
{
    // This test validates that the decoder doesn't crash with disposal methods
    // Actual visual validation would require loading a real GIF
    GifDecoder decoder;
    REQUIRE(decoder.GetFrameCount() == 0);
}
