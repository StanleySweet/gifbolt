// <copyright file="IRepeatStrategy.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

namespace GifBolt
{
    /// <summary>
    /// Defines the strategy for determining GIF animation repeat behavior.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface encapsulate different repeat policies,
    /// such as looping forever, repeating a specific number of times, or using
    /// the GIF's metadata.
    /// </remarks>
    public interface IRepeatStrategy
    {
        /// <summary>
        /// Computes the repeat count for the animation.
        /// </summary>
        /// <param name="isLooping">Whether the GIF metadata indicates infinite looping.</param>
        /// <returns>
        /// <c>-1</c> for infinite repeat, or a positive integer for finite repeats.
        /// </returns>
        int GetRepeatCount(bool isLooping);
    }
}
