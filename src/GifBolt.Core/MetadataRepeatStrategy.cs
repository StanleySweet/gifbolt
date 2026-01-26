// <copyright file="MetadataRepeatStrategy.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt
{
    /// <summary>
    /// Repeat strategy that uses the GIF's metadata to determine repeat behavior.
    /// </summary>
    /// <remarks>
    /// This strategy respects the loop count embedded in the GIF file's metadata.
    /// If the GIF is marked as looping, returns -1 (infinite), otherwise returns 1 (play once).
    /// </remarks>
    public sealed class MetadataRepeatStrategy : IRepeatStrategy
    {
        /// <summary>
        /// Gets the singleton instance of the metadata repeat strategy.
        /// </summary>
        public static MetadataRepeatStrategy Instance { get; } = new MetadataRepeatStrategy();

        private MetadataRepeatStrategy()
        {
        }

        /// <summary>
        /// Computes the repeat count for the animation.
        /// </summary>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping.</param>
        /// <returns><c>-1</c> if looping, <c>1</c> otherwise.</returns>
        public int GetRepeatCount(bool isLooping)
        {
            return isLooping ? -1 : 1;
        }
    }
}
