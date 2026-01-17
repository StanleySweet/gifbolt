# GifBolt

A high-performance WPF library for rendering animated GIFs with DirectX 11/12 acceleration.

## Project Structure

```
GifBolt/
├── src/
│   ├── GifBolt.Native/          # C++ DirectX renderer
│   │   ├── include/
│   │   ├── src/
│   │   └── CMakeLists.txt
│   └── GifBolt.Wpf/             # C# WPF wrapper
├── tests/                        # Unit tests
├── samples/                      # Example applications
├── .github/workflows/            # GitHub Actions CI/CD
├── CMakeLists.txt               # Root CMake configuration
├── .clang-tidy                  # Clang-Tidy configuration
├── .clang-format                # Code formatting rules
└── README.md
```

## Requirements

- **CMake**: 3.20 or higher
- **Visual Studio**: 2019 or higher (for DirectX development)
- **vcpkg**: For managing C++ dependencies
- **.NET Framework** or **.NET 6+**: For WPF components
- **Google Test**: For unit testing

## Building

### 1. Install dependencies
```bash
vcpkg integrate install
vcpkg install directx-headers:x64-windows directxmath:x64-windows gtest:x64-windows
```

### 2. Create build directory and configure
```bash
mkdir build
cd build
cmake .. -DCMAKE_TOOLCHAIN_FILE=[vcpkg_root]/scripts/buildsystems/vcpkg.cmake
```

### 3. Build
```bash
cmake --build . --config Release
```

### 4. Run tests
```bash
ctest -C Release --verbose
```

## Code Quality

- **Clang-Tidy**: Static analysis and modernization checks
- **Clang-Format**: Automatic code formatting
- **GitHub Actions**: Automated build, test, and analysis pipelines

## Development Status

🚧 Early Development - Core architecture setup complete, implementation in progress

## License

[Add license information]
