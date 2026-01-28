using GifBolt.Internal;

namespace GifBolt
{
    /// <summary>
    /// Provides helper methods to compute GIF repeat behavior consistently across frameworks.
    /// Delegates to C++ implementation for performance and consistency.
    /// </summary>
    /// <remarks>
    /// This class serves as a facade over the Strategy pattern implementation for backwards compatibility.
    /// New code should use <see cref="RepeatStrategyFactory"/> and <see cref="IRepeatStrategy"/> directly.
    /// </remarks>
    public static class RepeatBehaviorHelper
    {
        /// <summary>
        /// Computes the repeat count from a repeat behavior string.
        /// Delegates to C++ implementation for platform-consistent behavior.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string (e.g., "Forever", "3x", "0x").</param>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping.</param>
        /// <returns>
        /// <c>-1</c> for infinite repeat, a positive integer for finite repeats,
        /// or <c>1</c> when looping is disabled.
        /// </returns>
        public static int ComputeRepeatCount(string repeatBehavior, bool isLooping)
        {
            return Native.gb_decoder_compute_repeat_count(repeatBehavior, isLooping ? 1 : 0);
        }
    }
}
