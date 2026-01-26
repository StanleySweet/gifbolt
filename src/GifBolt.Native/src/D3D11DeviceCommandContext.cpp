// SPDX-License-Identifier: MIT
#ifdef _WIN32

#include "D3D11DeviceCommandContext.h"

#include <d3d11.h>
#include <d3dcompiler.h>
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
    D3D11Texture(const ComPtr<ID3D11Device>& device, uint32_t width, uint32_t height,
                 const void* data, size_t byteCount)
        : _width(width), _height(height)
    {
        (void)byteCount;  // Used for validation in calling code
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

    bool Update(const void* rgba32Pixels, size_t byteCount) override
    {
        (void)byteCount;  // Size validated by caller
        if (!_context || !_tex)
        {
            return false;
        }
        _context->UpdateSubresource(_tex.Get(), 0, nullptr, rgba32Pixels, _width * 4, 0);
        return true;
    }

    uint32_t GetWidth() const override
    {
        return _width;
    }

    uint32_t GetHeight() const override
    {
        return _height;
    }

    PixelFormats::Format GetFormat() const override
    {
        return PixelFormats::Format::R8G8B8A8_UNORM;
    }

    void SetContext(ComPtr<ID3D11DeviceContext> ctx)
    {
        _context = ctx;
    }

    ID3D11ShaderResourceView* Srv() const
    {
        return _srv.Get();
    }

    void* GetNativeTexturePtr() override
    {
        return _tex.Get();
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
    ComPtr<ID3D11ComputeShader> conversionShader;
    ComPtr<ID3D11Buffer> constantBuffer;
    bool computeShaderReady = false;

    bool InitializeComputeShader();
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

    // Initialize compute shader for GPU-accelerated pixel conversion
    if (p->device && p->context)
    {
        p->computeShaderReady = p->InitializeComputeShader();
    }
}

D3D11DeviceCommandContext::~D3D11DeviceCommandContext()
{
    delete _impl;
}

Backend D3D11DeviceCommandContext::GetBackend() const
{
    return Backend::D3D11;
}

std::shared_ptr<ITexture> D3D11DeviceCommandContext::CreateTexture(uint32_t width, uint32_t height,
                                                                   const void* rgba32Pixels,
                                                                   size_t byteCount)
{
    auto tex =
        std::make_shared<D3D11Texture>(_impl->device, width, height, rgba32Pixels, byteCount);
    if (tex)
    {
        static_cast<D3D11Texture*>(tex.get())->SetContext(_impl->context);
    }
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

bool D3D11DeviceCommandContext::Impl::InitializeComputeShader()
{
    // Inline HLSL compute shader source (embedded for simplicity)
    const char* shaderSource = R"(
        cbuffer ConversionParams : register(b0)
        {
            uint pixelCount;
            uint padding0;
            uint padding1;
            uint padding2;
        };

        StructuredBuffer<uint> inputRGBA : register(t0);
        RWStructuredBuffer<uint> outputBGRA : register(u0);

        [numthreads(256, 1, 1)]
        void ConvertRGBAToBGRAPremultiplied(uint3 dispatchThreadID : SV_DispatchThreadID)
        {
            uint idx = dispatchThreadID.x;
            if (idx >= pixelCount)
                return;

            uint rgba = inputRGBA[idx];
            uint r = (rgba & 0x000000FF);
            uint g = (rgba & 0x0000FF00) >> 8;
            uint b = (rgba & 0x00FF0000) >> 16;
            uint a = (rgba & 0xFF000000) >> 24;

            uint rPremul = (r * a + 128) >> 8;
            uint gPremul = (g * a + 128) >> 8;
            uint bPremul = (b * a + 128) >> 8;

            uint bgra = bPremul | (gPremul << 8) | (rPremul << 16) | (a << 24);
            outputBGRA[idx] = bgra;
        }
    )";

    ComPtr<ID3DBlob> shaderBlob;
    ComPtr<ID3DBlob> errorBlob;

    HRESULT hr = D3DCompile(shaderSource, strlen(shaderSource), nullptr, nullptr, nullptr,
                            "ConvertRGBAToBGRAPremultiplied", "cs_5_0", 0, 0,
                            shaderBlob.GetAddressOf(), errorBlob.GetAddressOf());

    if (FAILED(hr))
    {
        return false;
    }

    hr = this->device->CreateComputeShader(shaderBlob->GetBufferPointer(),
                                           shaderBlob->GetBufferSize(), nullptr,
                                           this->conversionShader.GetAddressOf());

    if (FAILED(hr))
    {
        return false;
    }

    // Create constant buffer for parameters
    D3D11_BUFFER_DESC cbDesc = {};
    cbDesc.ByteWidth = 16;  // 4 uints (aligned to 16 bytes)
    cbDesc.Usage = D3D11_USAGE_DYNAMIC;
    cbDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    cbDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

    hr = this->device->CreateBuffer(&cbDesc, nullptr, this->constantBuffer.GetAddressOf());

    return SUCCEEDED(hr);
}

bool D3D11DeviceCommandContext::ConvertRGBAToBGRAPremultipliedGPU(const void* inputRGBA,
                                                                  void* outputBGRA,
                                                                  uint32_t pixelCount)
{
    if (!_impl || !_impl->computeShaderReady || !_impl->device || !_impl->context)
    {
        return false;
    }

    const uint32_t byteCount = pixelCount * 4;

    // Create input buffer (structured buffer)
    D3D11_BUFFER_DESC inputDesc = {};
    inputDesc.ByteWidth = byteCount;
    inputDesc.Usage = D3D11_USAGE_DEFAULT;
    inputDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    inputDesc.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
    inputDesc.StructureByteStride = 4;  // 1 uint per pixel

    D3D11_SUBRESOURCE_DATA initData = {};
    initData.pSysMem = inputRGBA;

    ComPtr<ID3D11Buffer> inputBuffer;
    HRESULT hr = _impl->device->CreateBuffer(&inputDesc, &initData, inputBuffer.GetAddressOf());
    if (FAILED(hr))
        return false;

    // Create input SRV
    D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
    srvDesc.Format = DXGI_FORMAT_UNKNOWN;
    srvDesc.ViewDimension = D3D11_SRV_DIMENSION_BUFFER;
    srvDesc.Buffer.FirstElement = 0;
    srvDesc.Buffer.NumElements = pixelCount;

    ComPtr<ID3D11ShaderResourceView> inputSRV;
    hr = _impl->device->CreateShaderResourceView(inputBuffer.Get(), &srvDesc,
                                                 inputSRV.GetAddressOf());
    if (FAILED(hr))
        return false;

    // Create output buffer (UAV)
    D3D11_BUFFER_DESC outputDesc = inputDesc;
    outputDesc.BindFlags = D3D11_BIND_UNORDERED_ACCESS;

    ComPtr<ID3D11Buffer> outputBuffer;
    hr = _impl->device->CreateBuffer(&outputDesc, nullptr, outputBuffer.GetAddressOf());
    if (FAILED(hr))
        return false;

    // Create output UAV
    D3D11_UNORDERED_ACCESS_VIEW_DESC uavDesc = {};
    uavDesc.Format = DXGI_FORMAT_UNKNOWN;
    uavDesc.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
    uavDesc.Buffer.FirstElement = 0;
    uavDesc.Buffer.NumElements = pixelCount;

    ComPtr<ID3D11UnorderedAccessView> outputUAV;
    hr = _impl->device->CreateUnorderedAccessView(outputBuffer.Get(), &uavDesc,
                                                  outputUAV.GetAddressOf());
    if (FAILED(hr))
        return false;

    // Update constant buffer with pixel count
    D3D11_MAPPED_SUBRESOURCE mapped;
    hr = _impl->context->Map(_impl->constantBuffer.Get(), 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped);
    if (SUCCEEDED(hr))
    {
        uint32_t* params = static_cast<uint32_t*>(mapped.pData);
        params[0] = pixelCount;
        params[1] = 0;
        params[2] = 0;
        params[3] = 0;
        _impl->context->Unmap(_impl->constantBuffer.Get(), 0);
    }

    // Set shader and resources
    _impl->context->CSSetShader(_impl->conversionShader.Get(), nullptr, 0);
    _impl->context->CSSetConstantBuffers(0, 1, _impl->constantBuffer.GetAddressOf());
    _impl->context->CSSetShaderResources(0, 1, inputSRV.GetAddressOf());
    _impl->context->CSSetUnorderedAccessViews(0, 1, outputUAV.GetAddressOf(), nullptr);

    // Dispatch compute shader (256 threads per group)
    const uint32_t threadGroupCount = (pixelCount + 255) / 256;
    _impl->context->Dispatch(threadGroupCount, 1, 1);

    // Unbind resources
    ID3D11ShaderResourceView* nullSRV = nullptr;
    ID3D11UnorderedAccessView* nullUAV = nullptr;
    _impl->context->CSSetShaderResources(0, 1, &nullSRV);
    _impl->context->CSSetUnorderedAccessViews(0, 1, &nullUAV, nullptr);

    // Copy result back to CPU memory
    D3D11_BUFFER_DESC stagingDesc = {};
    stagingDesc.ByteWidth = byteCount;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;

    ComPtr<ID3D11Buffer> stagingBuffer;
    hr = _impl->device->CreateBuffer(&stagingDesc, nullptr, stagingBuffer.GetAddressOf());
    if (FAILED(hr))
        return false;

    _impl->context->CopyResource(stagingBuffer.Get(), outputBuffer.Get());

    // Map and copy to output
    hr = _impl->context->Map(stagingBuffer.Get(), 0, D3D11_MAP_READ, 0, &mapped);
    if (SUCCEEDED(hr))
    {
        memcpy(outputBGRA, mapped.pData, byteCount);
        _impl->context->Unmap(stagingBuffer.Get(), 0);
        return true;
    }

    return false;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // _WIN32
