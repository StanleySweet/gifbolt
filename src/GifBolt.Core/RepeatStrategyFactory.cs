// <copyright file="RepeatStrategyFactory.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;

namespace GifBolt
{
    /// <summary>
    /// Factory for creating repeat strategies from behavior strings.
    /// </summary>
    /// <remarks>
    /// Parses repeat behavior strings and creates the appropriate strategy instance.
    /// Supported formats: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
    /// </remarks>
    public static class RepeatStrategyFactory
    {
        /// <summary>
        /// Creates a repeat strategy from a behavior string.
        /// </summary>
        /// <param name="repeatBehavior">
        /// The repeat behavior string. Valid values:
        /// <list type="bullet">
        /// <item><description>"Forever" - loop infinitely</description></item>
        /// <item><description>"Nx" where N > 0 - repeat N times</description></item>
        /// <item><description>"0x" or null/empty - use GIF metadata</description></item>
        /// </list>
        /// </param>
        /// <returns>The appropriate repeat strategy instance.</returns>
        /// <exception cref="ArgumentException">When the behavior string has an invalid format.</exception>
        public static IRepeatStrategy CreateStrategy(string? repeatBehavior)
        {
            // Null, empty, or "0x" means use metadata
            if (string.IsNullOrWhiteSpace(repeatBehavior) || repeatBehavior == "0x")
            {
                return MetadataRepeatStrategy.Instance;
            }

            // "Forever" means infinite loop
            if (repeatBehavior.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                return ForeverRepeatStrategy.Instance;
            }

            // "Nx" format where N is the count
            if (repeatBehavior.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                var countStr = repeatBehavior.Substring(0, repeatBehavior.Length - 1);
                if (int.TryParse(countStr, out int count) && count > 0)
                {
                    return new CountRepeatStrategy(count);
                }
            }

            // If we can't parse it, default to metadata strategy
            return MetadataRepeatStrategy.Instance;
        }
    }
}
