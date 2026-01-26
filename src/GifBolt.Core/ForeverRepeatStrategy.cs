// <copyright file="ForeverRepeatStrategy.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt
{
    /// <summary>
    /// Repeat strategy that loops the animation infinitely.
    /// </summary>
    /// <remarks>
    /// This strategy always returns -1, indicating infinite loop regardless
    /// of the GIF's metadata.
    /// </remarks>
    public sealed class ForeverRepeatStrategy : IRepeatStrategy
    {
        /// <summary>
        /// Gets the singleton instance of the forever repeat strategy.
        /// </summary>
        public static ForeverRepeatStrategy Instance { get; } = new ForeverRepeatStrategy();

        private ForeverRepeatStrategy()
        {
        }

        /// <summary>
        /// Computes the repeat count for the animation.
        /// </summary>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping (ignored).</param>
        /// <returns>Always returns <c>-1</c> for infinite repeat.</returns>
        public int GetRepeatCount(bool isLooping)
        {
            return -1;
        }
    }
}
