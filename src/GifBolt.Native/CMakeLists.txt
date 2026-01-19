project(GifBolt.Native VERSION 1.0.0 LANGUAGES CXX)

# Add library
add_library(
    GifBolt.Native SHARED
    src/GifBoltRenderer.cpp
    src/GifDecoder.cpp
    src/DummyDeviceCommandContext.cpp
    src/gifbolt_c.cpp
    src/gifbolt_version.cpp)

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

    # Link Direct3D libraries with delay-loading to avoid module-not-found errors
    # when Direct3D is not available at DLL load time. Libraries will be loaded
    # on-demand when rendering is actually performed.
    target_link_libraries(GifBolt.Native PRIVATE d3d11 dxgi d3dcompiler)

    # Add delay-load linker flags for Direct3D libraries
    if(MSVC)
        target_link_options(GifBolt.Native PRIVATE
            /DELAYLOAD:d3d11.dll
            /DELAYLOAD:dxgi.dll
            /DELAYLOAD:d3dcompiler_47.dll
            delayimp.lib)
    endif()

    target_compile_definitions(GifBolt.Native PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN)
endif()

# macOS/iOS Metal backend
if(APPLE)
    enable_language(OBJCXX)
    target_sources(GifBolt.Native PRIVATE
        src/MetalDeviceCommandContext.mm
        include/MetalDeviceCommandContext.h)
    target_link_libraries(GifBolt.Native PRIVATE
        "-framework Metal"
        "-framework QuartzCore"
        "-framework Foundation")
    set_source_files_properties(src/MetalDeviceCommandContext.mm PROPERTIES
        COMPILE_FLAGS "-x objective-c++")

    # Note: Metal shaders are compiled at runtime from embedded source
    # No need to pre-compile .metallib files
endif()

# Set compile options
if(NOT MSVC)
    target_compile_options(GifBolt.Native PRIVATE -fPIC)
endif()

# C++ Runtime linking: use static linking to eliminate runtime dependencies
if(MSVC)
    # Replace dynamic CRT (/MD, /MDd) with static (/MT, /MTd)
    set_property(TARGET GifBolt.Native PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

    # Optimization flags
    target_compile_options(GifBolt.Native PRIVATE $<$<CONFIG:Release>:/O2> $<$<CONFIG:Debug>:/RTC1>)
else()
    # GCC/Clang: -O3 for maximum optimization
    target_compile_options(GifBolt.Native PRIVATE -O3)
endif()

# Export macro for Windows builds
target_compile_definitions(GifBolt.Native PRIVATE GIFBOLT_NATIVE_EXPORTS)
