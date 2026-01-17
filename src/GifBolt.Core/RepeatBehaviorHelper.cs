// SPDX-License-Identifier: MIT
using System;

namespace GifBolt
{
    /// <summary>
    /// Provides helper methods to compute GIF repeat behavior consistently across frameworks.
    /// </summary>
    public static class RepeatBehaviorHelper
    {
        /// <summary>
        /// Computes the repeat count from a repeat behavior string.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string (e.g., "Forever", "3x", "0x").</param>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping.</param>
        /// <returns>
        /// <c>-1</c> for infinite repeat, a positive integer for finite repeats,
        /// or <c>1</c> when looping is disabled.
        /// </returns>
        public static int ComputeRepeatCount(string repeatBehavior, bool isLooping)
        {
            if (string.IsNullOrWhiteSpace(repeatBehavior) || repeatBehavior == "0x")
            {
                return isLooping ? -1 : 1;
            }

            if (repeatBehavior.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            if (repeatBehavior.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var countStr = repeatBehavior.Substring(0, repeatBehavior.Length - 1);
                if (int.TryParse(countStr, out int count) && count > 0)
                {
                    return count;
                }
            }

            return isLooping ? -1 : 1;
        }
    }
}
