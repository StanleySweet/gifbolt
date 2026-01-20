// <copyright file="AnimationBehavior.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Windows;
using System.Windows.Controls;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Attached properties for animating GIFs on standard WPF Image controls.
    /// 100% API-compatible replacement for XamlAnimatedGif AnimationBehavior.
    /// </summary>
    /// <remarks>
    /// This class provides drop-in replacement for XamlAnimatedGif, allowing
    /// GPU-accelerated GIF rendering while maintaining identical API.
    /// </remarks>
    public static class AnimationBehavior
    {
        /// <summary>
        /// Gets or sets the animated GIF source URI.
        /// Supports file paths and Uri references. 100% XamlAnimatedGif compatible.
        /// </summary>
        public static readonly DependencyProperty SourceUriProperty =
            DependencyProperty.RegisterAttached(
                "SourceUri",
                typeof(string),
                typeof(AnimationBehavior),
                new PropertyMetadata(null, OnSourceUriChanged));

        /// <summary>
        /// Gets or sets the repeat behavior for the animation.
        /// Valid values: "Forever", "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly DependencyProperty RepeatBehaviorProperty =
            DependencyProperty.RegisterAttached(
                "RepeatBehavior",
                typeof(string),
                typeof(AnimationBehavior),
                new PropertyMetadata(null, OnRepeatBehaviorChanged));

        /// <summary>
        /// Gets or sets whether animation starts in design mode.
        /// </summary>
        public static readonly DependencyProperty AnimateInDesignModeProperty =
            DependencyProperty.RegisterAttached(
                "AnimateInDesignMode",
                typeof(bool),
                typeof(AnimationBehavior),
                new PropertyMetadata(false, OnAnimateInDesignModeChanged));

        /// <summary>
        /// Gets or sets the scaling filter used when resizing GIF frames.
        /// Valid values: None (default, native resolution), Nearest, Bilinear, Bicubic, Lanczos (highest quality).
        /// </summary>
        public static readonly DependencyProperty ScalingFilterProperty =
            DependencyProperty.RegisterAttached(
                "ScalingFilter",
                typeof(ScalingFilter),
                typeof(AnimationBehavior),
                new PropertyMetadata(ScalingFilter.None, OnScalingFilterChanged));

        /// <summary>
        /// Internal property to store the GifAnimationController instance.
        /// </summary>
        private static readonly DependencyProperty _animationControllerProperty =
            DependencyProperty.RegisterAttached(
                "AnimationController",
                typeof(GifAnimationController),
                typeof(AnimationBehavior),
                new PropertyMetadata(null));

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

            return (string?)image.GetValue(SourceUriProperty);
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

            return (string?)image.GetValue(RepeatBehaviorProperty);
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

            return (bool)image.GetValue(AnimateInDesignModeProperty);
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

        /// <summary>
        /// Gets the value of the ScalingFilter attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>The scaling filter value.</returns>
        public static ScalingFilter GetScalingFilter(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return (ScalingFilter)image.GetValue(ScalingFilterProperty);
        }

        /// <summary>
        /// Sets the value of the ScalingFilter attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">The scaling filter to set.</param>
        public static void SetScalingFilter(Image image, ScalingFilter value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(ScalingFilterProperty, value);
        }

        /// <summary>
        /// Private Implementation.
        /// </summary>
        private static GifAnimationController? GetAnimationController(Image image)
        {
            return (GifAnimationController?)image.GetValue(_animationControllerProperty);
        }

        private static void SetAnimationController(Image image, GifAnimationController? value)
        {
            image.SetValue(_animationControllerProperty, value);
        }

        /// <summary>
        /// Handles changes to the SourceUri property.
        /// Creates or updates the animation controller when the source changes.
        /// </summary>
        private static void OnSourceUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
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

            var sourceUri = e.NewValue;

            // Resolve the source (handles pack URIs, relative paths, BitmapImage, etc.)
            if (!GifSourceResolver.TryResolve(sourceUri, out byte[]? bytes, out string? resolvedPath))
            {
                return;
            }

            // Either bytes or path must be non-null
            if (bytes == null && string.IsNullOrWhiteSpace(resolvedPath))
            {
                return;
            }

            // Capture values for use in nested functions
            var repeatBehavior = GetRepeatBehavior(image) ?? "Forever";
            var scalingFilter = GetScalingFilter(image);

            // Create new animation controller
            var controller = bytes != null
                ? new GifAnimationController(image, bytes, onLoaded: OnControllerLoaded, onError: OnControllerError)
                : new GifAnimationController(image, resolvedPath!, onLoaded: OnControllerLoaded, onError: OnControllerError);

            void OnControllerLoaded()
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

                // Apply scaling filter
                current.SetScalingFilter(scalingFilter);

                // Auto-start (default for XamlAnimatedGif compatibility)
                try
                {
                    current.Play();
                }
                catch
                {
                    // Suppress errors during autostart
                }
            }

            void OnControllerError(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GifBolt] Error loading GIF '{sourceUri}': {ex.Message}");
            }

            SetAnimationController(image, controller);
            image.Unloaded += OnImageUnloaded;
        }

        /// <summary>
        /// Handles changes to the RepeatBehavior property.
        /// Updates the controller with the new repeat behavior.
        /// </summary>
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

        /// <summary>
        /// Handles changes to the AnimateInDesignMode property.
        /// Currently just triggers a source reload to apply the setting.
        /// </summary>
        private static void OnAnimateInDesignModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            // If we're in design mode and this was just enabled, reload the source
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(image) && (bool)e.NewValue)
            {
                var sourceUri = GetSourceUri(image);
                if (!string.IsNullOrWhiteSpace(sourceUri))
                {
                    // Re-trigger the source changed handler
                    OnSourceUriChanged(image, new DependencyPropertyChangedEventArgs(SourceUriProperty, null, sourceUri));
                }
            }
        }

        private static void OnScalingFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller is not null && e.NewValue is ScalingFilter filter)
            {
                controller.SetScalingFilter(filter);
            }
        }

        /// <summary>
        /// Handles image unloaded event.
        /// Disposes the animation controller when the image is removed from the visual tree.
        /// </summary>
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
                controller.Dispose();
                SetAnimationController(image, null);
            }
        }
    }
}
