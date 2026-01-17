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

    PixelFormats::Format GetFormat() const override
    {
        return PixelFormats::Format::R8G8B8A8_UNORM;
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
    id<MTLComputePipelineState> conversionPipeline;
    id<MTLLibrary> shaderLibrary;
    bool computeShaderReady;

    Impl()
        : device(nil), commandQueue(nil), commandBuffer(nil), renderEncoder(nil),
          pipelineState(nil), renderPassDescriptor(nil), conversionPipeline(nil),
          shaderLibrary(nil), computeShaderReady(false)
    {
    }

    ~Impl()
    {
        if (conversionPipeline)
        {
            [conversionPipeline release];
        }
        if (shaderLibrary)
        {
            [shaderLibrary release];
        }
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

    bool InitializeComputeShader();
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

    // Initialize compute shader for GPU-accelerated pixel conversion
    _impl->computeShaderReady = _impl->InitializeComputeShader();

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

bool MetalDeviceCommandContext::Impl::InitializeComputeShader()
{
    // Inline Metal shader source
    NSString* shaderSource = @R"(
        #include <metal_stdlib>
        using namespace metal;

        kernel void ConvertRGBAToBGRAPremultiplied(
            constant uint* inputRGBA [[buffer(0)]],
            device uint* outputBGRA [[buffer(1)]],
            constant uint& pixelCount [[buffer(2)]],
            uint idx [[thread_position_in_grid]])
        {
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

    NSError* error = nil;
    this->shaderLibrary = [this->device newLibraryWithSource:shaderSource
                                                      options:nil
                                                        error:&error];

    if (!this->shaderLibrary || error)
    {
        return false;
    }

    id<MTLFunction> kernelFunction = [this->shaderLibrary newFunctionWithName:@"ConvertRGBAToBGRAPremultiplied"];
    if (!kernelFunction)
    {
        return false;
    }

    this->conversionPipeline = [this->device newComputePipelineStateWithFunction:kernelFunction
                                                                            error:&error];
    [kernelFunction release];

    return (this->conversionPipeline != nil && !error);
}

bool MetalDeviceCommandContext::ConvertRGBAToBGRAPremultipliedGPU(const void* inputRGBA,
                                                                   void* outputBGRA,
                                                                   uint32_t pixelCount)
{
    if (!_impl || !_impl->computeShaderReady || !_impl->device || !_impl->commandQueue)
    {
        return false;
    }

    const uint32_t byteCount = pixelCount * 4;

    // Create input buffer
    id<MTLBuffer> inputBuffer = [_impl->device newBufferWithBytes:inputRGBA
                                                           length:byteCount
                                                          options:MTLResourceStorageModeShared];
    if (!inputBuffer)
    {
        return false;
    }

    // Create output buffer
    id<MTLBuffer> outputBuffer = [_impl->device newBufferWithLength:byteCount
                                                             options:MTLResourceStorageModeShared];
    if (!outputBuffer)
    {
        [inputBuffer release];
        return false;
    }

    // Create parameter buffer
    id<MTLBuffer> paramBuffer = [_impl->device newBufferWithBytes:&pixelCount
                                                           length:sizeof(uint32_t)
                                                          options:MTLResourceStorageModeShared];
    if (!paramBuffer)
    {
        [inputBuffer release];
        [outputBuffer release];
        return false;
    }

    // Create command buffer
    id<MTLCommandBuffer> commandBuffer = [_impl->commandQueue commandBuffer];
    if (!commandBuffer)
    {
        [inputBuffer release];
        [outputBuffer release];
        [paramBuffer release];
        return false;
    }

    // Create compute encoder
    id<MTLComputeCommandEncoder> computeEncoder = [commandBuffer computeCommandEncoder];
    [computeEncoder setComputePipelineState:_impl->conversionPipeline];
    [computeEncoder setBuffer:inputBuffer offset:0 atIndex:0];
    [computeEncoder setBuffer:outputBuffer offset:0 atIndex:1];
    [computeEncoder setBuffer:paramBuffer offset:0 atIndex:2];

    // Dispatch threads
    MTLSize threadgroupSize = MTLSizeMake(256, 1, 1);
    MTLSize threadgroups = MTLSizeMake((pixelCount + 255) / 256, 1, 1);
    [computeEncoder dispatchThreadgroups:threadgroups threadsPerThreadgroup:threadgroupSize];

    [computeEncoder endEncoding];
    [commandBuffer commit];
    [commandBuffer waitUntilCompleted];

    // Copy result to output
    memcpy(outputBGRA, [outputBuffer contents], byteCount);

    // Cleanup
    [inputBuffer release];
    [outputBuffer release];
    [paramBuffer release];

    return true;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // __APPLE__
