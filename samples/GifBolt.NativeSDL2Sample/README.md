# GifBolt Native SDL2 Sample

A minimal C program using SDL2 to test GifBolt native rendering without WPF/C# complexity.

## Purpose

This sample isolates the native C code from the WPF layer to diagnose:
- Whether `ITexture::Update()` is being called
- Frame timing and sequence correctness
- Double-buffering surface swapping
- Cross-platform rendering

## Building

### Prerequisites

- CMake 3.16+
- SDL2 development libraries
- A compiler (MSVC, GCC, or Clang)

### Windows (Visual Studio)

```powershell
# Install SDL2 via vcpkg or download prebuilt
vcpkg install sdl2:x64-windows

# Build
cd gifbolt
mkdir build
cd build
cmake .. -DCMAKE_TOOLCHAIN_FILE=<vcpkg_root>/scripts/buildsystems/vcpkg.cmake
cmake --build . --config Release
```

### macOS (Homebrew)

```bash
brew install sdl2
cd gifbolt
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

### Linux

```bash
# Ubuntu/Debian
sudo apt install libsdl2-dev

# Fedora
sudo dnf install SDL2-devel

# Then build
cd gifbolt
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

## Running

```bash
./bin/GifBolt.NativeSDL2Sample <path_to_gif>

# Example:
./bin/GifBolt.NativeSDL2Sample samples/GifBolt.SampleApp/sample.gif
```

## Features

- Simple SDL2 window (800x600)
- Loads GIF file using GifBolt C API
- Renders frames with proper timing
- FPS display and debug logging
- Press ESC to exit

## Debug Output

The sample logs to both console and `%TEMP%/gifbolt_debug.log`, showing:

```
[D3D9ExTexture::Update] Frame 1, DisplayingAlt=0, Data=0x..., Size=1920000
  -> Updating Primary surface, GPU sync...
  [OK] GPU sync complete, swapped to Alt surface
[GetNativeTexturePtr] Call #60, returning Alt surface
```

If `[D3D9ExTexture::Update]` messages appear in the log, frame data is being pushed to the GPU.
If they're missing, the issue is in the frame rendering pipeline before reaching the texture.

## Troubleshooting

**"SDL2 not found"**
- Install SDL2 development headers
- On Windows with vcpkg: `vcpkg install sdl2:x64-windows`

**"GifBolt.Native.dll not found (Windows)"**
- The CMakeLists.txt automatically copies the DLL to the output directory
- Ensure the build completed successfully

**No frames displayed**
- Check debug log for `[D3D9ExTexture::Update]` messages
- Verify GIF file exists and is readable
- Confirm window is rendered (should show black rectangle)

## Future Enhancements

- Implement OpenGL texture rendering (cross-platform GPU rendering)
- Add frame-by-frame stepping with arrow keys
- Show frame metadata (timing, dimensions)
- Multi-frame optimization testing
