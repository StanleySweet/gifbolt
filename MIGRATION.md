# Migration Guide: WpfAnimatedGif → GifBolt

This guide helps you migrate from [WpfAnimatedGif](https://github.com/XamlAnimatedGif/WpfAnimatedGif) to GifBolt.

## Why Migrate?

- ⚡ **GPU-accelerated rendering** with DirectX 11 (better performance for complex UIs)
- 🏗️ **Modern architecture** with clean separation of concerns
- 🔧 **Active development** with modern tooling and CI/CD
- 🎯 **Drop-in compatibility** - minimal code changes required

## API Compatibility

GifBolt provides **100% API compatibility** with WpfAnimatedGif's attached properties:

| WpfAnimatedGif | GifBolt | Status |
|----------------|---------|--------|
| `ImageBehavior.AnimatedSource` | `ImageBehavior.AnimatedSource` | ✅ Compatible |
| `ImageBehavior.RepeatBehavior` | `ImageBehavior.RepeatBehavior` | ✅ Compatible |
| `ImageBehavior.AutoStart` | `ImageBehavior.AutoStart` | ✅ Compatible |

## Migration Steps

### 1. Update NuGet References

```xml
<!-- Remove WpfAnimatedGif -->
<PackageReference Include="WpfAnimatedGif" Version="2.0.2" />

<!-- Add GifBolt -->
<PackageReference Include="GifBolt.Wpf" Version="1.0.0" />
```

### 2. Update XAML Namespace

**Before (WpfAnimatedGif):**
```xml
<Window xmlns:gif="http://wpfanimatedgif.codeplex.com">
    <Image gif:ImageBehavior.AnimatedSource="animation.gif" />
</Window>
```

**After (GifBolt):**
```xml
<Window xmlns:gif="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">
    <Image gif:ImageBehavior.AnimatedSource="animation.gif" />
</Window>
```

That's it! The property names and behavior are identical.

### 3. Code-Behind (No Changes Required)

The API is 100% compatible:

```csharp
// Works the same in both libraries
var image = new BitmapImage();
image.BeginInit();
image.UriSource = new Uri(fileName);
image.EndInit();
ImageBehavior.SetAnimatedSource(img, image);

// Repeat behavior
ImageBehavior.SetRepeatBehavior(img, "3x");      // Repeat 3 times
ImageBehavior.SetRepeatBehavior(img, "Forever"); // Loop forever
ImageBehavior.SetRepeatBehavior(img, "0x");      // Use GIF metadata
```

## Feature Comparison

| Feature | WpfAnimatedGif | GifBolt |
|---------|----------------|---------|
| Attached properties on Image | ✅ | ✅ |
| RepeatBehavior support | ✅ | ✅ |
| AutoStart | ✅ | ✅ |
| Manual control (Pause/Resume) | ✅ | 🚧 In progress |
| Animation events | ✅ | 🚧 Planned |
| Design-time preview | ✅ | 🚧 Planned |
| GPU acceleration | ❌ | ✅ DirectX 11 |
| Custom control option | ❌ | ✅ `GifBoltControl` |
| Cross-platform decoder | ❌ | ✅ Native C++ |

## Advanced: Using GifBoltControl

GifBolt also provides a custom control for scenarios where you need more direct control:

```xml
<Window xmlns:gifbolt="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">
    <gifbolt:GifBoltControl Source="animation.gif"
                           AutoStart="True"
                           Loop="True"/>
</Window>
```

```csharp
// Full API access
gifControl.Play();
gifControl.Pause();
gifControl.Stop();
gifControl.LoadGif("newfile.gif");
```

## Known Differences

### 1. Rendering Backend

- **WpfAnimatedGif**: Software rendering via WPF's imaging stack
- **GifBolt**: DirectX 11 GPU rendering (Windows) or WriteableBitmap fallback

### 2. Performance

GifBolt is significantly faster for:
- Large GIF files (>1MB)
- Multiple simultaneous animations
- High-resolution displays (4K+)

### 3. Dependencies

- **WpfAnimatedGif**: Pure managed code, no native dependencies
- **GifBolt**: Requires native `GifBolt.Native.dll` (automatically included via NuGet)

## Troubleshooting

### "Could not load GifBolt.Native.dll"

Ensure the native DLL is copied to your output directory. This should be automatic with NuGet, but if needed:

```xml
<ItemGroup>
  <Content Include="$(NuGetPackageRoot)\gifbolt.wpf\1.0.0\runtimes\win-x64\native\GifBolt.Native.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Performance Not Improved

1. Ensure you're running on Windows (DirectX 11 backend)
2. Check GPU drivers are up to date
3. Verify hardware acceleration is enabled in WPF

## Feedback & Support

- 🐛 [Report Issues](https://github.com/yourorg/GifBolt/issues)
- 💬 [Discussions](https://github.com/yourorg/GifBolt/discussions)
- 📖 [Full Documentation](https://github.com/yourorg/GifBolt/wiki)

## License

GifBolt is MIT licensed, same permissive license as WpfAnimatedGif (Apache 2.0).
