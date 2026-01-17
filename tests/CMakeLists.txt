project(GifBolt.Tests LANGUAGES CXX)

# Find testing framework
find_package(GTest REQUIRED)

# Create test executable
add_executable(GifBolt.Tests
    GifDecoderTests.cpp
    GifBoltRendererTests.cpp
)

# Link dependencies
target_link_libraries(GifBolt.Tests
    PRIVATE
        GifBolt.Native
        GTest::gtest
        GTest::gtest_main
)

# Add tests to CTest
add_test(NAME GifBoltTests COMMAND GifBolt.Tests)
