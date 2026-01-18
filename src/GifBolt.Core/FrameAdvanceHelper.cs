// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;

namespace GifBolt
{
    /// <summary>
    /// Helper for managing GIF frame advancement and animation state.
    /// Provides reusable logic for frame iteration, repeat counting, and timing.
    /// </summary>
    public static class FrameAdvanceHelper
    {
        /// <summary>
        /// Represents the result of a frame advance operation.
        /// </summary>
        public sealed class FrameAdvanceResult
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
        /// Initializes a new instance of the <see cref="FrameAdvanceResult"/> class.
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

            int nextFrame = currentFrame + 1;

            // Check if we've reached the end of the frame sequence
            if (nextFrame >= frameCount)
            {
                // Determine if we should loop
                if (repeatCount == -1)
                {
                    // Infinite loop
                    return new FrameAdvanceResult(nextFrame: 0, isComplete: false, updatedRepeatCount: -1);
                }
                else if (repeatCount > 0)
                {
                    // Finite repeats remaining
                    return new FrameAdvanceResult(nextFrame: 0, isComplete: false, updatedRepeatCount: repeatCount - 1);
                }
                else
                {
                    // No more repeats; animation is complete
                    return new FrameAdvanceResult(nextFrame: currentFrame, isComplete: true, updatedRepeatCount: 0);
                }
            }

            // Normal frame advance within the sequence
            return new FrameAdvanceResult(nextFrame: nextFrame, isComplete: false, updatedRepeatCount: repeatCount);
        }

        /// <summary>
        /// Calculates the effective frame delay, applying a minimum threshold.
        /// </summary>
        /// <param name="frameDelayMs">The frame delay from GIF metadata (in milliseconds).</param>
        /// <param name="minDelayMs">The minimum frame delay to enforce (in milliseconds).</param>
        /// <returns>The effective frame delay in milliseconds.</returns>
        public static int GetEffectiveFrameDelay(int frameDelayMs, int minDelayMs = 0)
        {
            if (frameDelayMs < minDelayMs)
            {
                return minDelayMs;
            }

            return frameDelayMs;
        }
    }
}
