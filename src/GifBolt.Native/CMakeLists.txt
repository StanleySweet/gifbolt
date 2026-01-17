project(GifBolt.Native VERSION 1.0.0 LANGUAGES CXX)

# Add library
add_library(
    GifBolt.Native SHARED
    src/GifBoltRenderer.cpp
    src/GifDecoder.cpp
    src/DummyDeviceCommandContext.cpp
    src/gifbolt_c.cpp)

# Include directories
target_include_directories(GifBolt.Native
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Link libraries
target_link_libraries(GifBolt.Native
    PRIVATE
        gif
)

# Windows-only D3D11 backend
if(WIN32)
    target_sources(GifBolt.Native PRIVATE
        src/D3D11DeviceCommandContext.cpp
        include/D3D11DeviceCommandContext.h)
    target_link_libraries(GifBolt.Native PRIVATE d3d11 dxgi)
    target_compile_definitions(GifBolt.Native PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()

# Set compile options
if(NOT MSVC)
    target_compile_options(GifBolt.Native PRIVATE -fPIC)
endif()

# Enable RTC and other debug features for MSVC
if(MSVC)
    target_compile_options(GifBolt.Native PRIVATE $<$<CONFIG:Debug>:/RTC1>)
endif()

# Export macro for Windows builds
target_compile_definitions(GifBolt.Native PRIVATE GIFBOLT_NATIVE_EXPORTS)
