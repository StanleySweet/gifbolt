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
        /// Valid values: "Forever" (default), "3x" (repeat N times), "0x" (use GIF metadata).
        /// </summary>
        public static readonly AttachedProperty<string?> RepeatBehaviorProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, string?>(
                "RepeatBehavior",
                defaultValue: "Forever");

        /// <summary>
        /// Gets or sets whether animation starts in design mode.
        /// </summary>
        public static readonly AttachedProperty<bool> AnimateInDesignModeProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, bool>(
                "AnimateInDesignMode",
                defaultValue: false);

        /// <summary>
        /// Gets or sets the scaling filter used when resizing GIF frames.
        /// Valid values: None (default, native resolution), Nearest, Bilinear, Bicubic, Lanczos (highest quality).
        /// </summary>
        public static readonly AttachedProperty<ScalingFilter> ScalingFilterProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, ScalingFilter>(
                "ScalingFilter",
                defaultValue: ScalingFilter.None);

        /// <summary>
        /// Internal property to store the GifAnimationController instance.
        /// </summary>
        private static readonly AttachedProperty<GifAnimationController?> _animationControllerProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, GifAnimationController?>(
                "AnimationController",
                defaultValue: null);

        /// <summary>
        /// FPS text for display/debugging.
        /// </summary>
        public static readonly AttachedProperty<string?> FpsTextProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, string?>(
                "FpsText",
                defaultValue: "FPS: --");

        /// <summary>
        /// Gets or sets whether animation should start automatically.
        /// Default is true for API compatibility with WPF XamlAnimatedGif.
        /// </summary>
        public static readonly AttachedProperty<bool> AutoStartProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, bool>(
                "AutoStart",
                defaultValue: true);

        /// <summary>
        /// Gets or sets whether to cache decoded frames in memory.
        /// Useful for animations that are played multiple times.
        /// </summary>
        public static readonly AttachedProperty<bool> CacheFramesInMemoryProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, bool>(
                "CacheFramesInMemory",
                defaultValue: false);

        /// <summary>
        /// Gets or sets the source stream for animation.
        /// Alternative to SourceUri for stream-based loading.
        /// </summary>
        public static readonly AttachedProperty<System.IO.Stream?> SourceStreamProperty =
            AvaloniaProperty.RegisterAttached<AnimationBehavior, Image, System.IO.Stream?>(
                "SourceStream",
                defaultValue: null);

        static AnimationBehavior()
        {
            SourceUriProperty.Changed.AddClassHandler<Image>(OnSourceUriChanged);
            SourceStreamProperty.Changed.AddClassHandler<Image>(OnSourceStreamChanged);
            RepeatBehaviorProperty.Changed.AddClassHandler<Image>(OnRepeatBehaviorChanged);
            ScalingFilterProperty.Changed.AddClassHandler<Image>(OnScalingFilterChanged);
            AutoStartProperty.Changed.AddClassHandler<Image>(OnAutoStartChanged);
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

        /// <summary>
        /// Gets the FPS text attached property value.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>The FPS text.</returns>
        public static string? GetFpsText(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(FpsTextProperty);
        }

        /// <summary>
        /// Sets the FPS text attached property value.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">The FPS text.</param>
        public static void SetFpsText(Image image, string? value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(FpsTextProperty, value);
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

            return image.GetValue(ScalingFilterProperty);
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
        /// Gets the value of the AutoStart attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>Whether animation starts automatically.</returns>
        public static bool GetAutoStart(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(AutoStartProperty);
        }

        /// <summary>
        /// Sets the value of the AutoStart attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">Whether to start animation automatically.</param>
        public static void SetAutoStart(Image image, bool value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(AutoStartProperty, value);
        }

        /// <summary>
        /// Gets the value of the CacheFramesInMemory attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>Whether frames are cached in memory.</returns>
        public static bool GetCacheFramesInMemory(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(CacheFramesInMemoryProperty);
        }

        /// <summary>
        /// Sets the value of the CacheFramesInMemory attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">Whether to cache decoded frames.</param>
        public static void SetCacheFramesInMemory(Image image, bool value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(CacheFramesInMemoryProperty, value);
        }

        /// <summary>
        /// Gets the value of the SourceStream attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <returns>The source stream value.</returns>
        public static System.IO.Stream? GetSourceStream(Image image)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            return image.GetValue(SourceStreamProperty);
        }

        /// <summary>
        /// Sets the value of the SourceStream attached property.
        /// </summary>
        /// <param name="image">The Image control.</param>
        /// <param name="value">The source stream to set.</param>
        public static void SetSourceStream(Image image, System.IO.Stream? value)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            image.SetValue(SourceStreamProperty, value);
        }

        // =============================================
        // Private Implementation
        // =============================================

        /// <summary>
        /// Gets the animation controller currently managing the specified Image control.
        /// </summary>
        /// <remarks>
        /// Returns null if no controller is currently attached or if the image has not been loaded with a GIF source.
        /// </remarks>
        /// <param name="image">The Image control to query.</param>
        /// <returns>The GifAnimationController managing this image, or null if none is attached.</returns>
        public static GifAnimationController? GetAnimationController(Image image)
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
            var repeatBehavior = GetRepeatBehavior(image) ?? "Forever";
            var scalingFilter = GetScalingFilter(image);
            var autoStart = GetAutoStart(image);
            var cacheFrames = GetCacheFramesInMemory(image);
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

                    // Apply scaling filter
                    current.SetScalingFilter(scalingFilter);

                    // Apply repeat behavior
                    if (!string.IsNullOrWhiteSpace(repeatBehavior))
                    {
                        current.SetRepeatBehavior(repeatBehavior);
                    }

                    // Auto-start if enabled
                    if (autoStart)
                    {
                        try
                        {
                            current.Play();
                        }
                        catch
                        {
                            // Suppress errors during autostart
                        }
                    }
                },
                onError: (ex) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[GifBolt] Error loading GIF '{sourceUri}': {ex.Message}");
                },
                cacheFrames: cacheFrames);

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
        /// Handles changes to the ScalingFilter property.
        /// </summary>
        private static void OnScalingFilterChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            if (image == null)
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
        /// Handles changes to the SourceStream property.
        /// </summary>
        private static void OnSourceStreamChanged(Image image, AvaloniaPropertyChangedEventArgs e)
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

            var sourceStream = e.NewValue as System.IO.Stream;
            if (sourceStream == null)
            {
                return;
            }

            // Load from stream by writing to a temporary file
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // Create a temporary file for the GIF
                    string tempFile = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"gifbolt_{System.Guid.NewGuid().ToString().Substring(0, 8)}.gif");

                    // Write stream to temp file
                    sourceStream.Seek(0, System.IO.SeekOrigin.Begin);
                    using (var fileStream = System.IO.File.Create(tempFile))
                    {
                        sourceStream.CopyTo(fileStream);
                    }

                    // Now load from the temp file
                    var repeatBehavior = GetRepeatBehavior(image) ?? "Forever";
                    var scalingFilter = GetScalingFilter(image);
                    var autoStart = GetAutoStart(image);
                    var cacheFrames = GetCacheFramesInMemory(image);

                    var controller = new GifAnimationController(
                        image,
                        tempFile,
                        onLoaded: () =>
                        {
                            // Verify controller is still current
                            var current = GetAnimationController(image);
                            if (current == null)
                            {
                                System.IO.File.Delete(tempFile);
                                return;
                            }

                            current.SetScalingFilter(scalingFilter);
                            if (!string.IsNullOrWhiteSpace(repeatBehavior))
                            {
                                current.SetRepeatBehavior(repeatBehavior);
                            }

                            if (autoStart)
                            {
                                try
                                {
                                    current.Play();
                                }
                                catch
                                {
                                    // Suppress errors
                                }
                            }

                            // Clean up temp file after a delay (allow time for reading)
                            System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ =>
                            {
                                try
                                {
                                    if (System.IO.File.Exists(tempFile))
                                    {
                                        System.IO.File.Delete(tempFile);
                                    }
                                }
                                catch
                                {
                                    // Ignore cleanup errors
                                }
                            });
                        },
                        onError: (ex) =>
                        {
                            System.Diagnostics.Debug.WriteLine($"[GifBolt] Error loading GIF from stream: {ex.Message}");
                            // Clean up temp file
                            try
                            {
                                if (System.IO.File.Exists(tempFile))
                                {
                                    System.IO.File.Delete(tempFile);
                                }
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                        },
                        cacheFrames: cacheFrames);

                    SetAnimationController(image, controller);
                    image.Unloaded += OnImageUnloaded;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GifBolt] Error processing GIF stream: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Handles changes to the AutoStart property.
        /// </summary>
        private static void OnAutoStartChanged(Image image, AvaloniaPropertyChangedEventArgs e)
        {
            if (image == null)
            {
                return;
            }

            var controller = GetAnimationController(image);
            if (controller is not null && e.NewValue is bool autoStart)
            {
                if (autoStart)
                {
                    controller.Play();
                }
                else
                {
                    controller.Stop();
                }
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
