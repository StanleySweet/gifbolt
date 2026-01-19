// <copyright file="ImageBehavior.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Runtime.InteropServices;
using Avalonia;
using GifBolt;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Diagnostics;

namespace GifBolt.Avalonia
{
    /// <summary>
    /// Attached properties for animating GIFs on standard Avalonia Image controls.
    /// Provides drop-in replacement compatibility with WpfAnimatedGif library.
    /// Cross-platform support for Windows, macOS, and Linux.
    /// </summary>
    public static class ImageBehavior
    {
        /// <summary>
        /// Defines the minimum frame delay in milliseconds for GIF playback.
        /// </summary>
        public static readonly AttachedProperty<int> MinFrameDelayMsProperty =
            AvaloniaProperty.RegisterAttached<Image, int>(
                "MinFrameDelayMs",
                typeof(ImageBehavior),
                defaultValue: 0);

        /// <summary>
        /// Gets the minimum frame delay in milliseconds for GIF playback.
        /// </summary>
        /// <param name="image">The Image control to query.</param>
        /// <returns>The minimum frame delay in milliseconds.</returns>
        public static int GetMinFrameDelayMs(Image image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(MinFrameDelayMsProperty);
        }

        /// <summary>
        /// Sets the minimum frame delay in milliseconds for GIF playback.
        /// </summary>
        /// <param name="image">The Image control to configure.</param>
        /// <param name="value">The minimum frame delay in milliseconds.</param>
        public static void SetMinFrameDelayMs(Image image, int value)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(MinFrameDelayMsProperty, value);
        }
        #region AnimatedSource (compatible WpfAnimatedGif)

        /// <summary>
        /// Defines the AnimatedSource attached property.
        /// Gets or sets the animated GIF source (Uri or string).
        /// Supports <see cref="string"/> file paths and <see cref="Uri"/> references.
        /// </summary>
        public static readonly AttachedProperty<object?> AnimatedSourceProperty =
            AvaloniaProperty.RegisterAttached<Image, object?>(
                "AnimatedSource",
                typeof(ImageBehavior),
                defaultValue: null);

        /// <summary>
        /// Gets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the animated source from.</param>
        /// <returns>The animated source value, or null if not set.</returns>
        public static object? GetAnimatedSource(Image image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(AnimatedSourceProperty);
        }

        /// <summary>
        /// Sets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the animated source on.</param>
        /// <param name="value">The animated source value (string path or Uri).</param>
        public static void SetAnimatedSource(Image image, object? value)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(AnimatedSourceProperty, value);
        }

        #endregion

        #region RepeatBehavior (compatible WpfAnimatedGif)

        /// <summary>
        /// Defines the RepeatBehavior attached property.
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly AttachedProperty<string> RepeatBehaviorProperty =
            AvaloniaProperty.RegisterAttached<Image, string>(
                "RepeatBehavior",
                typeof(ImageBehavior),
                defaultValue: "0x");

        /// <summary>
        /// Gets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the repeat behavior from.</param>
        /// <returns>The repeat behavior string.</returns>
        public static string GetRepeatBehavior(Image image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(RepeatBehaviorProperty);
        }

        /// <summary>
        /// Sets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the repeat behavior on.</param>
        /// <param name="value">The repeat behavior string.</param>
        public static void SetRepeatBehavior(Image image, string value)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(RepeatBehaviorProperty, value);
        }

        #endregion

        #region AutoStart

        /// <summary>
        /// Defines the AutoStart attached property.
        /// Gets or sets whether animation starts automatically upon loading.
        /// </summary>
        public static readonly AttachedProperty<bool> AutoStartProperty =
            AvaloniaProperty.RegisterAttached<Image, bool>(
                "AutoStart",
                typeof(ImageBehavior),
                defaultValue: true);

        /// <summary>
        /// Gets whether auto-start is enabled for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to query.</param>
        /// <returns>true if animation starts automatically; otherwise false.</returns>
        public static bool GetAutoStart(Image image)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(AutoStartProperty);
        }

        /// <summary>
        /// Sets whether animation starts automatically for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to configure.</param>
        /// <param name="value">true to start animation automatically; otherwise false.</param>
        public static void SetAutoStart(Image image, bool value)
        {
            if (image is null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(AutoStartProperty, value);
        }

        #endregion

        #region AnimationController (internal state)

        private static readonly AttachedProperty<GifAnimationController?> _animationControllerProperty =
            AvaloniaProperty.RegisterAttached<Image, GifAnimationController?>(
                "AnimationController",
                typeof(ImageBehavior),
                defaultValue: null);

        private static GifAnimationController? GetAnimationController(Image image) =>
            image.GetValue(_animationControllerProperty);

        private static void SetAnimationController(Image image, GifAnimationController? value) =>
            image.SetValue(_animationControllerProperty, value);

        #endregion

        static ImageBehavior()
        {
            AnimatedSourceProperty.Changed.AddClassHandler<Image>(OnAnimatedSourceChanged);
            RepeatBehaviorProperty.Changed.AddClassHandler<Image>(OnRepeatBehaviorChanged);
            MinFrameDelayMsProperty.Changed.AddClassHandler<Image>(OnMinFrameDelayMsChanged);
        }

        private static void OnMinFrameDelayMsChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null && e.NewValue is int minDelayMs)
            {
                controller.SetMinFrameDelayMs(minDelayMs);
            }
        }

        private static void OnAnimatedSourceChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null)
            {
                controller.Dispose();
                SetAnimationController(image, null);
                image.DetachedFromVisualTree -= OnImageDetached;
                image.AttachedToVisualTree -= OnImageAttached;
            }

            if (e.NewValue == null)
            {
                return;
            }

            string? path = GetPathFromSource(e.NewValue);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            GifAnimationController? asyncController = null;
            asyncController = new GifAnimationController(image, path,
                onLoaded: () =>
                {
                    SetAnimationController(image, asyncController!);

                    var repeatBehavior = GetRepeatBehavior(image);
                    asyncController!.SetRepeatBehavior(repeatBehavior);

                    int minDelayMs = GetMinFrameDelayMs(image);
                    if (minDelayMs > 0)
                    {
                        asyncController!.SetMinFrameDelayMs(minDelayMs);
                    }

                    if (GetAutoStart(image))
                    {
                        asyncController!.Play();
                    }

                    if (image != null)
                    {
                        image.DetachedFromVisualTree += OnImageDetached;
                        image.AttachedToVisualTree += OnImageAttached;
                    }
                },
                onError: (ex) =>
                {
                    Debug.WriteLine($"[GifBolt] ERROR: Failed to load GIF: {ex.Message}");
                    Debug.WriteLine($"[GifBolt] Stack trace: {ex.StackTrace}");
                });
        }

        private static void OnRepeatBehaviorChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            var controller = GetAnimationController(image);
            if (controller != null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        private static void OnImageAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Image image)
            {
                var controller = GetAnimationController(image);
                if (controller != null && GetAutoStart(image))
                {
                    controller.Play();
                }
            }
        }

        private static void OnImageDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Image image)
            {
                var controller = GetAnimationController(image);
                if (controller != null)
                {
                    controller.Pause();
                }
            }
        }

        private static string? GetPathFromSource(object source)
        {
            if (source is string str)
            {
                // Handle Avalonia asset paths (avares://)
                if (str.StartsWith("/Assets/") || str.StartsWith("Assets/"))
                {
                    // Try to resolve as application asset
                    var assemblyName = typeof(ImageBehavior).Assembly.GetName().Name;
                    var assetUri = new Uri($"avares://{assemblyName}{(str.StartsWith("/") ? "" : "/")}{str}");
                    try
                    {
                        var assets = global::Avalonia.Platform.AssetLoader.Open(assetUri);
                        // For assets, we need to copy to temp file since native decoder needs file path
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gifbolt_{Guid.NewGuid()}.gif");
                        using (var fileStream = System.IO.File.Create(tempPath))
                        {
                            assets.CopyTo(fileStream);
                        }
                        Debug.WriteLine($"[GifBolt] Asset extracted to: {tempPath}");
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GifBolt] Failed to load asset: {ex.Message}");
                        return null;
                    }
                }
                // Try as absolute path or expand relative path
                if (System.IO.Path.IsPathRooted(str))
                {
                    return str;
                }
                // Try relative to current directory
                var fullPath = System.IO.Path.GetFullPath(str);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
                return str;
            }

            if (source is Uri uri)
            {
                if (uri.Scheme == "avares")
                {
                    // Extract Avalonia asset to temp file
                    try
                    {
                        var assets = global::Avalonia.Platform.AssetLoader.Open(uri);
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gifbolt_{Guid.NewGuid()}.gif");
                        using (var fileStream = System.IO.File.Create(tempPath))
                        {
                            assets.CopyTo(fileStream);
                        }
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GifBolt] Failed to load asset URI: {ex.Message}");
                        return null;
                    }
                }
                return uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
            }

            return null;
        }
    }
}
