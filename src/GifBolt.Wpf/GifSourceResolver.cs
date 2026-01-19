// <copyright file="GifSourceResolver.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Resolves GIF sources into either file paths or in-memory byte buffers.
    /// Centralizes pack URI handling for WPF resources.
    /// </summary>
    internal static class GifSourceResolver
    {
        public static bool TryResolve(object? source, out byte[]? bytes, out string? path)
        {
            bytes = null;
            path = null;

            switch (source)
            {
                case null:
                    return false;
                case string stringSource:
                    return TryResolveString(stringSource, out bytes, out path);
                case Uri uriSource:
                    return TryResolveUri(uriSource, out bytes, out path);
                case BitmapImage bitmapSource when bitmapSource.UriSource != null:
                    return TryResolveUri(bitmapSource.UriSource, out bytes, out path);
                default:
                    return false;
            }
        }

        private static bool TryResolveString(string source, out byte[]? bytes, out string? path)
        {
            bytes = null;
            path = null;

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            if (source.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            {
                return TryLoadPackUriBytes(source, out bytes);
            }

            path = source;
            return true;
        }

        private static bool TryResolveUri(Uri uri, out byte[]? bytes, out string? path)
        {
            bytes = null;
            path = null;

            if (string.Equals(uri.Scheme, "pack", StringComparison.OrdinalIgnoreCase))
            {
                return TryLoadPackUriBytes(uri.ToString(), out bytes);
            }

            path = uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
            return true;
        }

        private static bool TryLoadPackUriBytes(string uriString, out byte[]? bytes)
        {
            bytes = null;

            try
            {
                if (!Uri.TryCreate(uriString, UriKind.Absolute, out Uri? uri) || uri == null)
                {
                    return false;
                }

                var streamInfo = Application.GetResourceStream(uri);
                if (streamInfo == null || streamInfo.Stream == null)
                {
                    return false;
                }

                using (streamInfo.Stream)
                using (var memoryStream = new MemoryStream())
                {
                    streamInfo.Stream.CopyTo(memoryStream);
                    bytes = memoryStream.ToArray();
                    return bytes.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
