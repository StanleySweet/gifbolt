# GifBolt.Wpf

WPF attached properties for animating GIFs with GPU acceleration support via GifBolt native library.

## Features

- **Attached Properties**: Use on standard WPF `Image` controls
- **GPU Accelerated**: Native C++ decoder with Direct3D 11 rendering
- **High Performance**: Smooth playback of high-resolution GIFs
- **Repeat Control**: Configure looping behavior ("Forever", "3x", etc.)
- **Design Mode Support**: Optional animation in Visual Studio designer

## Installation

```bash
dotnet add package GifBolt.Wpf
```

## Usage

### XAML Attached Properties

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:gif="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">

    <!-- Basic usage -->
    <Image gif:AnimationBehavior.SourceUri="pack://application:,,,/Assets/animation.gif" />

    <!-- With repeat behavior -->
    <Image gif:AnimationBehavior.SourceUri="animation.gif"
           gif:AnimationBehavior.RepeatBehavior="3x" />

    <!-- Loop forever -->
    <Image gif:AnimationBehavior.SourceUri="animation.gif"
           gif:AnimationBehavior.RepeatBehavior="Forever" />

    <!-- Enable in design mode -->
    <Image gif:AnimationBehavior.SourceUri="animation.gif"
           gif:AnimationBehavior.AnimateInDesignMode="True" />
</Window>
```

### Code-Behind

```csharp
using GifBolt.Wpf;
using System.Windows.Controls;

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

- .NET Framework 4.7.2+
- Windows with Direct3D 11 support
- Requires `GifBolt.Native.dll` in application directory

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
