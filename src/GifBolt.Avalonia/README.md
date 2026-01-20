# GifBolt.Avalonia

Avalonia control for animating GIFs with GPU acceleration support via GifBolt native library.

## Features

- **GPU Accelerated Rendering**: Hardware-accelerated GIF decoding and rendering
- **Cross-Platform Support**: Windows (Direct3D 11), macOS (Metal), and Linux (Vulkan)
- **High Performance**: Smooth playback of high-resolution GIFs
- **Native Avalonia Integration**: Seamless integration with Avalonia UI framework

## Installation

```bash
dotnet add package GifBolt.Avalonia
```

## Usage

```xaml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:gif="using:GifBolt.Avalonia">
    <gif:GifBoltControl Source="path/to/animation.gif" />
</Window>
```

```csharp
var control = new GifBoltControl { Source = "animation.gif" };
control.Play();
```

## Requirements

- Avalonia 11.0+
- .NET 6.0+

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
