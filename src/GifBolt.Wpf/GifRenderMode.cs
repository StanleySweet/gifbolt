// <copyright file="GifRenderMode.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt.Wpf
{
    /// <summary>
    /// Specifies the rendering mode for GIF animations in WPF.
    /// </summary>
    public enum GifRenderMode
    {
        /// <summary>
        /// Software rendering using WriteableBitmap with CPU-to-GPU memory copies.
        /// </summary>
        /// <remarks>
        /// Compatible with all WPF versions and hardware.
        /// Good performance for most scenarios.
        /// Requires CPU memory copy for each frame.
        /// </remarks>
        WriteableBitmap = 0,

        /// <summary>
        /// GPU-accelerated rendering using D3DImage with direct DirectX interop.
        /// </summary>
        /// <remarks>
        /// Requires Windows Vista or later with DirectX 9Ex/11 compatible hardware.
        /// Best performance - eliminates CPU-to-GPU memory copies.
        /// DirectX texture is rendered directly into WPF's composition pipeline.
        /// May have compatibility issues on older systems or virtual machines.
        /// </remarks>
        D3DImage = 1,
    }
}
