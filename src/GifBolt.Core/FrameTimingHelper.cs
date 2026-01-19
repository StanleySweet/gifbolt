// <copyright file="FrameTimingHelper.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt
{
    /// <summary>
    /// Provides standard timing constants and helpers for GIF animation playback.
    /// </summary>
    public static class FrameTimingHelper
    {
        /// <summary>
        /// The default minimum frame delay in milliseconds (100 ms).
        /// Matches Chrome, macOS, and ezgif standards to prevent GIFs from playing too fast.
        /// </summary>
        public const int DefaultMinFrameDelayMs = 100;

        /// <summary>
        /// The minimum render interval in milliseconds for UI frameworks (16 ms = ~60 FPS).
        /// </summary>
        public const int MinRenderIntervalMs = 16;
    }
}
