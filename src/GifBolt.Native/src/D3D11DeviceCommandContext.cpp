// SPDX-License-Identifier: MIT
#if defined(_WIN32) || defined(_WIN64)

#include "D3D11DeviceCommandContext.h"

#include <d3d11.h>
#include <dxgi.h>
#include <wrl/client.h>

#include <vector>

#include "ITexture.h"

using Microsoft::WRL::ComPtr;

namespace GifBolt
{
namespace Renderer
{

class D3D11Texture : public ITexture
{
   public:
    D3D11Texture(ComPtr<ID3D11Device> device, uint32_t width, uint32_t height, const void* data,
                 size_t byteCount)
        : _width(width), _height(height)
    {
        D3D11_TEXTURE2D_DESC desc = {};
        desc.Width = width;
        desc.Height = height;
        desc.MipLevels = 1;
        desc.ArraySize = 1;
        desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.SampleDesc.Count = 1;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;

        D3D11_SUBRESOURCE_DATA init = {};
        init.pSysMem = data;
        init.SysMemPitch = width * 4;

        device->CreateTexture2D(&desc, data ? &init : nullptr, _tex.ReleaseAndGetAddressOf());
        if (_tex)
        {
            D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
            srvDesc.Format = desc.Format;
            srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
            srvDesc.Texture2D.MipLevels = 1;
            device->CreateShaderResourceView(_tex.Get(), &srvDesc, _srv.ReleaseAndGetAddressOf());
        }
    }

    void Update(const void* rgba32Pixels, size_t byteCount) override
    {
        if (!_context || !_tex)
            return;
        _context->UpdateSubresource(_tex.Get(), 0, nullptr, rgba32Pixels, _width * 4, 0);
    }

    void SetContext(ComPtr<ID3D11DeviceContext> ctx)
    {
        _context = ctx;
    }

    ID3D11ShaderResourceView* Srv() const
    {
        return _srv.Get();
    }

   private:
    uint32_t _width;
    uint32_t _height;
    ComPtr<ID3D11Texture2D> _tex;
    ComPtr<ID3D11ShaderResourceView> _srv;
    ComPtr<ID3D11DeviceContext> _context;
};

struct D3D11DeviceCommandContext::Impl
{
    ComPtr<ID3D11Device> device;
    ComPtr<ID3D11DeviceContext> context;
    // Minimal pipeline placeholders; swapchain integration with WPF will follow.
};

D3D11DeviceCommandContext::D3D11DeviceCommandContext()
{
    UINT flags = 0;
    D3D_FEATURE_LEVEL level;
    D3D_FEATURE_LEVEL levels[] = {D3D_FEATURE_LEVEL_11_0};
    Impl* p = new Impl();
    _impl = p;
    D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags, levels, 1,
                      D3D11_SDK_VERSION, p->device.ReleaseAndGetAddressOf(), &level,
                      p->context.ReleaseAndGetAddressOf());
}

D3D11DeviceCommandContext::~D3D11DeviceCommandContext()
{
    delete _impl;
}

IDeviceCommandContext::Backend D3D11DeviceCommandContext::GetBackend() const
{
    return Backend::D3D11;
}

std::shared_ptr<ITexture> D3D11DeviceCommandContext::CreateTexture(uint32_t width, uint32_t height,
                                                                   const void* rgba32Pixels,
                                                                   size_t byteCount)
{
    auto tex =
        std::make_shared<D3D11Texture>(_impl->device, width, height, rgba32Pixels, byteCount);
    static_cast<D3D11Texture*>(tex.get())->SetContext(_impl->context);
    return tex;
}

void D3D11DeviceCommandContext::BeginFrame()
{
}

void D3D11DeviceCommandContext::Clear(float, float, float, float)
{
}

void D3D11DeviceCommandContext::DrawTexture(ITexture* /*texture*/, int, int, int, int)
{
}

void D3D11DeviceCommandContext::EndFrame()
{
}

void D3D11DeviceCommandContext::Flush()
{
    if (_impl && _impl->context)
        _impl->context->Flush();
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // _WIN32
