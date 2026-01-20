# GifBolt.Core

Cross-platform GIF playback library with GPU acceleration support for WPF and Avalonia.

## Features

- **GPU Accelerated Rendering**: Hardware-accelerated GIF decoding and rendering via DirectX 11 (Windows), Metal (macOS), and Vulkan (Linux)
- **Cross-Platform Support**: Works seamlessly on Windows, macOS, and Linux
- **High Performance**: Optimized for smooth playback of high-resolution GIFs
- **Platform Abstraction**: Unified API for all supported platforms

## Installation

```bash
dotnet add package GifBolt
```

## Quick Start

```csharp
using GifBolt;

// Initialize the GIF player
var player = new GifPlayer();
player.LoadGif("path/to/animation.gif");
player.Play();
```

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
