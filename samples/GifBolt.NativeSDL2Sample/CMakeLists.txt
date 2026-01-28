cmake_minimum_required(VERSION 3.16)
project(GifBolt.NativeSDL2Sample C)

# Find SDL2
find_package(SDL2 REQUIRED)

# Add executable
add_executable(GifBolt.NativeSDL2Sample main.c)

# Link libraries
# Note: SDL2::SDL2main is optional on Windows but can help with WinMain
target_link_libraries(GifBolt.NativeSDL2Sample PRIVATE
    GifBolt.Native
    SDL2::SDL2
)

# Optionally include SDL2main on Windows for console apps
if(WIN32)
    if(TARGET SDL2::SDL2main)
        target_link_libraries(GifBolt.NativeSDL2Sample PRIVATE SDL2::SDL2main)
    endif()
endif()

# Include GifBolt headers
target_include_directories(GifBolt.NativeSDL2Sample PRIVATE
    "${CMAKE_SOURCE_DIR}/src/GifBolt.Native/include"
)

# Set output directory
set_target_properties(GifBolt.NativeSDL2Sample PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

# Platform-specific settings
if(NOT MSVC)
    # GCC/Clang: Set C99 standard and warnings
    target_compile_options(GifBolt.NativeSDL2Sample PRIVATE
        -std=c99
        -Wall
        -Wextra
    )
endif()

# Copy DLL to output directory on Windows
if(WIN32)
    add_custom_command(TARGET GifBolt.NativeSDL2Sample POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:GifBolt.Native>" 
        "$<TARGET_FILE_DIR:GifBolt.NativeSDL2Sample>/GifBolt.Native.dll"
        COMMENT "Copying GifBolt.Native.dll..."
    )
endif()

message(STATUS "GifBolt.NativeSDL2Sample will be built")

