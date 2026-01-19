// <copyright file="AnimationBehavior.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using Avalonia;
using Avalonia.Controls;

namespace GifBolt.Avalonia
{
    /// <summary>
    /// Attached properties for animating GIFs on standard Avalonia Image controls.
    /// 100% API-compatible with WPF AnimationBehavior for cross-platform consistency.
    /// </summary>
    /// <remarks>
    /// This class provides the same attached behavior pattern as the WPF version,
    /// allowing GIF animation on Avalonia Image controls with identical API.
    /// </remarks>
    public sealed class AnimationBehavior
    {
        /// <summary>
        /// Gets or sets the animated GIF source URI.
        /// Supports file paths and Uri references.
        /// </summary>
        public static readonly AttachedProperty<string?> SourceUriProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, string?>(
                "SourceUri",
                defaultValue: null);

        /// <summary>
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly AttachedProperty<string?> RepeatBehaviorProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, string?>(
                "RepeatBehavior",
                defaultValue: "0x");

        /// <summary>
        /// Gets or sets whether animation starts in design mode.
        /// </summary>
        public static readonly AttachedProperty<bool> AnimateInDesignModeProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, bool>(
                "AnimateInDesignMode",
                defaultValue: false);

        /// <summary>
        /// Internal property to store the GifAnimationController instance.
        /// </summary>
        private static readonly AttachedProperty<GifAnimationController?> _animationControllerProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, GifAnimationController?>(
                "AnimationController",
                defaultValue: null);

        static AnimationBehavior()
        {
            SourceUriProperty.Changed.AddClassHandler<Image>(OnSourceUriChanged);
            RepeatBehaviorProperty.Changed.AddClassHandler<Image>(OnRepeatBehaviorChanged);
        }

        /// <summary>
        /// Gets the value of the SourceUri attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>The source URI value.</returns>
        public static string? GetSourceUri(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(SourceUriProperty);
        }

        /// <summary>
        /// Sets the value of the SourceUri attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">The source URI to set.</param>
        public static void SetSourceUri(Image image, string? value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(SourceUriProperty, value);
        }

        /// <summary>
        /// Gets the value of the RepeatBehavior attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>The repeat behavior value.</returns>
        public static string? GetRepeatBehavior(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(RepeatBehaviorProperty);
        }

        /// <summary>
        /// Sets the value of the RepeatBehavior attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">The repeat behavior to set.</param>
        public static void SetRepeatBehavior(Image image, string? value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(RepeatBehaviorProperty, value);
        }

        /// <summary>
        /// Gets the value of the AnimateInDesignMode attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>Whether animation should occur in design mode.</returns>
        public static bool GetAnimateInDesignMode(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(AnimateInDesignModeProperty);
        }

        /// <summary>
        /// Sets the value of the AnimateInDesignMode attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">Whether to animate in design mode.</param>
        public static void SetAnimateInDesignMode(Image image, bool value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(AnimateInDesignModeProperty, value);
        }

        // =============================================
        // Private Implementation
        // =============================================

        private static GifAnimationController? GetAnimationController(Image image)
        {
            return image.GetValue(_animationControllerProperty);
        }

        private static void SetAnimationController(Image image, GifAnimationController? value)
        {
            image.SetValue(_animationControllerProperty, value);
        }

        /// <summary>
        /// Handles changes to the SourceUri property.
        /// </summary>
        private static void OnSourceUriChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            // Dispose existing controller if any
            var existingController = GetAnimationController(image);
            if (existingController != null)
            {
                existingController.Stop();
                SetAnimationController(image, null);
                System.Threading.Tasks.Task.Run(() => existingController.Dispose());
            }

            // Null source = stop animation
            if (e.NewValue == null)
            {
                return;
            }

            var sourceUri = e.NewValue as string;
            if (string.IsNullOrWhiteSpace(sourceUri))
            {
                return;
            }

            // Resolve the source path
            string? resolvedPath = ResolveSourceUri(sourceUri);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return;
            }

            // Create new animation controller
            var repeatBehavior = GetRepeatBehavior(image) ?? "0x";
            var controller = new GifAnimationController(
                image,
                resolvedPath!,
                onLoaded: () =>
                {
                    // Verify controller is still current
                    var current = GetAnimationController(image);
                    if (current == null)
                    {
                        return;
                    }

                    // Apply repeat behavior
                    if (!string.IsNullOrWhiteSpace(repeatBehavior))
                    {
                        current.SetRepeatBehavior(repeatBehavior);
                    }

                    // Auto-start (default for API compatibility)
                    try
                    {
                        current.Play();
                    }
                    catch
                    {
                        // Suppress errors during autostart
                    }
                },
                onError: (ex) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[GifBolt] Error loading GIF '{sourceUri}': {ex.Message}");
                });

            SetAnimationController(image, controller);
            image.Unloaded += OnImageUnloaded;
        }

        /// <summary>
        /// Handles changes to the RepeatBehavior property.
        /// </summary>
        private static void OnRepeatBehaviorChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller is not null && e.NewValue is string repeatBehavior)
            {
                controller.SetRepeatBehavior(repeatBehavior);
            }
        }

        /// <summary>
        /// Handles image unloaded event.
        /// </summary>
        private static void OnImageUnloaded(object? sender, EventArgs e)
        {
            if (sender is not Image image)
            {
                return;
            }

            image.Unloaded -= OnImageUnloaded;
            var controller = GetAnimationController(image);
            if (controller is not null)
            {
                controller.Dispose();
                SetAnimationController(image, null);
            }
        }

        /// <summary>
        /// Resolves a source URI to an absolute file path.
        /// </summary>
        private static string? ResolveSourceUri(string sourceUri)
        {
            if (string.IsNullOrWhiteSpace(sourceUri))
            {
                return null;
            }

            // Already a rooted path
            if (System.IO.Path.IsPathRooted(sourceUri))
            {
                return sourceUri;
            }

            // Try relative to application directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = System.IO.Path.Combine(appDir, sourceUri);
            if (System.IO.File.Exists(fullPath))
            {
                return fullPath;
            }

            // Try as-is (might be a relative path that works)
            if (System.IO.File.Exists(sourceUri))
            {
                return sourceUri;
            }

            // Could not resolve
            return null;
        }
    }
}
