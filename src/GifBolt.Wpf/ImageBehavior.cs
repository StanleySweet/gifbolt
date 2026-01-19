// <copyright file="ImageBehavior.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GifBolt;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Attached properties for animating GIFs on standard WPF Image controls.
    /// Provides drop-in replacement compatibility with WpfAnimatedGif library.
    /// </summary>
    public static class ImageBehavior
    {
        /// <summary>
        /// Gets or sets the animated GIF source (Uri or string).
        /// Supports <see cref="string"/> file paths and <see cref="Uri"/> references.
        /// </summary>
        public static readonly DependencyProperty AnimatedSourceProperty =
            DependencyProperty.RegisterAttached(
                "AnimatedSource",
                typeof(object),
                typeof(ImageBehavior),
                new PropertyMetadata(null, OnAnimatedSourceChanged));

        /// <summary>
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly DependencyProperty RepeatBehaviorProperty =
            DependencyProperty.RegisterAttached(
                "RepeatBehavior",
                typeof(string),
                typeof(ImageBehavior),
                new PropertyMetadata("0x", OnRepeatBehaviorChanged));

        /// <summary>
        /// Gets or sets whether animation starts automatically upon loading.
        /// </summary>
        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.RegisterAttached(
                "AutoStart",
                typeof(bool),
                typeof(ImageBehavior),
                new PropertyMetadata(true));

        private static readonly DependencyProperty _animationControllerProperty =
            DependencyProperty.RegisterAttached(
                "AnimationController",
                typeof(GifAnimationController),
                typeof(ImageBehavior),
                new PropertyMetadata(null));

        /// <summary>
        /// Gets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the animated source from.</param>
        /// <returns>The animated source value, or null if not set.</returns>
        public static object GetAnimatedSource(Image image)
        {
            return image.GetValue(AnimatedSourceProperty);
        }

        /// <summary>
        /// Sets the animated source for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the animated source on.</param>
        /// <param name="value">The animated source value (string path or Uri).</param>
        public static void SetAnimatedSource(Image image, object value)
        {
            image.SetValue(AnimatedSourceProperty, value);
        }

        /// <summary>
        /// Gets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to get the repeat behavior from.</param>
        /// <returns>The repeat behavior string.</returns>
        public static string GetRepeatBehavior(Image image)
        {
            return (string)image.GetValue(RepeatBehaviorProperty);
        }

        /// <summary>
        /// Sets the repeat behavior for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to set the repeat behavior on.</param>
        /// <param name="value">The repeat behavior string.</param>
        public static void SetRepeatBehavior(Image image, string value)
        {
            image.SetValue(RepeatBehaviorProperty, value);
        }

        /// <summary>
        /// Gets whether auto-start is enabled for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to query.</param>
        /// <returns>true if animation starts automatically; otherwise false.</returns>
        public static bool GetAutoStart(Image image)
        {
            return (bool)image.GetValue(AutoStartProperty);
        }

        /// <summary>
        /// Sets whether animation starts automatically for the specified Image control.
        /// </summary>
        /// <param name="image">The Image control to configure.</param>
        /// <param name="value">true to start animation automatically; otherwise false.</param>
        public static void SetAutoStart(Image image, bool value)
        {
            image.SetValue(AutoStartProperty, value);
        }

        private static GifAnimationController? GetAnimationController(Image image)
        {
            return (GifAnimationController?)image.GetValue(_animationControllerProperty);
        }

        private static void SetAnimationController(Image image, GifAnimationController? value)
        {
            image.SetValue(_animationControllerProperty, value);
        }

        private static void OnAnimatedSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            var existingController = GetAnimationController(image);
            if (existingController != null)
            {
                // Stop the old controller immediately to prevent multiple GIFs playing
                existingController.Stop();
                SetAnimationController(image, (GifAnimationController?)null);
                // Dispose resources asynchronously to avoid blocking UI thread
                System.Threading.Tasks.Task.Run(() => existingController.Dispose());
            }

            if (e.NewValue == null)
            {
                return;
            }

            if (!GifSourceResolver.TryResolve(e.NewValue, out byte[]? sourceBytes, out string? path))
            {
                return;
            }

            // Create animation controller with async loading and error callbacks
            var repeatBehavior = GetRepeatBehavior(image);
            var initialAutoStart = GetAutoStart(image); // Capture the current AutoStart value

            var controller = sourceBytes != null
                ? new GifAnimationController(image, sourceBytes, onLoaded: () =>
                {
                    var currentController = GetAnimationController(image);
                    if (currentController == null)
                    {
                        return;
                    }

                    if (initialAutoStart)
                    {
                        try
                        {
                            currentController.Play();
                        }
                        catch
                        {
                        }
                    }
                },
                onError: (ex) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading GIF: {ex.Message}");
                })
                : new GifAnimationController(
                    image,
                    path!,
                onLoaded: () =>
                {
                    // Verify this controller is still current
                    var currentController = GetAnimationController(image);
                    if (currentController == null)
                    {
                        return; // Controller was replaced
                    }

                    // Only auto-start if AutoStart was true when the GIF was LOADED
                    // Don't use the current AutoStart value, use the initial one
                    if (initialAutoStart)
                    {
                        try
                        {
                            currentController.Play();
                        }
                        catch
                        {
                            // Swallow errors
                        }
                    }
                },
                    onError: (ex) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading GIF: {ex.Message}");
                    });

            SetAnimationController(image, controller);
            controller.SetRepeatBehavior(repeatBehavior);

            // Only subscribe to Unloaded once per Image control
            if (existingController == null)
            {
                image.Unloaded += OnImageUnloaded;
            }
        }

        private static void OnRepeatBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller is not null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        private static void OnImageUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Image image)
            {
                return;
            }

            image.Unloaded -= OnImageUnloaded;
            var controller = GetAnimationController(image);
            if (controller is not null)
            {
                SetAnimationController(image, null);
                // Dispose asynchronously to avoid blocking during cleanup
                System.Threading.Tasks.Task.Run(() => controller.Dispose());
            }
        }

        private static string? GetPathFromSource(object source)
        {
            if (source is string str)
            {
                return str;
            }

            if (source is Uri uri)
            {
                return uri.IsAbsoluteUri ? uri.LocalPath : uri.ToString();
            }

            if (source is BitmapImage bitmap && bitmap.UriSource != null)
            {
                return bitmap.UriSource.IsAbsoluteUri ? bitmap.UriSource.LocalPath : bitmap.UriSource.ToString();
            }

            return null;
        }

        /// <summary>
        /// Loads bytes from a pack:// embedded resource.
        /// </summary>
        private static byte[]? LoadPackUriBytes(string packUri)
        {
            try
            {
                if (!Uri.TryCreate(packUri, UriKind.Absolute, out Uri? uri))
                {
                    return null;
                }

                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo == null || streamInfo.Stream == null)
                {
                    return null;
                }

                using (var stream = streamInfo.Stream)
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GifBolt.ImageBehavior] Failed to load pack URI '{packUri}': {ex.Message}");
                return null;
            }
        }
    }
}
