#include <gtest/gtest.h>
#include "GifDecoder.h"

using namespace GifBolt;

class GifDecoderTest : public ::testing::Test {
protected:
    GifDecoder decoder;
};

TEST_F(GifDecoderTest, CanCreateDecoder) {
    EXPECT_TRUE(true);  // Placeholder test
}

// TODO: Add more comprehensive tests
