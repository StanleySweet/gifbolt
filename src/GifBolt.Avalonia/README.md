# GifBolt.Avalonia

Avalonia attached properties for animating GIFs with GPU acceleration support via GifBolt native library.

## Features

- **Attached Properties**: Use on standard Avalonia `Image` controls
- **GPU Accelerated**: Native C++ decoder with platform-specific rendering
- **Cross-Platform Support**: Windows (Direct3D 11), macOS (Metal), and Linux
- **High Performance**: Smooth playback of high-resolution GIFs
- **Repeat Control**: Configure looping behavior ("Forever", "3x", etc.)
- **API Compatibility**: Matches WPF version for easy cross-platform development

## Installation

```bash
dotnet add package GifBolt.Avalonia
```

## Usage

### XAML Attached Properties

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:gif="using:GifBolt.Avalonia">

    <!-- Basic usage -->
    <Image gif:AnimationBehavior.SourceUri="avares://MyApp/Assets/animation.gif" />

    <!-- With repeat behavior -->
    <Image gif:AnimationBehavior.SourceUri="animation.gif"
           gif:AnimationBehavior.RepeatBehavior="3x" />

    <!-- Loop forever -->
    <Image gif:AnimationBehavior.SourceUri="animation.gif"
           gif:AnimationBehavior.RepeatBehavior="Forever" />

    <!-- Relative path (from application directory) -->
    <Image gif:AnimationBehavior.SourceUri="Assets/animation.gif" />
</Window>
```

### Code-Behind

```csharp
using Avalonia.Controls;
using GifBolt.Avalonia;

// Set animated source
Image myImage = new Image();
AnimationBehavior.SetSourceUri(myImage, "path/to/animation.gif");

// Configure repeat behavior
AnimationBehavior.SetRepeatBehavior(myImage, "Forever");

// Get current source
string source = AnimationBehavior.GetSourceUri(myImage);
```

### Repeat Behavior Values

- `"Forever"` - Loop indefinitely
- `"0x"` - Use GIF metadata (default)
- `"3x"` - Repeat 3 times
- `"1x"` - Play once

## Requirements

- Avalonia 11.0+
- .NET 6.0+
- Requires `GifBolt.Native.dll` (Windows), `libGifBolt.Native.dylib` (macOS), or `libGifBolt.Native.so` (Linux) in application directory

## Platform Support

- **Windows**: Direct3D 11 GPU acceleration
- **macOS**: Metal GPU acceleration
- **Linux**: Software rendering (OpenGL backend planned)

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
