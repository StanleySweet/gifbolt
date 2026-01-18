# Test Assets

This folder contains GIF test files for validating the decoder.

## Included Files

- **sample.gif** (335 KB) - Primary test GIF used by all automated tests

## Large Test Files (Not Committed)

For performance testing, place large GIF files in the project root:

- `artillery_tower6.gif` (not committed, ~54 MB)
- `VUE_CAISSE_EXPRESS 897x504_01.gif` (not committed, ~8.9 MB)

These files are automatically ignored by Git (see `.gitignore`).

## Creating a Test GIF

You can create a simple GIF with ImageMagick:

```bash
# macOS
brew install imagemagick

# Create a simple animated GIF (2 frames)
convert -size 100x100 xc:red frame1.png
convert -size 100x100 xc:blue frame2.png
convert -delay 50 -loop 0 frame1.png frame2.png test.gif
rm frame1.png frame2.png
```

Or download any animated GIF from the Internet.

## Usage in C++ Tests

```cpp
GifDecoder decoder;
REQUIRE(decoder.LoadFromFile("assets/sample.gif"));
```

## Usage in C# Tests

```csharp
var player = new GifPlayer();
bool loaded = player.Load("tests/assets/sample.gif");
Console.WriteLine($"Frames: {player.FrameCount}, Size: {player.Width}x{player.Height}");
```
