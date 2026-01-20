# GifBolt.Wpf

WPF control for animating GIFs with GPU acceleration support via GifBolt native library.

## Features

- **GPU Accelerated Rendering**: Hardware-accelerated GIF decoding and rendering
- **Direct3D 11 Support**: Native Windows GPU acceleration
- **High Performance**: Smooth playback of high-resolution GIFs
- **Native WPF Integration**: Standard WPF control that works with all WPF features

## Installation

```bash
dotnet add package GifBolt.Wpf
```

## Usage

```xaml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:gif="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">
    <gif:GifBoltControl Source="path/to/animation.gif" />
</Window>
```

```csharp
var control = new GifBoltControl { Source = "animation.gif" };
control.Play();
```

## Requirements

- .NET Framework 4.7.2+
- Windows with Direct3D 11 support

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
