// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include <catch2/catch_test_macros.hpp>
#include "GifDecoder.h"

using namespace GifBolt;

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

TEST_CASE("GifDecoder can get frame properties", "[GifDecoder]") {
    GifDecoder decoder;
    REQUIRE(decoder.IsLooping() == false);
}
