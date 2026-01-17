project(GifBolt.Native VERSION 1.0.0 LANGUAGES CXX)

# Find required packages
find_package(directx-headers CONFIG REQUIRED)
find_package(directxmath CONFIG REQUIRED)

# Add library
add_library(GifBolt.Native SHARED
    src/GifBoltRenderer.cpp
    src/GifDecoder.cpp
)

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
        directx-headers::directx-headers
        directxmath::directxmath
        d3d11
        dxgi
)

# Set compile options
if(NOT MSVC)
    target_compile_options(GifBolt.Native PRIVATE -fPIC)
endif()

# Enable RTC and other debug features for MSVC
if(MSVC)
    target_compile_options(GifBolt.Native PRIVATE $<$<CONFIG:Debug>:/RTC1>)
endif()
