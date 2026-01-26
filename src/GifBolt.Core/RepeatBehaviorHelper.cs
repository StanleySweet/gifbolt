// <copyright file="RepeatBehaviorHelper.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt
{
    /// <summary>
    /// Provides helper methods to compute GIF repeat behavior consistently across frameworks.
    /// </summary>
    /// <remarks>
    /// This class serves as a facade over the Strategy pattern implementation for backwards compatibility.
    /// New code should use <see cref="RepeatStrategyFactory"/> and <see cref="IRepeatStrategy"/> directly.
    /// </remarks>
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
            IRepeatStrategy strategy = RepeatStrategyFactory.CreateStrategy(repeatBehavior);
            return strategy.GetRepeatCount(isLooping);
        }
    }
}
