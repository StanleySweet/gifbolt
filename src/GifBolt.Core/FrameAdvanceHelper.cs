// <copyright file="FrameAdvanceHelper.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using GifBolt.Internal;

namespace GifBolt
{
    /// <summary>
    /// Helper for managing GIF frame advancement and animation state.
    /// Provides reusable logic for frame iteration, repeat counting, and timing.
    /// Delegates to C++ implementations for performance.
    /// </summary>
    public static class FrameAdvanceHelper
    {
        /// <summary>
        /// Represents the result of a frame advance operation (stack-allocated value type).
        /// </summary>
        public readonly struct FrameAdvanceResult
        {
            /// <summary>
            /// Gets the next frame index.
            /// </summary>
            public int NextFrame { get; }

            /// <summary>
            /// Gets a value indicating whether the animation has completed (no more frames to display).
            /// </summary>
            public bool IsComplete { get; }

            /// <summary>
            /// Gets the updated repeat count (decremented if needed).
            /// </summary>
            public int UpdatedRepeatCount { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="FrameAdvanceResult"/> struct.
            /// </summary>
            /// <param name="nextFrame">The next frame index.</param>
            /// <param name="isComplete">Whether the animation is complete.</param>
            /// <param name="updatedRepeatCount">The updated repeat count.</param>
            public FrameAdvanceResult(int nextFrame, bool isComplete, int updatedRepeatCount)
            {
                this.NextFrame = nextFrame;
                this.IsComplete = isComplete;
                this.UpdatedRepeatCount = updatedRepeatCount;
            }
        }

        /// <summary>
        /// Advances to the next frame in a GIF animation.
        /// Implementation delegates to C++ for performance.
        /// </summary>
        /// <param name="currentFrame">The current frame index (0-based).</param>
        /// <param name="frameCount">The total number of frames in the GIF.</param>
        /// <param name="repeatCount">The current repeat count (-1 = infinite, 0 = stop, >0 = repeat N times).</param>
        /// <returns>A <see cref="FrameAdvanceResult"/> containing the next frame and updated state.</returns>
        /// <exception cref="ArgumentException">Thrown if frameCount is less than 1.</exception>
        public static FrameAdvanceResult AdvanceFrame(int currentFrame, int frameCount, int repeatCount)
        {
            if (frameCount < 1)
            {
                throw new ArgumentException("frameCount must be at least 1", nameof(frameCount));
            }

            // Call C++ implementation for platform-agnostic frame advancement
            var nativeResult = Native.gb_decoder_advance_frame(currentFrame, frameCount, repeatCount);
            return new FrameAdvanceResult(nativeResult.NextFrame, nativeResult.IsComplete != 0, nativeResult.UpdatedRepeatCount);
        }

        /// <summary>
        /// Calculates the effective frame delay, applying a minimum threshold.
        /// Implementation delegates to C++ for consistency.
        /// </summary>
        /// <param name="frameDelayMs">The frame delay from GIF metadata (in milliseconds).</param>
        /// <param name="minDelayMs">The minimum frame delay to enforce (in milliseconds).</param>
        /// <returns>The effective frame delay in milliseconds.</returns>
        public static int GetEffectiveFrameDelay(int frameDelayMs, int minDelayMs = 0)
        {
            return Native.gb_decoder_get_effective_frame_delay(frameDelayMs, minDelayMs);
        }
    }
}
