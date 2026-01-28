// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#include "D3D9ExDeviceCommandContext.h"
#include "D3D9ExTexture.h"
#include "PixelConversion.h"

#ifdef _WIN32

#include <initguid.h>
#include <d3d9.h>
#include <dxva2api.h>
#include <wrl/client.h>
#include <stdexcept>
#include <cstring>

// Explicitly define GUIDs if not available in headers
DEFINE_GUID(IID_IDirect3D9Ex_, 0x02177241, 0x69FC, 0x400C, 0x8F, 0xF1, 0x93, 0x52, 0x85, 0xF7, 0x25, 0x45);
DEFINE_GUID(IID_IDirect3DDevice9Ex_, 0xB18B10CE, 0x2649, 0x405B, 0x9F, 0x12, 0x16, 0xEA, 0x94, 0x25, 0x3E, 0xF3);

using Microsoft::WRL::ComPtr;

namespace GifBolt
{
namespace Renderer
{

struct D3D9ExDeviceCommandContext::Impl
{
    ComPtr<IDirect3D9Ex> d3d9ex;
    ComPtr<IDirect3DDevice9Ex> device;

    Impl()
    {
        // Try Direct3D 9Ex first (Windows Vista+)
        HRESULT hr = Direct3DCreate9Ex(D3D_SDK_VERSION, &d3d9ex);
        
        if (FAILED(hr))
        {
            char dbgMsg[256];
            sprintf_s(dbgMsg, sizeof(dbgMsg), 
                "[D3D9Ex] Direct3DCreate9Ex failed with HR=0x%08X, trying fallback...\n", hr);
            OutputDebugStringA(dbgMsg);
            
            // Fallback: try standard Direct3D 9 (available on all Windows)
            IDirect3D9* d3d9 = Direct3DCreate9(D3D_SDK_VERSION);
            if (!d3d9)
            {
                OutputDebugStringA("[D3D9Ex] Direct3DCreate9 also failed - d3d9.dll not found\n");
                throw std::runtime_error("Direct3D 9 not available - d3d9.dll not found or failed to load");
            }
            
            // We got D3D9, but we need D3D9Ex interface for the rest of the code
            // Try to get the Ex interface
            hr = d3d9->QueryInterface(IID_IDirect3D9Ex_, (void**)&d3d9ex);
            d3d9->Release();
            
            if (FAILED(hr))
            {
                char dbgMsg2[256];
                sprintf_s(dbgMsg2, sizeof(dbgMsg2), 
                    "[D3D9Ex] QueryInterface for D3D9Ex failed with HR=0x%08X\n", hr);
                OutputDebugStringA(dbgMsg2);
                throw std::runtime_error("D3D9Ex interface not available on this system (requires Windows Vista or later)");
            }
        }

        // Set up presentation parameters for off-screen rendering
        D3DPRESENT_PARAMETERS pp = {};
        pp.Windowed = TRUE;
        pp.hDeviceWindow = GetDesktopWindow();
        pp.SwapEffect = D3DSWAPEFFECT_DISCARD;
        pp.BackBufferCount = 1;
        pp.BackBufferFormat = D3DFMT_A8R8G8B8;
        pp.AutoDepthStencilFormat = D3DFMT_D24S8;
        pp.EnableAutoDepthStencil = TRUE;

        // Create the device (hardware vertex processing with software fallback)
        hr = d3d9ex->CreateDeviceEx(D3DADAPTER_DEFAULT, D3DDEVTYPE_HAL, GetDesktopWindow(),
                                    D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED,
                                    &pp, nullptr, &device);

        if (FAILED(hr))
        {
            // Fallback to software vertex processing
            hr = d3d9ex->CreateDeviceEx(D3DADAPTER_DEFAULT, D3DDEVTYPE_HAL, GetDesktopWindow(),
                                        D3DCREATE_SOFTWARE_VERTEXPROCESSING | D3DCREATE_MULTITHREADED,
                                        &pp, nullptr, &device);
            OutputDebugStringA("[D3D9Ex] Retried CreateDeviceEx with SOFTWARE_VERTEXPROCESSING\n");
        }

        if (FAILED(hr))
        {
            char errMsg[512];
            sprintf_s(errMsg, sizeof(errMsg), 
                "[D3D9Ex] CRITICAL: CreateDeviceEx failed with HR=0x%08X - throwing exception\n", hr);
            OutputDebugStringA(errMsg);
            
            char exMsg[256];
            sprintf_s(exMsg, sizeof(exMsg), 
                "Failed to create Direct3D 9Ex device - CreateDeviceEx returned 0x%08X", hr);
            throw std::runtime_error(exMsg);
        }
        
        OutputDebugStringA("[D3D9Ex] Device created successfully\n");

        // Enable alpha blending for proper transparency compositing
        // D3DImage surfaces need alpha blending enabled for WPF to composite alpha correctly
        if (device)
        {
            // Enable alpha blending
            device->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE);
            
            // Set blend factors for premultiplied alpha
            // Source is already premultiplied, so use: src * 1 + dst * (1 - srcAlpha)
            device->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_ONE);
            device->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA);
            
            // Set alpha test to discard fully transparent pixels
            device->SetRenderState(D3DRS_ALPHATESTENABLE, TRUE);
            device->SetRenderState(D3DRS_ALPHAREF, 0x00);
            device->SetRenderState(D3DRS_ALPHAFUNC, D3DCMP_GREATER);
            
            OutputDebugStringA("[D3D9Ex] Alpha blending enabled for transparency support\n");
        }
    }

    ~Impl()
    {
        if (device)
        {
            device->Release();
        }
        if (d3d9ex)
        {
            d3d9ex->Release();
        }
    }
};

D3D9ExDeviceCommandContext::D3D9ExDeviceCommandContext() : _impl(new Impl())
{
}

D3D9ExDeviceCommandContext::~D3D9ExDeviceCommandContext()
{
    delete _impl;
}

Backend D3D9ExDeviceCommandContext::GetBackend() const
{
    return Backend::D3D9Ex;
}

std::shared_ptr<ITexture> D3D9ExDeviceCommandContext::CreateTexture(uint32_t width, uint32_t height,
                                                                     const void* rgba32Pixels,
                                                                     size_t byteCount)
{
    return std::make_shared<D3D9ExTexture>(_impl->device.Get(), width, height, rgba32Pixels,
                                           byteCount);
}

void D3D9ExDeviceCommandContext::BeginFrame()
{
    if (_impl && _impl->device)
    {
        _impl->device->BeginScene();
    }
}

void D3D9ExDeviceCommandContext::Clear(float r, float g, float b, float a)
{
    if (_impl && _impl->device)
    {
        D3DCOLOR color =
            D3DCOLOR_COLORVALUE(r, g, b, a);  // D3DCOLOR_COLORVALUE handles 0.0-1.0 range
        _impl->device->Clear(0, nullptr, D3DCLEAR_TARGET, color, 1.0f, 0);
    }
}

void D3D9ExDeviceCommandContext::DrawTexture(ITexture* texture, int x, int y, int width,
                                             int height)
{
    // Placeholder: D3D9Ex texture drawing would be implemented here
    // For now, this is a no-op since we're primarily using this for D3DImage interop
    (void)texture;
    (void)x;
    (void)y;
    (void)width;
    (void)height;
}

void D3D9ExDeviceCommandContext::EndFrame()
{
    if (_impl && _impl->device)
    {
        _impl->device->EndScene();
    }
}

void D3D9ExDeviceCommandContext::Flush()
{
    if (_impl && _impl->device)
    {
        // Force GPU to process all pending commands
        _impl->device->GetRenderTargetData(nullptr, nullptr);
    }
}

void* D3D9ExDeviceCommandContext::GetD3D9ExDevice() const
{
    return _impl ? _impl->device.Get() : nullptr;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // _WIN32
