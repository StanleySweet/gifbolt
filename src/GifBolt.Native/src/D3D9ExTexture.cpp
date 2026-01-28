// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "D3D9ExTexture.h"
#include "DebugLog.h"
#include "PixelConversion.h"

#ifdef _WIN32

#include <d3d9.h>
#include <wrl/client.h>
#include <algorithm>
#include <cstring>
#include <stdexcept>
#include <memory>
#include <fstream>
#include <stdio.h>
#include <stdlib.h>

using Microsoft::WRL::ComPtr;

namespace GifBolt
{
namespace Renderer
{

struct D3D9ExTexture::Impl
{
    uint32_t width;
    uint32_t height;
    uint32_t frameCounter = 0;  // Track frame number for debugging
    ComPtr<IDirect3DTexture9> d3d9Texture;        // Primary texture (on GPU)
    ComPtr<IDirect3DTexture9> d3d9TextureAlt;     // Alternate texture for double-buffering
    ComPtr<IDirect3DSurface9> d3d9Surface;        // Primary surface (currently displayed by D3DImage)
    ComPtr<IDirect3DSurface9> d3d9SurfaceAlt;     // Alternate surface (being updated)
    ComPtr<IDirect3DSurface9> lockableSurface;    // CPU-lockable surface
    ComPtr<IDirect3DSurface9> lockableSurfaceAlt; // Alternate CPU-lockable surface
    bool displayingAlt = false;  // Whether we're currently displaying the alt surface
    HANDLE sharedHandle;
    HANDLE sharedHandleAlt;

    Impl(void* d3d9Device, uint32_t w, uint32_t h, const void* initialData, size_t initialDataSize)
        : width(w), height(h), sharedHandle(nullptr), sharedHandleAlt(nullptr), displayingAlt(false)
    {
        IDirect3DDevice9Ex* device = static_cast<IDirect3DDevice9Ex*>(d3d9Device);

        // Create main texture (on GPU for rendering)
        HRESULT hr = device->CreateTexture(width, height, 1, D3DUSAGE_RENDERTARGET,
                                          D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &d3d9Texture, &sharedHandle);

        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to create D3D9Ex texture");
        }

        // Get surface from texture to use as D3DImage back buffer
        hr = d3d9Texture->GetSurfaceLevel(0, &d3d9Surface);
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to get surface from texture");
        }

        // Create alternate texture for double-buffering GPU surfaces
        hr = device->CreateTexture(width, height, 1, D3DUSAGE_RENDERTARGET,
                                   D3DFMT_A8R8G8B8, D3DPOOL_DEFAULT, &d3d9TextureAlt, &sharedHandleAlt);
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to create alternate D3D9Ex texture");
        }

        // Get surface from alternate texture
        hr = d3d9TextureAlt->GetSurfaceLevel(0, &d3d9SurfaceAlt);
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to get surface from alternate texture");
        }

        // Create separate lockable offscreen surface for CPU pixel upload
        hr = device->CreateOffscreenPlainSurface(width, height, D3DFMT_A8R8G8B8,
                                                  D3DPOOL_SYSTEMMEM, &lockableSurface, nullptr);
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to create lockable surface");
        }

        // Create alternate lockable surface for double-buffering
        hr = device->CreateOffscreenPlainSurface(width, height, D3DFMT_A8R8G8B8,
                                                  D3DPOOL_SYSTEMMEM, &lockableSurfaceAlt, nullptr);
        if (FAILED(hr))
        {
            throw std::runtime_error("Failed to create alternate lockable surface");
        }

        // Initialize with data if provided
        if (initialData && initialDataSize > 0)
        {
            // initialData is already BGRA premultiplied from GetFramePixelsBGRA32Premultiplied

            // Upload to both lockable surfaces for consistent state
            D3DLOCKED_RECT locked = {};
            
            // Copy to primary lockable surface
            hr = lockableSurface->LockRect(&locked, nullptr, 0);
            if (SUCCEEDED(hr))
            {
                uint8_t* dest = reinterpret_cast<uint8_t*>(locked.pBits);
                const uint8_t* src = reinterpret_cast<const uint8_t*>(initialData);
                for (uint32_t y = 0; y < height; y++)
                {
                    std::memcpy(dest + y * locked.Pitch, src + y * width * 4, width * 4);
                }
                lockableSurface->UnlockRect();

                // Copy from primary lockable to primary GPU surface
                hr = device->UpdateSurface(lockableSurface.Get(), nullptr, d3d9Surface.Get(), nullptr);
            }
            
            // Copy to alternate lockable surface
            hr = lockableSurfaceAlt->LockRect(&locked, nullptr, 0);
            if (SUCCEEDED(hr))
            {
                uint8_t* dest = reinterpret_cast<uint8_t*>(locked.pBits);
                const uint8_t* src = reinterpret_cast<const uint8_t*>(initialData);
                for (uint32_t y = 0; y < height; y++)
                {
                    std::memcpy(dest + y * locked.Pitch, src + y * width * 4, width * 4);
                }
                lockableSurfaceAlt->UnlockRect();

                // Copy from alternate lockable to alternate GPU surface
                hr = device->UpdateSurface(lockableSurfaceAlt.Get(), nullptr, d3d9SurfaceAlt.Get(), nullptr);
            }
            
            // CRITICAL: Ensure GPU has fully completed the surface updates before texture is used
            if (SUCCEEDED(hr))
            {
                IDirect3DQuery9* query = nullptr;
                if (SUCCEEDED(device->CreateQuery(D3DQUERYTYPE_EVENT, &query)) && query)
                {
                    query->Issue(D3DISSUE_END);
                    // Block until GPU has finished all operations
                    DWORD data = 0;
                    while (query->GetData(&data, sizeof(data), D3DGETDATA_FLUSH) == S_FALSE)
                    {
                        // Spin-wait for GPU to complete
                    }
                    query->Release();
                }
            }
        }
    }

    ~Impl()
    {
        if (lockableSurfaceAlt)
        {
            lockableSurfaceAlt->Release();
        }
        if (lockableSurface)
        {
            lockableSurface->Release();
        }
        if (d3d9SurfaceAlt)
        {
            d3d9SurfaceAlt->Release();
        }
        if (d3d9Surface)
        {
            d3d9Surface->Release();
        }
        if (d3d9TextureAlt)
        {
            d3d9TextureAlt->Release();
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
    if (!data || dataSize == 0 || !_impl)
    {
        return false;
    }

    // Determine which surfaces to update (the ones NOT currently being displayed)
    IDirect3DSurface9* updateLockable = _impl->displayingAlt ? _impl->lockableSurface.Get() : _impl->lockableSurfaceAlt.Get();
    IDirect3DSurface9* updateGPUSurface = _impl->displayingAlt ? _impl->d3d9Surface.Get() : _impl->d3d9SurfaceAlt.Get();

    if (!updateLockable || !updateGPUSurface)
    {
        return false;
    }

    // data is already BGRA premultiplied from GetFramePixelsBGRA32Premultiplied
    const size_t bufferSize = _impl->width * _impl->height * 4;
    if (dataSize < bufferSize)
    {
        return false;
    }

    // Lock the update lockable surface (render target surfaces cannot be locked)
    D3DLOCKED_RECT locked = {};
    HRESULT hr = updateLockable->LockRect(&locked, nullptr, 0);
    if (FAILED(hr))
    {
        return false;
    }

    // Copy data row-by-row to respect pitch
    uint8_t* dest = reinterpret_cast<uint8_t*>(locked.pBits);
    const uint8_t* src = reinterpret_cast<const uint8_t*>(data);
    for (uint32_t y = 0; y < _impl->height; y++)
    {
        std::memcpy(dest + y * locked.Pitch, src + y * _impl->width * 4, _impl->width * 4);
    }
    updateLockable->UnlockRect();
    
    // DEBUG: Dump frame to disk for inspection
    {
        char tempPath[512];
        char dumpDir[512];
        if (SUCCEEDED(GetEnvironmentVariableA("TEMP", dumpDir, sizeof(dumpDir))))
        {
            sprintf_s(tempPath, sizeof(tempPath), "%s\\gifbolt_frame_%04u.raw", dumpDir, _impl->frameCounter);
            std::ofstream file(tempPath, std::ios::binary);
            if (file.is_open())
            {
                // Write width and height as little-endian uint32
                uint32_t w = _impl->width;
                uint32_t h = _impl->height;
                file.write(reinterpret_cast<const char*>(&w), 4);
                file.write(reinterpret_cast<const char*>(&h), 4);
                // Write raw BGRA32 data
                file.write(reinterpret_cast<const char*>(src), bufferSize);
                file.close();
            }
            
            // Also write a metadata file
            sprintf_s(tempPath, sizeof(tempPath), "%s\\gifbolt_frame_%04u.txt", dumpDir, _impl->frameCounter);
            std::ofstream meta(tempPath);
            if (meta.is_open())
            {
                meta << "Frame: " << _impl->frameCounter << "\n";
                meta << "Width: " << _impl->width << "\n";
                meta << "Height: " << _impl->height << "\n";
                meta << "DisplayingAlt: " << (_impl->displayingAlt ? "true" : "false") << "\n";
                meta << "BufferSize: " << bufferSize << "\n";
                meta.close();
            }
        }
        _impl->frameCounter++;
    }

    // Transfer from CPU surface to GPU texture surface (the non-displayed one)
    IDirect3DDevice9* device = nullptr;
    if (_impl->d3d9Texture)
    {
        hr = _impl->d3d9Texture->GetDevice(&device);
        if (SUCCEEDED(hr) && device)
        {
            hr = device->UpdateSurface(updateLockable, nullptr, updateGPUSurface, nullptr);
            
            // CRITICAL: Ensure GPU has fully completed the surface update before swapping display surfaces
            if (SUCCEEDED(hr))
            {
                IDirect3DQuery9* query = nullptr;
                if (SUCCEEDED(device->CreateQuery(D3DQUERYTYPE_EVENT, &query)) && query)
                {
                    query->Issue(D3DISSUE_END);
                    // Block until GPU has finished all operations
                    DWORD eventData = 0;
                    while (query->GetData(&eventData, sizeof(eventData), D3DGETDATA_FLUSH) == S_FALSE)
                    {
                        // Spin-wait for GPU to complete
                    }
                    query->Release();
                }
                
                // SUCCESS: Now swap which surface is displayed for next frame
                _impl->displayingAlt = !_impl->displayingAlt;
            }
            
            device->Release();
            return SUCCEEDED(hr);
        }
    }

    return false;
}

void* D3D9ExTexture::GetNativeTexturePtr()
{
    if (!_impl)
    {
        return nullptr;
    }

    // Return the surface currently being displayed, not the one being updated
    IDirect3DSurface9* displaySurface = _impl->displayingAlt ? _impl->d3d9SurfaceAlt.Get() : _impl->d3d9Surface.Get();
    
    if (!displaySurface)
    {
        return nullptr;
    }

    // AddRef before returning so caller owns a reference
    // (D3DImage will Release this when done)
    displaySurface->AddRef();
    return displaySurface;
}

void* D3D9ExTexture::GetSharedResourceHandle() const
{
    if (!_impl)
    {
        return nullptr;
    }
    
    // Return the handle for the surface currently being displayed
    return _impl->displayingAlt ? _impl->sharedHandleAlt : _impl->sharedHandle;
}

void* D3D9ExTexture::GetD3D11TexturePtr() const
{
    // D3D9Ex doesn't support D3D11 interop - return nullptr
    return nullptr;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // _WIN32
