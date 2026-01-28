project(GifBolt.Native VERSION 1.0.0 LANGUAGES CXX)

# Create object library first (for tests to link against)
add_library(
    GifBolt.Native.Objects OBJECT
    src/GifBoltRenderer.cpp
    src/GifDecoder.cpp
    src/DummyDeviceCommandContext.cpp
    src/PixelBuffer.cpp
    src/gifbolt_c.cpp
    src/gifbolt_version.cpp)

# Add shared library using the object library
add_library(
    GifBolt.Native SHARED
    $<TARGET_OBJECTS:GifBolt.Native.Objects>)

# Configure object library
target_include_directories(GifBolt.Native.Objects
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_link_libraries(GifBolt.Native.Objects
    PRIVATE
        gif
)

# Include directories for shared library
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

if (WIN32)
    target_sources(GifBolt.Native.Objects PRIVATE
        src/D3D11DeviceCommandContext.cpp
        include/D3D11DeviceCommandContext.h
        src/D3D9ExDeviceCommandContext.cpp
        include/D3D9ExDeviceCommandContext.h
        src/D3D9ExTexture.cpp
        include/D3D9ExTexture.h
    )

    # Link Direct3D libraries (FORCE compiler version)
    target_link_libraries(GifBolt.Native.Objects PRIVATE
        d3d11
        d3d9
        dxgi
        d3dcompiler
        delayimp
    )

    target_link_libraries(GifBolt.Native PRIVATE
        d3d11
        d3d9
        dxgi
        d3dcompiler
    )

    # Note: Removed delay-load directives to fix P/Invoke issues
    # System DLLs (d3d11, dxgi, d3dcompiler) are always available on Windows 10+
    # Delay-loading them causes P/Invoke to fail with error 126

    target_compile_definitions(GifBolt.Native.Objects PRIVATE
        NOMINMAX
        WIN32_LEAN_AND_MEAN
    )

    target_compile_definitions(GifBolt.Native PRIVATE
        NOMINMAX
        WIN32_LEAN_AND_MEAN
    )
endif()

# macOS/iOS Metal backend
if(APPLE)
    enable_language(OBJCXX)
    target_sources(GifBolt.Native.Objects PRIVATE
        src/MetalDeviceCommandContext.mm
        include/MetalDeviceCommandContext.h)
    target_link_libraries(GifBolt.Native.Objects PRIVATE
        "-framework Metal"
        "-framework QuartzCore"
        "-framework Foundation")
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
    target_compile_options(GifBolt.Native.Objects PRIVATE -fPIC)
    target_compile_options(GifBolt.Native PRIVATE -fPIC)
endif()

# C++ Runtime
if(MSVC)
    # Object library
    set_property(TARGET GifBolt.Native.Objects PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
    set_property(TARGET GifBolt.Native.Objects PROPERTY MSVC_RUNTIME_LIBRARY_DEBUG "MultiThreadedDLLDebug")

    # Shared library
    set_property(TARGET GifBolt.Native PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
    set_property(TARGET GifBolt.Native PROPERTY MSVC_RUNTIME_LIBRARY_DEBUG "MultiThreadedDLLDebug")

    target_compile_options(GifBolt.Native.Objects PRIVATE $<$<CONFIG:Release>:/O2> $<$<CONFIG:Debug>:/RTC1>)
    target_compile_options(GifBolt.Native PRIVATE $<$<CONFIG:Release>:/O2> $<$<CONFIG:Debug>:/RTC1>)
else()
    # GCC/Clang: -O3 for maximum optimization
    target_compile_options(GifBolt.Native.Objects PRIVATE -O3)
    target_compile_options(GifBolt.Native PRIVATE -O3)
endif()

# Export macro for Windows builds
target_compile_definitions(GifBolt.Native.Objects PRIVATE GIFBOLT_NATIVE_EXPORTS)
target_compile_definitions(GifBolt.Native PRIVATE GIFBOLT_NATIVE_EXPORTS)
