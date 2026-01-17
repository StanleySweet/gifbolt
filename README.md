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

🚧 Early Development - Core architecture setup complete, implementation in progress

## License

[Add license information]
