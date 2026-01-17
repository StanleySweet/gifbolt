project(GifBolt.Tests LANGUAGES CXX)

# Create test executable
add_executable(GifBolt.Tests
    GifDecoderTests.cpp
    GifBoltRendererTests.cpp
)

# Link dependencies
target_link_libraries(GifBolt.Tests
    PRIVATE
        GifBolt.Native
        Catch2::Catch2WithMain
)

# Add tests to CTest
list(APPEND CMAKE_MODULE_PATH ${catch2_SOURCE_DIR}/extras)
include(CTest)
include(Catch)
catch_discover_tests(GifBolt.Tests)
