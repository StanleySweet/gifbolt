// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "D3D9ExTexture.h"
#include "DebugLog.h"
#include "PixelConversion.h"

#ifdef _WIN32

#define NOMINMAX  // Prevent Windows.h from defining min/max macros

#include <d3d9.h>
#include <d3d11.h>
#include <dxgi.h>
#include <wrl/client.h>
#include <algorithm>
#include <cstring>
#include <stdexcept>
#include <memory>

using Microsoft::WRL::ComPtr;

namespace GifBolt
{
namespace Renderer
{

struct D3D9ExTexture::Impl
{
    uint32_t width;
    uint32_t height;
    ComPtr<IDirect3DTexture9> d3d9Texture;      // Main texture (on GPU)
    ComPtr<IDirect3DSurface9> d3d9Surface;      // Surface from texture (for D3DImage)
    ComPtr<IDirect3DSurface9> lockableSurface;  // CPU-lockable surface (for pixel upload)
    ComPtr<ID3D11Texture2D> d3d11Texture;
    ComPtr<ID3D11Device> d3d11Device;
    ComPtr<ID3D11DeviceContext> d3d11DeviceContext;
    HANDLE sharedHandle;
    std::unique_ptr<uint8_t[]> stagingBuffer;  // CPU-side buffer for data

    Impl(void* d3d9Device, uint32_t w, uint32_t h, const void* initialData, size_t initialDataSize)
        : width(w), height(h), sharedHandle(nullptr)
    {
        IDirect3DDevice9Ex* device = static_cast<IDirect3DDevice9Ex*>(d3d9Device);

        // Create main texture (on GPU for rendering)
        HRESULT hr = device->CreateTexture(width, height, 1, D3DUSAGE_RENDERTARGET,
                                          D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &d3d9Texture, &sharedHandle);

        if (FAILED(hr))
        {
            DebugLog("[D3D9ExTexture] CreateTexture failed: 0x%08X\n", hr);
            throw std::runtime_error("Failed to create D3D9Ex texture");
        }

        // Get surface from texture to use as D3DImage back buffer
        hr = d3d9Texture->GetSurfaceLevel(0, &d3d9Surface);
        if (FAILED(hr))
        {
            DebugLog("[D3D9ExTexture] GetSurfaceLevel failed: 0x%08X\n", hr);
            throw std::runtime_error("Failed to get surface from texture");
        }

        // Create separate lockable offscreen surface for CPU pixel upload
        hr = device->CreateOffscreenPlainSurface(width, height, D3DFMT_A8R8G8B8,
                                                  D3DPOOL_SYSTEMMEM, &lockableSurface, nullptr);
        if (FAILED(hr))
        {
            DebugLog("[D3D9ExTexture] CreateOffscreenPlainSurface failed: 0x%08X\n", hr);
            throw std::runtime_error("Failed to create lockable surface");
        }

        // Allocate staging buffer for CPU operations
        stagingBuffer = std::make_unique<uint8_t[]>(width * height * 4);

        // Initialize with data if provided
        if (initialData && initialDataSize > 0)
        {
            // Count non-zero bytes to verify data validity
            size_t nonZeroBytes = 0;
            const uint8_t* dataBytes = reinterpret_cast<const uint8_t*>(initialData);
            for (size_t i = 0; i < initialDataSize && i < width * height * 4; ++i)
            {
                if (dataBytes[i] != 0)
                {
                    nonZeroBytes++;
                }
            }
            
            DebugLog("[D3D9ExTexture] Impl: Uploading %zu bytes to %ux%u surface, non-zero=%zu/%zu (%.1f%%)\n", 
                initialDataSize, width, height, nonZeroBytes, initialDataSize, 
                (nonZeroBytes * 100.0f) / initialDataSize);
            
            if (nonZeroBytes == 0)
            {
                DebugLog("[D3D9ExTexture] WARNING: All bytes are zero! Surface will be completely transparent\n");
            }
            
            // initialData is already BGRA premultiplied from GetFramePixelsBGRA32Premultiplied
            // Just copy it directly to the lockable surface
            const size_t copySize = std::min(initialDataSize, static_cast<size_t>(width * height * 4));
            std::memcpy(stagingBuffer.get(), initialData, copySize);

            // Upload to lockable surface
            D3DLOCKED_RECT locked = {};
            hr = lockableSurface->LockRect(&locked, nullptr, 0);
            if (SUCCEEDED(hr))
            {
                DebugLog("[D3D9ExTexture] Locked surface at %p, pitch=%d\n", locked.pBits, locked.Pitch);
                // Copy exact pixel data (row by row respecting pitch)
                uint8_t* dest = reinterpret_cast<uint8_t*>(locked.pBits);
                const uint8_t* src = stagingBuffer.get();
                for (uint32_t y = 0; y < height; y++)
                {
                    std::memcpy(dest + y * locked.Pitch, src + y * width * 4, width * 4);
                }
                lockableSurface->UnlockRect();
                DebugLog("[D3D9ExTexture] Surface unlocked after upload\n");

                // Copy from lockable CPU surface to GPU texture surface
                hr = device->UpdateSurface(lockableSurface.Get(), nullptr, d3d9Surface.Get(), nullptr);
                if (SUCCEEDED(hr))
                {
                    DebugLog("[D3D9ExTexture] UpdateSurface: Copied pixel data to GPU texture - SUCCESS\n");
                }
                else
                {
                    DebugLog("[D3D9ExTexture] UpdateSurface FAILED: 0x%08X\n", hr);
                }
            }
            else
            {
                DebugLog("[D3D9ExTexture] FAILED to lock CPU surface: 0x%08X\n", hr);
            }
        }
        else
        {
            DebugLog("[D3D9ExTexture] Impl: No initial data (data=%p, size=%zu)\n", initialData, initialDataSize);
        }
    }

    ~Impl()
    {
        if (d3d11DeviceContext)
        {
            d3d11DeviceContext->Release();
        }
        if (d3d11Device)
        {
            d3d11Device->Release();
        }
        if (d3d11Texture)
        {
            d3d11Texture->Release();
        }
        if (lockableSurface)
        {
            lockableSurface->Release();
        }
        if (d3d9Surface)
        {
            d3d9Surface->Release();
        }
        if (d3d9Texture)
        {
            d3d9Texture->Release();
        }
    }
};

D3D9ExTexture::D3D9ExTexture(void* d3d9Device, uint32_t width, uint32_t height,
                             const void* initialData, size_t initialDataSize)
    : _impl(std::make_unique<Impl>(d3d9Device, width, height, initialData, initialDataSize))
{
}

D3D9ExTexture::~D3D9ExTexture() = default;

uint32_t D3D9ExTexture::GetWidth() const
{
    return _impl->width;
}

uint32_t D3D9ExTexture::GetHeight() const
{
    return _impl->height;
}

PixelFormats::Format D3D9ExTexture::GetFormat() const
{
    return PixelFormats::Format::R8G8B8A8_UNORM;
}

bool D3D9ExTexture::Update(const void* data, size_t dataSize)
{
    if (!data || dataSize == 0 || !_impl || !_impl->d3d9Surface)
    {
        return false;
    }

    // data is already BGRA premultiplied from GetFramePixelsBGRA32Premultiplied
    // Just verify size and copy directly
    const size_t bufferSize = _impl->width * _impl->height * 4;
    if (dataSize < bufferSize)
    {
        return false;
    }

    // Copy BGRA data directly to staging buffer
    std::memcpy(_impl->stagingBuffer.get(), data, bufferSize);

    // Lock and update surface
    D3DLOCKED_RECT locked = {};
    HRESULT hr = _impl->d3d9Surface->LockRect(&locked, nullptr, 0);
    if (SUCCEEDED(hr))
    {
        std::memcpy(locked.pBits, _impl->stagingBuffer.get(), bufferSize);
        _impl->d3d9Surface->UnlockRect();
        return true;
    }

    return false;
}

void* D3D9ExTexture::GetNativeTexturePtr()
{
    if (!_impl || !_impl->d3d9Surface)
    {
        return nullptr;
    }

    // AddRef before returning so caller owns a reference
    // (D3DImage will Release this when done)
    _impl->d3d9Surface->AddRef();
    return _impl->d3d9Surface.Get();
}

void* D3D9ExTexture::GetSharedResourceHandle() const
{
    return _impl ? _impl->sharedHandle : nullptr;
}

void* D3D9ExTexture::GetD3D11TexturePtr() const
{
    return _impl ? _impl->d3d11Texture.Get() : nullptr;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // _WIN32
