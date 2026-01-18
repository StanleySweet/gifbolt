#!/bin/bash
# SPDX-License-Identifier: MIT
# Download native library artifacts from GitHub Actions and prepare for NuGet packaging

set -e

RUN_ID=$1

if [ -z "$RUN_ID" ]; then
  echo "Usage: ./collect-artifacts.sh <github-run-id>"
  echo ""
  echo "Find the run ID by running:"
  echo "  gh run list --workflow=build-native.yml"
  exit 1
fi

echo "Downloading artifacts from run $RUN_ID..."

# Create temporary artifacts directory
rm -rf artifacts
mkdir -p artifacts

# Download each platform's artifacts
gh run download "$RUN_ID" -n native-win-x64 -D artifacts/win-x64
gh run download "$RUN_ID" -n native-win-x86 -D artifacts/win-x86
gh run download "$RUN_ID" -n native-osx-x64 -D artifacts/osx-x64
gh run download "$RUN_ID" -n native-osx-arm64 -D artifacts/osx-arm64
gh run download "$RUN_ID" -n native-linux-x64 -D artifacts/linux-x64

echo ""
echo "Organizing binaries into runtime folders..."

# Create runtime directories
mkdir -p src/GifBolt.Core/runtimes/win-x64/native
mkdir -p src/GifBolt.Core/runtimes/win-x86/native
mkdir -p src/GifBolt.Core/runtimes/osx-x64/native
mkdir -p src/GifBolt.Core/runtimes/osx-arm64/native
mkdir -p src/GifBolt.Core/runtimes/linux-x64/native

# Copy artifacts to runtime folders
cp artifacts/win-x64/GifBolt.Native.dll src/GifBolt.Core/runtimes/win-x64/native/
cp artifacts/win-x86/GifBolt.Native.dll src/GifBolt.Core/runtimes/win-x86/native/
cp artifacts/osx-x64/libGifBolt.Native.dylib src/GifBolt.Core/runtimes/osx-x64/native/
cp artifacts/osx-arm64/libGifBolt.Native.dylib src/GifBolt.Core/runtimes/osx-arm64/native/
cp artifacts/linux-x64/libGifBolt.Native.so src/GifBolt.Core/runtimes/linux-x64/native/

# Verify all files exist
echo ""
echo "Verifying files..."
for runtime in win-x64 win-x86 osx-x64 osx-arm64 linux-x64; do
  if [ "$runtime" == "win-x64" ] || [ "$runtime" == "win-x86" ]; then
    file="src/GifBolt.Core/runtimes/$runtime/native/GifBolt.Native.dll"
  else
    file="src/GifBolt.Core/runtimes/$runtime/native/libGifBolt.Native.so"
    if [ "$runtime" == "osx-x64" ] || [ "$runtime" == "osx-arm64" ]; then
      file="src/GifBolt.Core/runtimes/$runtime/native/libGifBolt.Native.dylib"
    fi
  fi

  if [ -f "$file" ]; then
    size=$(ls -lh "$file" | awk '{print $5}')
    echo "  ✓ $runtime ($size)"
  else
    echo "  ✗ $runtime (missing!)"
    exit 1
  fi
done

# Clean up temporary artifacts directory
rm -rf artifacts

echo ""
echo "✓ Done! All native binaries are ready."
echo ""
echo "To pack the NuGet package, run:"
echo "  cd src/GifBolt.Core && dotnet pack -c Release -o ../../nupkg"
