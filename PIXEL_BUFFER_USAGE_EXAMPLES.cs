using GifBolt.Core;
using System;

namespace GifBolt.Examples
{
    /// <summary>
    /// Example demonstrating the new zero-copy pixel buffer API.
    /// 
    /// The new TryGetFramePixels*Buffer() methods provide direct access
    /// to pixel data in native memory without unnecessary copying.
    /// </summary>
    public class PixelBufferUsageExample
    {
        private GifPlayer _player;

        /// <summary>
        /// Example 1: Access pixel data without copying
        /// Preferred for read-only access or streaming scenarios
        /// </summary>
        public void Example_ReadPixelsWithoutCopy()
        {
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var buffer))
            {
                using (buffer)
                {
                    // Access pixel data directly in native memory
                    IntPtr pixelData = buffer.Data;
                    int sizeBytes = buffer.SizeInBytes;
                    
                    // Example: Process pixels without copying
                    ProcessPixelData(pixelData, sizeBytes);
                }
            }
        }

        /// <summary>
        /// Example 2: Copy to managed array when needed
        /// Use when you need a byte[] for framework requirements
        /// </summary>
        public void Example_CopyToManagedArray()
        {
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var buffer))
            {
                using (buffer)
                {
                    // Convert to managed array (performs copy internally)
                    byte[] managedPixels = buffer.ToArray();
                    
                    // Now you can use managedPixels with managed libraries
                    DisplayImage(managedPixels);
                }
            }
        }

        /// <summary>
        /// Example 3: Scaled frame access
        /// Get pixels at a specific resolution
        /// </summary>
        public void Example_ScaledFrameAccess()
        {
            if (_player.TryGetFramePixelsBgra32PremultipliedScaledBuffer(
                frameIndex: 0,
                targetWidth: 800,
                targetHeight: 600,
                out var buffer,
                out int actualWidth,
                out int actualHeight,
                filter: ScalingFilter.Bilinear))
            {
                using (buffer)
                {
                    Console.WriteLine($"Scaled to {actualWidth}x{actualHeight}");
                    IntPtr pixelData = buffer.Data;
                    // Use scaled pixel data
                }
            }
        }

        /// <summary>
        /// Example 4: Performance comparison
        /// Old way (still works but allocates twice)
        /// </summary>
        public void Example_OldWayStillWorks()
        {
            // This still works but involves extra copying
            if (_player.TryGetFramePixelsBgra32Premultiplied(0, out byte[] pixels))
            {
                // pixels array already contains copied data
                DisplayImage(pixels);
            }
        }

        /// <summary>
        /// Example 5: RGBA32 format
        /// For APIs that require RGBA instead of BGRA
        /// </summary>
        public void Example_Rgba32Format()
        {
            if (_player.TryGetFramePixelsRgba32Buffer(0, out var buffer))
            {
                using (buffer)
                {
                    // Access RGBA pixel data
                    byte[] rgbaPixels = buffer.ToArray();
                    SendToRgbaApi(rgbaPixels);
                }
            }
        }

        /// <summary>
        /// Best practices for pixel buffer usage
        /// </summary>
        public void BestPractices()
        {
            // ✅ DO: Use 'using' statement to ensure proper cleanup
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var buffer))
            {
                using (buffer)
                {
                    ProcessBuffer(buffer);
                } // <- Automatic cleanup here
            }

            // ✅ DO: Check IsValid to exit early if needed
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var buffer2))
            {
                if (!buffer2.IsValid)
                {
                    return; // Buffer creation failed
                }
                using (buffer2)
                {
                    ProcessBuffer(buffer2);
                }
            }

            // ❌ DON'T: Forget to dispose
            /*
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var badBuffer))
            {
                // Native memory leaked if you don't dispose!
                // This will compile but leaks memory
            }
            */

            // ✅ DO: For one-off access, copy to array
            if (_player.TryGetFramePixelsBgra32PremultipliedBuffer(0, out var buffer3))
            {
                using (buffer3)
                {
                    byte[] pixels = buffer3.ToArray(); // Safe copy
                    SendToFramework(pixels);
                } // <- Cleanup happens here
            }
        }

        private void ProcessPixelData(IntPtr pixelData, int sizeBytes)
        {
            // Implement pixel processing
        }

        private void ProcessBuffer(PixelBuffer buffer)
        {
            // Implement buffer processing
        }

        private void DisplayImage(byte[] pixels)
        {
            // Implement image display
        }

        private void SendToRgbaApi(byte[] pixels)
        {
            // Send to API
        }

        private void SendToFramework(byte[] pixels)
        {
            // Send to framework
        }
    }
}
