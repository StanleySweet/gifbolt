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
        /// The default minimum frame delay in milliseconds (10 ms).
        /// Most GIFs are created with delays of 10-100ms; we use 10ms as a reasonable
        /// minimum to prevent GIFs with very small delays from playing too fast,
        /// while still allowing fast animations to play at reasonable speeds.
        /// </summary>
        public const int DefaultMinFrameDelayMs = 10;

        /// <summary>
        /// The minimum render interval for the UI thread timer (16 ms = 60 FPS).
        /// This is the fastest the UI can be updated while staying responsive.
        /// </summary>
        public const int MinRenderIntervalMs = 16;
    }
}
