project(GifBolt.Tests LANGUAGES CXX)

# Create test executable
add_executable(GifBolt.Tests
    GifDecoderTests.cpp
    GifBoltRendererTests.cpp
    GifProfilingTests.cpp
    GPUProfilingTests.cpp
    ScalingFilterBenchmarks.cpp
    ThreadPoolBenchmarks.cpp
    PrefetchTests.cpp
)

# Link dependencies - use object library to get access to C++ classes
target_link_libraries(GifBolt.Tests
    PRIVATE
        GifBolt.Native.Objects
        Catch2::Catch2WithMain
        gif
)

# Use dynamic MSVC runtime to match libraries
set_property(TARGET GifBolt.Tests PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")

# Link platform-specific libraries
if(WIN32)
    target_link_libraries(GifBolt.Tests PRIVATE d3d11 dxgi d3dcompiler)
endif()

if(APPLE)
    target_link_libraries(GifBolt.Tests PRIVATE
        "-framework Metal"
        "-framework QuartzCore"
        "-framework Foundation")
endif()

# Add tests to CTest
list(APPEND CMAKE_MODULE_PATH ${catch2_SOURCE_DIR}/extras ${Catch2_SOURCE_DIR}/extras)
include(CTest)
# Fallback: register the test binary directly if Catch2 module is unavailable
add_test(NAME GifBolt.Tests COMMAND $<TARGET_FILE:GifBolt.Tests> -r console)
set_tests_properties(GifBolt.Tests PROPERTIES WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR})

# Copy test assets next to the built test executable for manual runs
add_custom_command(TARGET GifBolt.Tests POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "$<TARGET_FILE_DIR:GifBolt.Tests>/assets"
    COMMAND ${CMAKE_COMMAND} -E copy_directory "${CMAKE_CURRENT_SOURCE_DIR}/assets" "$<TARGET_FILE_DIR:GifBolt.Tests>/assets"
)
