# GifBolt

A high-performance WPF library for rendering animated GIFs with DirectX 11 acceleration.

## Features

- 🚀 **DirectX 11 GPU-accelerated rendering** on Windows
- 📦 **Cross-platform decoder** (C++ with giflib)
- 🎯 **.NET Standard 2.0 core** for broad compatibility
- 🖼️ **WPF control** with XAML data binding support
- 🔄 **Play/pause/stop controls** with looping support
- 🏗️ **Clean architecture** with backend abstraction layer (inspired by 0 A.D.)
- ✨ **Drop-in replacement for WpfAnimatedGif** - supports attached properties on standard Image controls

## Project Structure

```
GifBolt/
├── src/
│   ├── GifBolt.Native/          # C++ native library (decoder + D3D11 renderer)
│   ├── GifBolt.Core/            # .NET Standard 2.0 core (P/Invoke layer)
│   └── GifBolt.Wpf/             # WPF control (.NET Framework 4.7.2)
├── tests/
│   ├── GifBolt.Tests/           # Native C++ tests (Catch2)
│   └── GifBolt.Core.Tests/      # .NET P/Invoke integration tests
├── samples/
│   └── GifBolt.SampleApp/       # WPF sample application
├── .github/workflows/            # GitHub Actions CI/CD
├── CMakeLists.txt               # Root CMake configuration
├── .editorconfig                # C# code standards
├── .clang-format                # C++ formatting rules
└── README.md
```

## Quick Start

### Option 1: Attached Properties (Compatible with WpfAnimatedGif)

Use on standard WPF `Image` controls - works as a drop-in replacement:

```xml
<Window xmlns:gif="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">
    <Image gif:ImageBehavior.AnimatedSource="animation.gif" />

    <!-- With repeat behavior -->
    <Image gif:ImageBehavior.AnimatedSource="animation.gif"
           gif:ImageBehavior.RepeatBehavior="3x" />

    <!-- Looping forever -->
    <Image gif:ImageBehavior.AnimatedSource="animation.gif"
           gif:ImageBehavior.RepeatBehavior="Forever"
           gif:ImageBehavior.AutoStart="True" />
</Window>
```

Code-behind:

```csharp
var image = new BitmapImage();
image.BeginInit();
image.UriSource = new Uri(fileName);
image.EndInit();
ImageBehavior.SetAnimatedSource(img, image);
```

### Option 2: Custom Control (Direct API)

Use the `GifBoltControl` for more direct control:

```xml
<Window xmlns:gifbolt="clr-namespace:GifBolt.Wpf;assembly=GifBolt.Wpf">
    <gifbolt:GifBoltControl Source="animation.gif"
                           AutoStart="True"
                           Loop="True"/>
</Window>
```

```csharp
// C# code-behind
gifControl.LoadGif("myfile.gif");
gifControl.Play();
gifControl.Pause();
gifControl.Stop();
```



## Requirements

### macOS

- **Xcode Command Line Tools**: `xcode-select --install`
- **CMake 3.20+**: `brew install cmake`
- **Python 3.13+**: Included with Homebrew
- **Node.js**: `brew install node`
- **Ruby 3+**: `brew install ruby` (for markdown linting)
- **.NET SDK**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com)

### Windows

- **Visual Studio 2019+**: With C++ workload and Windows SDK
- **CMake 3.20+**: Download from [cmake.org](https://cmake.org)
- **Python 3.13+**: Download from [python.org](https://www.python.org)
- **Node.js**: Download from [nodejs.org](https://nodejs.org)
- **.NET SDK**: Download from [dotnet.microsoft.com](https://dotnet.microsoft.com)

## Setup & Building

### macOS Setup

1. **Clone repository**

   ```bash
   git clone <repo-url>
   cd GifBolt
   ```

2. **Install dependencies**

   ```bash
   # Install build tools and runtime dependencies only
   brew install cmake llvm clang-format node ruby

   # Install Python dependencies in virtual environment
   python3 -m venv venv
   source venv/bin/activate
   pip install -U pip pre-commit cmake-format

   # Set up pre-commit hooks
   pre-commit install
   ```

3. **Build** (CMake will automatically download and build giflib and Catch2)

   ```bash
   mkdir build && cd build
   cmake ..
   cmake --build . --config Release
   ```

4. **Run tests**

   ```bash
   ctest -C Release --verbose
   ```

5. **Build .NET libraries**

   ```bash
   dotnet build src/GifBolt.Core/GifBolt.Core.csproj -c Release
   ```

### Windows Setup

1. **Clone repository**

   ```powershell
   git clone <repo-url>
   cd GifBolt
   ```

2. **Install dependencies**

   ```powershell
   # Create Python virtual environment
   python -m venv venv
   .\venv\Scripts\Activate.ps1
   pip install -U pip pre-commit cmake-format

   # Set up pre-commit hooks
   pre-commit install
   ```

3. **Build** (CMake will automatically download and build giflib and Catch2)

   ```powershell
   mkdir build
   cd build
   cmake .. -A x64
   cmake --build . --config Release
   ```

4. **Run tests**

   ```powershell
   ctest -C Release --verbose
   ```

5. **Build .NET libraries and sample app**

   ```powershell
   dotnet build src/GifBolt.Core/GifBolt.Core.csproj -c Release
   dotnet build src/GifBolt.Wpf/GifBolt.Wpf.csproj -c Release
   dotnet build samples/GifBolt.SampleApp/GifBolt.SampleApp.csproj -c Release

   # Run the sample (native DLL must be in PATH or copied to output folder)
   copy build\lib\Release\GifBolt.Native.dll samples\GifBolt.SampleApp\bin\Release\net472\
   dotnet run --project samples/GifBolt.SampleApp/GifBolt.SampleApp.csproj -c Release
   ```

## Architecture

### Components

1. **GifBolt.Native** (C++): Cross-platform GIF decoder using giflib, with DirectX 11 backend for Windows rendering
2. **GifBolt.Core** (.NET Standard 2.0): P/Invoke layer and managed decoder wrapper
3. **GifBolt.Wpf** (.NET Framework 4.7.2): WPF control with dependency properties
4. **DeviceCommandContext**: Backend abstraction layer supporting DUMMY (testing) and D3D11 (production)

### C ABI Layer

The native library exposes a stable C API for P/Invoke:

```c
gb_decoder_t gb_decoder_create(void);
void gb_decoder_destroy(gb_decoder_t decoder);
int gb_decoder_load_from_path(gb_decoder_t decoder, const char* path);
int gb_decoder_get_frame_count(gb_decoder_t decoder);
// ... more functions
```

### Code Standards

- **C#**: Sealed classes by default, explicit `this`, Doxygen XML docs (enforced via `.editorconfig`)
- **C++**: Smart pointers, RAII pattern, Allman braces, zero warnings (`-Wall -Wextra -Wpedantic -Werror`)
- See [.llm](.llm) and [agents.md](agents.md) for detailed guidelines



## Code Quality

- **Clang-Format**: Automatic code formatting (enabled via pre-commit)
- **Clang-Tidy**: Static analysis and modernization checks
- **CMake-Format**: CMake file formatting
- **Codespell**: Spell checking
- **Pre-commit Framework**: Automated checks before each commit

All checks run automatically before committing via `pre-commit`. To manually check:

```bash
source venv/bin/activate  # On Windows: .\venv\Scripts\Activate.ps1
pre-commit run --all-files
```



## Development Status

✅ **Completed:**
- Core architecture and backend abstraction
- GIF decoder with giflib integration
- C ABI for P/Invoke interop
- .NET Standard 2.0 core library
- WPF control with dependency properties
- D3D11 backend (minimal implementation)
- Cross-platform CI (Windows + macOS)

🚧 **In Progress:**
- D3D11 rendering pipeline and WPF interop
- Sample application refinement
- Documentation and examples

## License

MIT License - See [LICENSE](LICENSE) for details
