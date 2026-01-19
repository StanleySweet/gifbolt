// <copyright file="GifAnimationController.cs" company="GifBolt Contributors">
// Copyright (c) 2026 GifBolt Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>
// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 GifBolt Contributors

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GifBolt.Wpf
{
    /// <summary>
    /// Manages animation state and frame rendering for a GIF displayed in an Image control.
    /// Handles frame decoding, timing, and pixel updates to the display.
    /// </summary>
    internal sealed class GifAnimationController : IDisposable
    {
        private readonly Image _image;
        private readonly GifBolt.GifPlayer _player;
        private WriteableBitmap _writeableBitmap;
        private bool _isPlaying;
        private int _repeatCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="GifAnimationController"/> class.
        /// </summary>
        /// <param name="image">The Image control to animate.</param>
        /// <param name="path">The file path to the GIF image.</param>
        /// <exception cref="InvalidOperationException">Thrown if the GIF cannot be loaded.</exception>
        public GifAnimationController(Image image, string path)
        {
            this._image = image;
            this._player = new GifBolt.GifPlayer();

            if (!this._player.Load(path))
            {
                this._player.Dispose();
                throw new InvalidOperationException($"Failed to load GIF: {path}");
            }

            this._writeableBitmap = new WriteableBitmap(
                this._player.Width,
                this._player.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null);

            this._image.Source = this._writeableBitmap;

            CompositionTarget.Rendering += this.OnRendering;
        }

        /// <summary>
        /// Starts playback of the animation.
        /// </summary>
        public void Play()
        {
            this._player.Play();
            this._isPlaying = true;
        }

        /// <summary>
        /// Pauses playback of the animation.
        /// </summary>
        public void Pause()
        {
            this._player.Pause();
            this._isPlaying = false;
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            this._player.Stop();
            this._isPlaying = false;
        }

        /// <summary>
        /// Sets the repeat behavior for the animation.
        /// </summary>
        /// <param name="repeatBehavior">The repeat behavior string ("Forever", "3x", "0x", etc.).</param>
        public void SetRepeatBehavior(string repeatBehavior)
        {
            this._repeatCount = RepeatBehaviorHelper.ComputeRepeatCount(repeatBehavior, this._player.IsLooping);
        }

        /// <summary>
        /// Releases all resources held by the animation controller.
        /// </summary>
        public void Dispose()
        {
            CompositionTarget.Rendering -= this.OnRendering;
            this._player?.Dispose();
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!this._isPlaying)
            {
                return;
            }

            if (this._player.TryGetFramePixelsBgra32Premultiplied(this._player.CurrentFrame, out byte[] pixels))
            {
                int width = this._player.Width;
                int height = this._player.Height;
                int stride = width * 4;

                this._writeableBitmap.WritePixels(
                    new Int32Rect(0, 0, width, height),
                    pixels,
                    stride,
                    0);
            }

            // Advance to the next frame using shared helper
            var advanceResult = FrameAdvanceHelper.AdvanceFrame(
                this._player.CurrentFrame,
                this._player.FrameCount,
                this._repeatCount);

            if (advanceResult.IsComplete)
            {
                this.Stop();
                return;
            }

            // Update the current frame and repeat count
            this._player.CurrentFrame = advanceResult.NextFrame;
            this._repeatCount = advanceResult.UpdatedRepeatCount;
        }
    }
}
