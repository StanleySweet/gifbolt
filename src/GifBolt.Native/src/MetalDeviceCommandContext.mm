// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

#if defined(__APPLE__)

#include "MetalDeviceCommandContext.h"

#import <Metal/Metal.h>
#import <QuartzCore/CAMetalLayer.h>

#include <vector>

#include "ITexture.h"

namespace GifBolt
{
namespace Renderer
{

/// \class MetalTexture
/// \brief Metal implementation of ITexture.
class MetalTexture : public ITexture
{
   public:
    MetalTexture(id<MTLDevice> device, uint32_t width, uint32_t height, const void* data,
                 size_t byteCount)
        : _width(width), _height(height)
    {
        (void)byteCount;
        MTLTextureDescriptor* descriptor = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatRGBA8Unorm
                                                                                              width:width
                                                                                             height:height
                                                                                          mipmapped:NO];
        descriptor.usage = MTLTextureUsageShaderRead;
        descriptor.storageMode = MTLStorageModeManaged;

        _texture = [device newTextureWithDescriptor:descriptor];

        if (data && _texture)
        {
            MTLRegion region = MTLRegionMake2D(0, 0, width, height);
            [_texture replaceRegion:region mipmapLevel:0 withBytes:data bytesPerRow:width * 4];
        }
    }

    ~MetalTexture()
    {
        if (_texture)
        {
            [_texture release];
            _texture = nil;
        }
    }

    uint32_t GetWidth() const override
    {
        return _width;
    }

    uint32_t GetHeight() const override
    {
        return _height;
    }

    TextureFormat GetFormat() const override
    {
        return TextureFormat::RGBA8;
    }

    bool Update(const void* rgba32Pixels, size_t byteCount) override
    {
        (void)byteCount;
        if (!_texture || !rgba32Pixels)
        {
            return false;
        }

        MTLRegion region = MTLRegionMake2D(0, 0, _width, _height);
        [_texture replaceRegion:region mipmapLevel:0 withBytes:rgba32Pixels bytesPerRow:_width * 4];
        return true;
    }

    id<MTLTexture> GetMetalTexture() const
    {
        return _texture;
    }

   private:
    uint32_t _width;
    uint32_t _height;
    id<MTLTexture> _texture;
};

/// \struct MetalDeviceCommandContext::Impl
/// \brief Private implementation details for Metal rendering context.
struct MetalDeviceCommandContext::Impl
{
    id<MTLDevice> device;
    id<MTLCommandQueue> commandQueue;
    id<MTLCommandBuffer> commandBuffer;
    id<MTLRenderCommandEncoder> renderEncoder;
    id<MTLRenderPipelineState> pipelineState;
    MTLRenderPassDescriptor* renderPassDescriptor;

    Impl()
        : device(nil), commandQueue(nil), commandBuffer(nil), renderEncoder(nil),
          pipelineState(nil), renderPassDescriptor(nil)
    {
    }

    ~Impl()
    {
        if (renderPassDescriptor)
        {
            [renderPassDescriptor release];
        }
        if (pipelineState)
        {
            [pipelineState release];
        }
        if (renderEncoder)
        {
            [renderEncoder release];
        }
        if (commandBuffer)
        {
            [commandBuffer release];
        }
        if (commandQueue)
        {
            [commandQueue release];
        }
        if (device)
        {
            [device release];
        }
    }
};

MetalDeviceCommandContext::MetalDeviceCommandContext()
{
    _impl = new Impl();

    // Get the default Metal device
    _impl->device = MTLCreateSystemDefaultDevice();
    if (!_impl->device)
    {
        throw std::runtime_error("Failed to create Metal device");
    }

    // Create command queue
    _impl->commandQueue = [_impl->device newCommandQueue];
    if (!_impl->commandQueue)
    {
        throw std::runtime_error("Failed to create Metal command queue");
    }

    // Create render pass descriptor
    _impl->renderPassDescriptor = [[MTLRenderPassDescriptor alloc] init];
    _impl->renderPassDescriptor.colorAttachments[0].loadAction = MTLLoadActionClear;
    _impl->renderPassDescriptor.colorAttachments[0].storeAction = MTLStoreActionStore;

    // Note: Full pipeline setup with shaders will be implemented when integrating with Avalonia
    // For now, this provides the basic Metal device initialization
}

MetalDeviceCommandContext::~MetalDeviceCommandContext()
{
    delete _impl;
}

Backend MetalDeviceCommandContext::GetBackend() const
{
    return Backend::Metal;
}

std::shared_ptr<ITexture> MetalDeviceCommandContext::CreateTexture(uint32_t width, uint32_t height,
                                                                    const void* rgba32Pixels,
                                                                    size_t byteCount)
{
    return std::make_shared<MetalTexture>(_impl->device, width, height, rgba32Pixels, byteCount);
}

void MetalDeviceCommandContext::BeginFrame()
{
    if (!_impl->commandQueue)
    {
        return;
    }

    // Create a new command buffer for this frame
    _impl->commandBuffer = [_impl->commandQueue commandBuffer];
}

void MetalDeviceCommandContext::Clear(float r, float g, float b, float a)
{
    if (_impl->renderPassDescriptor)
    {
        _impl->renderPassDescriptor.colorAttachments[0].clearColor = MTLClearColorMake(r, g, b, a);
    }
}

void MetalDeviceCommandContext::DrawTexture(ITexture* texture, int x, int y, int width, int height)
{
    // Placeholder for texture drawing
    // Full implementation requires:
    // - Vertex/fragment shaders
    // - Pipeline state setup
    // - Vertex buffer for quad geometry
    // - Texture sampling
    // This will be completed when integrating with Avalonia UI
    (void)texture;
    (void)x;
    (void)y;
    (void)width;
    (void)height;
}

void MetalDeviceCommandContext::EndFrame()
{
    if (_impl->commandBuffer)
    {
        [_impl->commandBuffer commit];
        [_impl->commandBuffer waitUntilCompleted];
        _impl->commandBuffer = nil;
    }
}

void MetalDeviceCommandContext::Flush()
{
    if (_impl->commandBuffer)
    {
        [_impl->commandBuffer commit];
        [_impl->commandBuffer waitUntilCompleted];
    }
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // __APPLE__
