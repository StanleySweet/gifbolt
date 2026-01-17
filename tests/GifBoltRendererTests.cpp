#include <catch2/catch_test_macros.hpp>
#include "GifBoltRenderer.h"

using namespace GifBolt;

TEST_CASE("GifBoltRenderer can be created", "[GifBoltRenderer]") {
    GifBoltRenderer renderer;
    REQUIRE(renderer.GetFrameCount() == 0);
}
