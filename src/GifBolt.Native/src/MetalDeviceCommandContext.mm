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
    id<MTLComputePipelineState> scalingNearestPipeline;
    id<MTLComputePipelineState> scalingBilinearPipeline;
    id<MTLComputePipelineState> scalingBicubicPipeline;
    id<MTLComputePipelineState> scalingLanczosPipeline;
    id<MTLLibrary> shaderLibrary;
    id<MTLLibrary> scalingLibrary;
    bool computeShaderReady;
    bool scalingShadersReady;

    Impl()
        : device(nil), commandQueue(nil), commandBuffer(nil), renderEncoder(nil),
          pipelineState(nil), renderPassDescriptor(nil), conversionPipeline(nil),
          scalingNearestPipeline(nil), scalingBilinearPipeline(nil),
          scalingBicubicPipeline(nil), scalingLanczosPipeline(nil),
          shaderLibrary(nil), scalingLibrary(nil),
          computeShaderReady(false), scalingShadersReady(false)
    {
    }

    ~Impl()
    {
        if (scalingLanczosPipeline)
        {
            [scalingLanczosPipeline release];
        }
        if (scalingBicubicPipeline)
        {
            [scalingBicubicPipeline release];
        }
        if (scalingBilinearPipeline)
        {
            [scalingBilinearPipeline release];
        }
        if (scalingNearestPipeline)
        {
            [scalingNearestPipeline release];
        }
        if (scalingLibrary)
        {
            [scalingLibrary release];
        }
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
    bool InitializeScalingShaders();
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

bool MetalDeviceCommandContext::Impl::InitializeScalingShaders()
{
    NSError* error = nil;

    // Always use runtime compilation of shaders from embedded source
    // This is more portable and doesn't require pre-compiled metallib files

    // Compile shaders from embedded source code
    NSString* shaderSource = @R"(
            #include <metal_stdlib>
            using namespace metal;

            kernel void scaleNearest(
                texture2d<float, access::read> inputTexture [[texture(0)]],
                texture2d<float, access::write> outputTexture [[texture(1)]],
                uint2 gid [[thread_position_in_grid]])
            {
                const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
                const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

                if (gid.x >= outputSize.x || gid.y >= outputSize.y) return;

                const float xRatio = float(inputSize.x) / float(outputSize.x);
                const float yRatio = float(inputSize.y) / float(outputSize.y);
                const uint2 srcPos = uint2(uint(gid.x * xRatio), uint(gid.y * yRatio));
                const float4 color = inputTexture.read(srcPos);
                outputTexture.write(color, gid);
            }

            kernel void scaleBilinear(
                texture2d<float, access::read> inputTexture [[texture(0)]],
                texture2d<float, access::write> outputTexture [[texture(1)]],
                uint2 gid [[thread_position_in_grid]])
            {
                const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
                const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

                if (gid.x >= outputSize.x || gid.y >= outputSize.y) return;

                const float xRatio = float(inputSize.x) / float(outputSize.x);
                const float yRatio = float(inputSize.y) / float(outputSize.y);
                const float srcX = float(gid.x) * xRatio;
                const float srcY = float(gid.y) * yRatio;

                const uint x0 = uint(srcX);
                const uint y0 = uint(srcY);
                const uint x1 = min(x0 + 1, inputSize.x - 1);
                const uint y1 = min(y0 + 1, inputSize.y - 1);
                const float fracX = srcX - float(x0);
                const float fracY = srcY - float(y0);

                const float4 c00 = inputTexture.read(uint2(x0, y0));
                const float4 c10 = inputTexture.read(uint2(x1, y0));
                const float4 c01 = inputTexture.read(uint2(x0, y1));
                const float4 c11 = inputTexture.read(uint2(x1, y1));

                const float4 cTop = mix(c00, c10, fracX);
                const float4 cBottom = mix(c01, c11, fracX);
                const float4 result = mix(cTop, cBottom, fracY);

                outputTexture.write(result, gid);
            }

            float cubicWeight(float x)
            {
                const float a = -0.5;
                const float absX = abs(x);
                if (absX <= 1.0) {
                    return ((a + 2.0) * absX - (a + 3.0)) * absX * absX + 1.0;
                } else if (absX < 2.0) {
                    return ((a * absX - 5.0 * a) * absX + 8.0 * a) * absX - 4.0 * a;
                }
                return 0.0;
            }

            kernel void scaleBicubic(
                texture2d<float, access::read> inputTexture [[texture(0)]],
                texture2d<float, access::write> outputTexture [[texture(1)]],
                uint2 gid [[thread_position_in_grid]])
            {
                const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
                const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

                if (gid.x >= outputSize.x || gid.y >= outputSize.y) return;

                const float xRatio = float(inputSize.x) / float(outputSize.x);
                const float yRatio = float(inputSize.y) / float(outputSize.y);
                const float srcX = float(gid.x) * xRatio;
                const float srcY = float(gid.y) * yRatio;
                const int x = int(srcX);
                const int y = int(srcY);
                const float dx = srcX - float(x);
                const float dy = srcY - float(y);

                float4 result = float4(0.0);
                float weightSum = 0.0;
                for (int j = -1; j <= 2; ++j) {
                    for (int i = -1; i <= 2; ++i) {
                        const int sx = clamp(x + i, 0, int(inputSize.x) - 1);
                        const int sy = clamp(y + j, 0, int(inputSize.y) - 1);
                        const float wx = cubicWeight(float(i) - dx);
                        const float wy = cubicWeight(float(j) - dy);
                        const float weight = wx * wy;
                        const float4 sample = inputTexture.read(uint2(sx, sy));
                        result += sample * weight;
                        weightSum += weight;
                    }
                }
                if (weightSum > 0.0) result /= weightSum;
                outputTexture.write(result, gid);
            }

            float lanczosWeight(float x, float a)
            {
                if (abs(x) < 0.001) return 1.0;
                if (abs(x) >= a) return 0.0;
                const float pi = 3.14159265359;
                const float piX = pi * x;
                return a * sin(piX) * sin(piX / a) / (piX * piX);
            }

            kernel void scaleLanczos(
                texture2d<float, access::read> inputTexture [[texture(0)]],
                texture2d<float, access::write> outputTexture [[texture(1)]],
                uint2 gid [[thread_position_in_grid]])
            {
                const uint2 outputSize = uint2(outputTexture.get_width(), outputTexture.get_height());
                const uint2 inputSize = uint2(inputTexture.get_width(), inputTexture.get_height());

                if (gid.x >= outputSize.x || gid.y >= outputSize.y) return;

                const float a = 3.0;
                const float xRatio = float(inputSize.x) / float(outputSize.x);
                const float yRatio = float(inputSize.y) / float(outputSize.y);
                const float srcX = float(gid.x) * xRatio;
                const float srcY = float(gid.y) * yRatio;
                const int x = int(srcX);
                const int y = int(srcY);
                const float dx = srcX - float(x);
                const float dy = srcY - float(y);

                float4 result = float4(0.0);
                float weightSum = 0.0;
                const int radius = int(ceil(a));
                for (int j = -radius; j <= radius; ++j) {
                    for (int i = -radius; i <= radius; ++i) {
                        const int sx = clamp(x + i, 0, int(inputSize.x) - 1);
                        const int sy = clamp(y + j, 0, int(inputSize.y) - 1);
                        const float wx = lanczosWeight(float(i) - dx, a);
                        const float wy = lanczosWeight(float(j) - dy, a);
                        const float weight = wx * wy;
                        const float4 sample = inputTexture.read(uint2(sx, sy));
                        result += sample * weight;
                        weightSum += weight;
                    }
                }
                if (weightSum > 0.0) result /= weightSum;
                outputTexture.write(result, gid);
            }
        )";

    this->scalingLibrary = [this->device newLibraryWithSource:shaderSource
                                                       options:nil
                                                         error:&error];

    if (!this->scalingLibrary || error)
    {
        NSLog(@"Failed to compile Metal shaders: %@", error);
        return false;
    }

    // Create pipeline states for each filter
    id<MTLFunction> nearestFunc = [this->scalingLibrary newFunctionWithName:@"scaleNearest"];
    id<MTLFunction> bilinearFunc = [this->scalingLibrary newFunctionWithName:@"scaleBilinear"];
    id<MTLFunction> bicubicFunc = [this->scalingLibrary newFunctionWithName:@"scaleBicubic"];
    id<MTLFunction> lanczosFunc = [this->scalingLibrary newFunctionWithName:@"scaleLanczos"];

    if (nearestFunc)
    {
        this->scalingNearestPipeline = [this->device newComputePipelineStateWithFunction:nearestFunc error:&error];
        [nearestFunc release];
    }

    if (bilinearFunc)
    {
        this->scalingBilinearPipeline = [this->device newComputePipelineStateWithFunction:bilinearFunc error:&error];
        [bilinearFunc release];
    }

    if (bicubicFunc)
    {
        this->scalingBicubicPipeline = [this->device newComputePipelineStateWithFunction:bicubicFunc error:&error];
        [bicubicFunc release];
    }

    if (lanczosFunc)
    {
        this->scalingLanczosPipeline = [this->device newComputePipelineStateWithFunction:lanczosFunc error:&error];
        [lanczosFunc release];
    }

    return (this->scalingNearestPipeline != nil && this->scalingBilinearPipeline != nil);
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

    // Optimize threadgroup size for Metal
    // Use maxTotalThreadsPerThreadgroup for better occupancy
    NSUInteger maxThreadsPerGroup = [_impl->conversionPipeline maxTotalThreadsPerThreadgroup];
    NSUInteger optimalThreads = std::min(maxThreadsPerGroup, static_cast<NSUInteger>(1024));

    MTLSize threadgroupSize = MTLSizeMake(optimalThreads, 1, 1);
    MTLSize threadgroups = MTLSizeMake((pixelCount + optimalThreads - 1) / optimalThreads, 1, 1);
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

bool MetalDeviceCommandContext::ScaleImageGPU(const void* inputBGRA, uint32_t inputWidth,
                                               uint32_t inputHeight, void* outputBGRA,
                                               uint32_t outputWidth, uint32_t outputHeight,
                                               int filterType)
{
    if (!_impl || !_impl->device || !_impl->commandQueue)
    {
        return false;
    }

    // Initialize scaling shaders if not already done
    if (!_impl->scalingShadersReady)
    {
        _impl->scalingShadersReady = _impl->InitializeScalingShaders();
        if (!_impl->scalingShadersReady)
        {
            return false;
        }
    }

    // Select pipeline based on filter type
    id<MTLComputePipelineState> pipeline = nil;
    switch (filterType)
    {
        case 0: // Nearest
            pipeline = _impl->scalingNearestPipeline;
            break;
        case 1: // Bilinear
            pipeline = _impl->scalingBilinearPipeline;
            break;
        case 2: // Bicubic
            pipeline = _impl->scalingBicubicPipeline;
            break;
        case 3: // Lanczos
            pipeline = _impl->scalingLanczosPipeline;
            break;
        default:
            pipeline = _impl->scalingBilinearPipeline;
            break;
    }

    if (!pipeline)
    {
        return false;
    }

    // Create input texture
    MTLTextureDescriptor* inputDesc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm
                                                                                          width:inputWidth
                                                                                         height:inputHeight
                                                                                      mipmapped:NO];
    inputDesc.usage = MTLTextureUsageShaderRead;
    inputDesc.storageMode = MTLStorageModeManaged;

    id<MTLTexture> inputTexture = [_impl->device newTextureWithDescriptor:inputDesc];
    if (!inputTexture)
    {
        return false;
    }

    MTLRegion inputRegion = MTLRegionMake2D(0, 0, inputWidth, inputHeight);
    [inputTexture replaceRegion:inputRegion mipmapLevel:0 withBytes:inputBGRA bytesPerRow:inputWidth * 4];

    // Create output texture
    MTLTextureDescriptor* outputDesc = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:MTLPixelFormatBGRA8Unorm
                                                                                           width:outputWidth
                                                                                          height:outputHeight
                                                                                       mipmapped:NO];
    outputDesc.usage = MTLTextureUsageShaderWrite;
    outputDesc.storageMode = MTLStorageModeManaged;

    id<MTLTexture> outputTexture = [_impl->device newTextureWithDescriptor:outputDesc];
    if (!outputTexture)
    {
        [inputTexture release];
        return false;
    }

    // Create command buffer
    id<MTLCommandBuffer> commandBuffer = [_impl->commandQueue commandBuffer];
    if (!commandBuffer)
    {
        [inputTexture release];
        [outputTexture release];
        return false;
    }

    // Create compute encoder
    id<MTLComputeCommandEncoder> computeEncoder = [commandBuffer computeCommandEncoder];
    [computeEncoder setComputePipelineState:pipeline];
    [computeEncoder setTexture:inputTexture atIndex:0];
    [computeEncoder setTexture:outputTexture atIndex:1];

    // Dispatch threads
    MTLSize threadgroupSize = MTLSizeMake(8, 8, 1);
    MTLSize threadgroups = MTLSizeMake(
        (outputWidth + 7) / 8,
        (outputHeight + 7) / 8,
        1
    );
    [computeEncoder dispatchThreadgroups:threadgroups threadsPerThreadgroup:threadgroupSize];

    [computeEncoder endEncoding];
    [commandBuffer commit];
    [commandBuffer waitUntilCompleted];

    // Copy result to output
    MTLRegion outputRegion = MTLRegionMake2D(0, 0, outputWidth, outputHeight);
    [outputTexture getBytes:outputBGRA
                bytesPerRow:outputWidth * 4
                 fromRegion:outputRegion
                mipmapLevel:0];

    // Cleanup
    [inputTexture release];
    [outputTexture release];

    return true;
}

}  // namespace Renderer
}  // namespace GifBolt

#endif  // __APPLE__
