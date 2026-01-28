// <copyright file="GifAnimationControllerBase.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;

namespace GifBolt
{
    /// <summary>
    /// Base class for platform-specific GIF animation controllers.
    /// Provides minimal UI-framework-independent animation logic.
    /// Frame advancement and repeat count management has been moved to C++ for better performance.
    /// </summary>
    public abstract class GifAnimationControllerBase : IDisposable
    {
        /// <summary>
        /// Gets the GIF player instance.
        /// </summary>
        protected GifPlayer? Player
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the native animation context handle.
        /// Encapsulates frame advancement, repeat count, and playback state management.
        /// </summary>
        protected System.IntPtr AnimationContext { get; set; } = System.IntPtr.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationControllerBase"/> class.
        /// </summary>
        /// <remarks>
        /// Derived classes are responsible for initializing the Player property
        /// and creating the animation context after loading the GIF.
        /// </remarks>
        protected GifAnimationControllerBase()
        {
        }

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        public virtual void Play()
        {
            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Play();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, true, false);
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public virtual void Pause()
        {
            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Pause();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, false, false);
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public virtual void Stop()
        {
            if (this.Player == null || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            this.Player.Stop();
            GifPlayer.SetAnimationPlaying(this.AnimationContext, false, true);
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        public virtual void SetRepeatBehavior(string repeatBehavior)
        {
            if (string.IsNullOrWhiteSpace(repeatBehavior) || this.Player == null 
                || this.AnimationContext == System.IntPtr.Zero)
            {
                return;
            }

            int repeatCount = this.Player.ComputeRepeatCount(repeatBehavior);
            GifPlayer.SetAnimationRepeatCount(this.AnimationContext, repeatCount);
        }

        /// <summary>
        /// Sets the scaling filter for image resizing operations.
        /// </summary>
        /// <param name="filter">The scaling filter to apply.</param>
        /// <remarks>
        /// This method should be overridden by derived classes that support scaling operations.
        /// The base implementation does nothing.
        /// </remarks>
        public virtual void SetScalingFilter(ScalingFilter filter)
        {
            // Base implementation does nothing - override in derived classes if scaling is supported
        }

        /// <summary>
        /// Gets the current repeat count from the animation context.
        /// </summary>
        /// <returns>The current repeat count (-1=infinite, 0=complete, >0=remaining).</returns>
        protected int GetRepeatCount()
        {
            if (this.AnimationContext == System.IntPtr.Zero)
            {
                return 1;
            }

            var state = GifPlayer.GetAnimationState(this.AnimationContext);
            return state.RepeatCount;
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public virtual void Dispose()
        {
            if (this.AnimationContext != System.IntPtr.Zero)
            {
                GifPlayer.DestroyAnimationContext(this.AnimationContext);
                this.AnimationContext = System.IntPtr.Zero;
            }

            this.Player?.Dispose();
        }
    }
}
