// <copyright file="DecoderHandle.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;

namespace GifBolt.Internal
{
    /// <summary>
    /// Wraps a native decoder handle from the GifBolt.Native library.
    /// Implements <see cref="SafeHandle"/> for safe cleanup of unmanaged resources.
    /// </summary>
    internal sealed class DecoderHandle : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DecoderHandle"/> class with an invalid handle.
        /// </summary>
        public DecoderHandle() : base(IntPtr.Zero, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DecoderHandle"/> class with an existing native handle.
        /// </summary>
        /// <param name="existing">The existing native decoder handle to wrap.</param>
        public DecoderHandle(IntPtr existing) : base(IntPtr.Zero, true)
        {
            this.SetHandle(existing);
        }

        /// <summary>
        /// Gets a value indicating whether the native handle is invalid.
        /// </summary>
        public override bool IsInvalid => this.handle == IntPtr.Zero;

        /// <summary>
        /// Releases the unmanaged native decoder handle.
        /// </summary>
        /// <returns>true if the handle was released successfully; otherwise false.</returns>
        protected override bool ReleaseHandle()
        {
            if (!this.IsInvalid)
            {
                Native.gb_decoder_destroy(this.handle);
                this.SetHandle(IntPtr.Zero);
            }
            return true;
        }
    }
}
