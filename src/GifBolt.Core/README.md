# GifBolt.Core

Cross-platform GIF decoding library (.NET Standard 2.0) with native C++ decoder integration.

## Features

- **GIF Decoding**: Native C++ decoder using giflib via P/Invoke
- **Frame Access**: Get RGBA pixel data and timing information for each frame
- **Memory Efficient**: Background frame prefetching for smooth playback
- **Cross-Platform Support**: Works on Windows, macOS, and Linux
- **Multiple Load Methods**: Load from file path, byte array, or stream

## Installation

```bash
dotnet add package GifBolt
```

## Usage

### Basic Loading and Frame Access

```csharp
using GifBolt;

var player = new GifPlayer();

// Load from file
player.Load("path/to/animation.gif");

// Or load from byte array
byte[] gifData = File.ReadAllBytes("animation.gif");
player.Load(gifData);

// Or load from stream
using var stream = File.OpenRead("animation.gif");
player.Load(stream);

// Access GIF properties
int width = player.Width;
int height = player.Height;
int frameCount = player.FrameCount;
bool loops = player.IsLooping;
```

### Frame Data Access

```csharp
// Get pixel data for a specific frame (RGBA32 format)
byte[] pixels = player.GetFramePixels(0);

// Get frame timing
int delayMs = player.GetFrameDelayMs(0);

// Iterate through frames
for (int i = 0; i < player.FrameCount; i++)
{
    byte[] framePixels = player.GetFramePixels(i);
    int frameDelay = player.GetFrameDelayMs(i);
    // Render frame...
}
```

### Playback Control

```csharp
// Control playback state (for your render loop)
player.Play();      // Sets IsPlaying = true
player.Pause();     // Sets IsPlaying = false
player.Stop();      // Sets IsPlaying = false, CurrentFrame = 0

// Check playback state
if (player.IsPlaying)
{
    // Advance frame based on timing
    player.CurrentFrame = (player.CurrentFrame + 1) % player.FrameCount;
}
```

### Frame Delay Configuration

```csharp
// Set minimum frame delay (useful for very fast GIFs)
player.SetMinFrameDelayMs(20); // Limit to 50 FPS

// Get current minimum delay
int minDelay = player.GetMinFrameDelayMs();
```

### Prefetching Control

```csharp
// Enable/disable background prefetching (default: true)
player.EnablePrefetching = true;

// Prefetching decodes frames ahead of CurrentFrame in background
// for smoother sequential playback
```

## Platform Wrappers

`GifBolt.Core` provides the low-level GIF decoding API. For UI controls:

- **WPF**: Use `GifBolt.Wpf` package with `AnimationBehavior` attached properties
- **Avalonia**: Use `GifBolt.Avalonia` package with `AnimationBehavior` attached properties

## Documentation

For complete documentation, visit [https://github.com/yourusername/GifBolt](https://github.com/yourusername/GifBolt)

## License

MIT License - see LICENSE file for details
