// <copyright file="StretchHelper.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;

namespace GifBolt
{
    /// <summary>
    /// Provides stretch/fill calculations for image rendering in different contexts.
    /// </summary>
    public static class StretchHelper
    {
        /// <summary>
        /// Represents a stretch/fill mode.
        /// </summary>
        public enum StretchMode
        {
            /// <summary>Do not stretch; render at original size.</summary>
            None = 0,

            /// <summary>Stretch to fit the available space (may distort).</summary>
            Fill = 1,

            /// <summary>Stretch to fit while maintaining aspect ratio; fill bounds.</summary>
            Uniform = 2,

            /// <summary>Stretch to fit while maintaining aspect ratio; fit within bounds.</summary>
            UniformToFill = 3,
        }

        /// <summary>
        /// Calculates the destination rectangle for rendering an image given source and destination bounds.
        /// </summary>
        /// <param name="sourceWidth">The width of the source image in pixels.</param>
        /// <param name="sourceHeight">The height of the source image in pixels.</param>
        /// <param name="destWidth">The width of the destination area in pixels.</param>
        /// <param name="destHeight">The height of the destination area in pixels.</param>
        /// <param name="stretch">The stretch mode to apply.</param>
        /// <returns>A tuple of (x, y, width, height) representing the destination rectangle.</returns>
        public static (int x, int y, int width, int height) CalculateDestinationRect(
            int sourceWidth,
            int sourceHeight,
            int destWidth,
            int destHeight,
            StretchMode stretch = StretchMode.Uniform)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || destWidth <= 0 || destHeight <= 0)
            {
                return (0, 0, destWidth, destHeight);
            }

            return stretch switch
            {
                StretchMode.None => (0, 0, sourceWidth, sourceHeight),
                StretchMode.Fill => (0, 0, destWidth, destHeight),
                StretchMode.Uniform => CalculateUniform(sourceWidth, sourceHeight, destWidth, destHeight),
                StretchMode.UniformToFill => CalculateUniformToFill(sourceWidth, sourceHeight, destWidth, destHeight),
                _ => (0, 0, destWidth, destHeight),
            };
        }

        private static (int x, int y, int width, int height) CalculateUniform(
            int sourceWidth,
            int sourceHeight,
            int destWidth,
            int destHeight)
        {
            double sourceAspect = (double)sourceWidth / sourceHeight;
            double destAspect = (double)destWidth / destHeight;

            int width, height;
            if (sourceAspect > destAspect)
            {
                // Source is wider; fit to width
                width = destWidth;
                height = (int)Math.Round(destWidth / sourceAspect);
            }
            else
            {
                // Source is taller; fit to height
                height = destHeight;
                width = (int)Math.Round(destHeight * sourceAspect);
            }

            int x = (destWidth - width) / 2;
            int y = (destHeight - height) / 2;
            return (x, y, width, height);
        }

        private static (int x, int y, int width, int height) CalculateUniformToFill(
            int sourceWidth,
            int sourceHeight,
            int destWidth,
            int destHeight)
        {
            double sourceAspect = (double)sourceWidth / sourceHeight;
            double destAspect = (double)destWidth / destHeight;

            int width, height;
            if (sourceAspect > destAspect)
            {
                // Source is wider; fit to height
                height = destHeight;
                width = (int)Math.Round(destHeight * sourceAspect);
            }
            else
            {
                // Source is taller; fit to width
                width = destWidth;
                height = (int)Math.Round(destWidth / sourceAspect);
            }

            int x = (destWidth - width) / 2;
            int y = (destHeight - height) / 2;
            return (x, y, width, height);
        }
    }
}
