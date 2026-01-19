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
    /// Provides common animation logic that is independent of the UI framework.
    /// </summary>
    public abstract class GifAnimationControllerBase : IDisposable
    {
        /// <summary>
        /// Gets the GIF player instance.
        /// </summary>
        protected GifPlayer? Player { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether animation is playing.
        /// </summary>
        protected bool IsPlaying { get; set; }

        /// <summary>
        /// Gets or sets the repeat count.
        /// </summary>
        protected int RepeatCount { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationControllerBase"/> class.
        /// </summary>
        /// <remarks>
        /// Derived classes are responsible for initializing the Player property.
        /// </remarks>
        protected GifAnimationControllerBase()
        {
            this.IsPlaying = false;
            this.RepeatCount = 1;
        }

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        public virtual void Play()
        {
            if (this.Player == null)
            {
                return;
            }

            this.Player.Play();
            this.IsPlaying = true;
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public virtual void Pause()
        {
            if (this.Player == null)
            {
                return;
            }

            this.Player.Pause();
            this.IsPlaying = false;
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public virtual void Stop()
        {
            if (this.Player == null)
            {
                return;
            }

            this.Player.Stop();
            this.IsPlaying = false;
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        /// <exception cref="ArgumentNullException">Thrown when repeatBehavior is null.</exception>
        public void SetRepeatBehavior(string repeatBehavior)
        {
            if (string.IsNullOrWhiteSpace(repeatBehavior) || this.Player == null)
            {
                return;
            }

            this.RepeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this.Player.IsLooping);
        }

        /// <summary>
        /// Advances the animation to the next frame.
        /// </summary>
        protected void AdvanceFrame()
        {
            if (this.Player == null)
            {
                return;
            }

            var advanceResult = FrameAdvanceHelper.AdvanceFrame(
                this.Player.CurrentFrame,
                this.Player.FrameCount,
                this.RepeatCount);

            if (advanceResult.IsComplete)
            {
                this.Stop();
                return;
            }

            this.Player.CurrentFrame = advanceResult.NextFrame;
            this.RepeatCount = advanceResult.UpdatedRepeatCount;
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public virtual void Dispose()
        {
            this.Player?.Dispose();
        }
    }
}
