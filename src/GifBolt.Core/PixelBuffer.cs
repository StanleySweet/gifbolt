// <copyright file="PixelBuffer.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;

namespace GifBolt
{
    /// <summary>
    /// Represents a reference-counted pixel buffer from the native decoder.
    /// Provides safe, zero-copy access to pixel data without intermediate allocations.
    /// </summary>
    /// <remarks>
    /// This struct manages the lifetime of a native C++ PixelBuffer.
    /// Always dispose when finished to avoid memory leaks.
    /// The buffer is reference-counted, so multiple C# objects can safely share ownership.
    /// </remarks>
    public struct PixelBuffer : IDisposable
    {
        /// <summary>
        /// Opaque handle to the native pixel buffer.
        /// </summary>
        private IntPtr _handle;

        /// <summary>
        /// Gets the size of the pixel buffer in bytes.
        /// </summary>
        public int SizeInBytes { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelBuffer"/> struct.
        /// </summary>
        /// <param name="handle">The native pixel buffer handle.</param>
        /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
        internal PixelBuffer(IntPtr handle, int sizeInBytes)
        {
            this._handle = handle;
            this.SizeInBytes = sizeInBytes;
        }

        /// <summary>
        /// Gets a value indicating whether this buffer is valid.
        /// </summary>
        public bool IsValid => this._handle != IntPtr.Zero;

        /// <summary>
        /// Adds a reference to the pixel buffer.
        /// </summary>
        /// <remarks>This is a no-op in the current implementation.</remarks>
        public void AddRef()
        {
            if (this._handle != IntPtr.Zero)
            {
                // No-op with current design (managed by C# GC)
                Internal.Native.gb_pixel_buffer_add_ref(this._handle);
            }
        }

        /// <summary>
        /// Releases the reference to the pixel buffer.
        /// </summary>
        public void Dispose()
        {
            if (this._handle != IntPtr.Zero)
            {
                Internal.Native.gb_pixel_buffer_release(this._handle);
                this._handle = IntPtr.Zero;
                this.SizeInBytes = 0;
            }
        }

        /// <summary>
        /// Copies the pixel buffer data to a managed byte array.
        /// </summary>
        /// <returns>A byte array containing a copy of the pixel data.</returns>
        public byte[] ToArray()
        {
            if (this._handle == IntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[this.SizeInBytes];
            IntPtr dataPtr = Internal.Native.gb_pixel_buffer_get_data(this._handle);
            if (dataPtr != IntPtr.Zero)
            {
                Marshal.Copy(dataPtr, result, 0, this.SizeInBytes);
            }

            return result;
        }
    }
}
