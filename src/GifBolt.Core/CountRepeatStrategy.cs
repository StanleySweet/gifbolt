// <copyright file="CountRepeatStrategy.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;

namespace GifBolt
{
    /// <summary>
    /// Repeat strategy that loops the animation a specific number of times.
    /// </summary>
    /// <remarks>
    /// This strategy returns the specified count regardless of the GIF's metadata.
    /// </remarks>
    public sealed class CountRepeatStrategy : IRepeatStrategy
    {
        private readonly int _count;

        /// <summary>
        /// Initializes a new instance of the <see cref="CountRepeatStrategy"/> class.
        /// </summary>
        /// <param name="count">The number of times to repeat the animation. Must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">When count is less than 1.</exception>
        public CountRepeatStrategy(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");
            }

            this._count = count;
        }

        /// <summary>
        /// Computes the repeat count for the animation.
        /// </summary>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping (ignored).</param>
        /// <returns>The specified repeat count.</returns>
        public int GetRepeatCount(bool isLooping)
        {
            return this._count;
        }
    }
}
