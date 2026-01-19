cmake_minimum_required(VERSION 3.20)
project(GifBolt VERSION 1.0.0 LANGUAGES C CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Enable FetchContent
include(FetchContent)

# Fetch giflib
FetchContent_Declare(
    giflib
    URL https://sourceforge.net/projects/giflib/files/giflib-5.2.2.tar.gz
    URL_HASH SHA256=be7ffbd057cadebe2aa144542fd90c6838c6a083b5e8a9048b8ee3b66b29d5fb
)

# Fetch Catch2
FetchContent_Declare(
    Catch2
    GIT_REPOSITORY https://github.com/catchorg/Catch2.git
    GIT_TAG v3.7.1
)

FetchContent_MakeAvailable(Catch2)

# Build giflib
FetchContent_GetProperties(giflib)
if(NOT giflib_POPULATED)
    FetchContent_Populate(giflib)

    # Build giflib as static library
    add_library(gif STATIC
        ${giflib_SOURCE_DIR}/dgif_lib.c
        ${giflib_SOURCE_DIR}/egif_lib.c
        ${giflib_SOURCE_DIR}/gif_err.c
        ${giflib_SOURCE_DIR}/gif_font.c
        ${giflib_SOURCE_DIR}/gif_hash.c
        ${giflib_SOURCE_DIR}/gifalloc.c
        ${giflib_SOURCE_DIR}/openbsd-reallocarray.c
        ${giflib_SOURCE_DIR}/quantize.c
    )

    target_include_directories(gif PUBLIC ${giflib_SOURCE_DIR})

    # Apply static CRT linking (must match GifBolt.Native)
    if(MSVC)
        set_property(TARGET gif PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    endif()

    # Disable warnings for giflib
    if(MSVC)
        target_compile_options(gif PRIVATE /W0)
    else()
        target_compile_options(gif PRIVATE -w)
    endif()
endif()

# Enable testing
enable_testing()

# Compiler flags for quality tools
if(MSVC)
    add_compile_options(/W4)
else()
    add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# Add subdirectories
add_subdirectory(src/GifBolt.Native)
add_subdirectory(tests)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
