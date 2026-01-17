// SPDX-License-Identifier: MIT
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GifBolt.Wpf
{
    /// <summary>
    /// WPF control for displaying animated GIFs using GifBolt.
    /// Exposes dependency properties: Source, AutoStart, Loop.
    /// </summary>
    public sealed class GifBoltControl : Control
    {
        private IntPtr _native;
        private bool _isLoaded;

        static GifBoltControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GifBoltControl),
                new FrameworkPropertyMetadata(typeof(GifBoltControl)));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GifBoltControl"/> class.
        /// </summary>
        public GifBoltControl()
        {
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
            CompositionTarget.Rendering += this.OnRendering;
        }

        #region Dependency Properties
        /// <summary>
        /// Identifies the <see cref="Source"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source), typeof(string), typeof(GifBoltControl),
                new PropertyMetadata(null, OnSourceChanged));

        /// <summary>
        /// Gets or sets the path or URI to the GIF image source.
        /// Setting this property automatically triggers loading if the control is loaded.
        /// </summary>
        public string Source
        {
            get => (string)this.GetValue(SourceProperty);
            set => this.SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="AutoStart"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register(
                nameof(AutoStart), typeof(bool), typeof(GifBoltControl),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets a value indicating whether playback starts automatically when a GIF is loaded.
        /// </summary>
        public bool AutoStart
        {
            get => (bool)this.GetValue(AutoStartProperty);
            set => this.SetValue(AutoStartProperty, value);
        }

        /// <summary>
        /// Identifies the <see cref="Loop"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty LoopProperty =
            DependencyProperty.Register(
                nameof(Loop), typeof(bool), typeof(GifBoltControl),
                new PropertyMetadata(true, OnLoopChanged));

        /// <summary>
        /// Gets or sets a value indicating whether the GIF loops indefinitely.
        /// </summary>
        public bool Loop
        {
            get => (bool)this.GetValue(LoopProperty);
            set => this.SetValue(LoopProperty, value);
        }
        #endregion

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GifBoltControl)d;
            control.LoadGifIfReady();
        }

        private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GifBoltControl)d;
            if (control._native != IntPtr.Zero)
            {
                GifBolt_SetLooping(control._native, (bool)e.NewValue ? 1 : 0);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this._isLoaded = true;
            this.EnsureNative();
            this.LoadGifIfReady();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            this._isLoaded = false;
            if (this._native != IntPtr.Zero)
            {
                GifBolt_Destroy(this._native);
                this._native = IntPtr.Zero;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // Actual DX interop will be implemented later.
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (this._native != IntPtr.Zero)
            {
                GifBolt_Render(this._native);
            }
        }

        private void EnsureNative()
        {
            if (this._native == IntPtr.Zero)
            {
                this._native = GifBolt_Create();
                if (this._native != IntPtr.Zero)
                {
                    int w = Math.Max(1, (int)this.ActualWidth);
                    int h = Math.Max(1, (int)this.ActualHeight);
                    GifBolt_Initialize(this._native, (uint)w, (uint)h);
                    GifBolt_SetLooping(this._native, this.Loop ? 1 : 0);
                }
            }
        }

        private void LoadGifIfReady()
        {
            if (!this._isLoaded) return;
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            if (string.IsNullOrWhiteSpace(this.Source)) return;

            this.EnsureNative();
            if (this._native != IntPtr.Zero)
            {
                if (GifBolt_LoadGif(this._native, this.Source) != 0)
                {
                    if (this.AutoStart)
                        GifBolt_Play(this._native);
                }
            }
        }

        #region Public Control Methods
        /// <summary>
        /// Starts playback of the GIF.
        /// </summary>
        public void Play()
        {
            if (this._native != IntPtr.Zero)
            {
                GifBolt_Play(this._native);
            }
        }

        /// <summary>
        /// Pauses playback of the GIF.
        /// </summary>
        public void Pause()
        {
            if (this._native != IntPtr.Zero)
            {
                GifBolt_Pause(this._native);
            }
        }

        /// <summary>
        /// Stops playback and resets to the first frame.
        /// </summary>
        public void Stop()
        {
            if (this._native != IntPtr.Zero)
            {
                GifBolt_Stop(this._native);
            }
        }

        /// <summary>
        /// Loads a new GIF from the specified file path.
        /// </summary>
        /// <param name="path">The file path to the GIF image.</param>
        public void LoadGif(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            this.Source = path;
        }
        #endregion

        #region Native Interop
        private const string NativeLib = "GifBolt.Native";

        [DllImport(NativeLib, EntryPoint = "GifBolt_Create", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GifBolt_Create();

        [DllImport(NativeLib, EntryPoint = "GifBolt_Destroy", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GifBolt_Destroy(IntPtr handle);

        [DllImport(NativeLib, EntryPoint = "GifBolt_Initialize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GifBolt_Initialize(IntPtr handle, uint width, uint height);

        [DllImport(NativeLib, EntryPoint = "GifBolt_LoadGif", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int GifBolt_LoadGif(IntPtr handle, string path);

        [DllImport(NativeLib, EntryPoint = "GifBolt_Play", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GifBolt_Play(IntPtr handle);

        [DllImport(NativeLib, EntryPoint = "GifBolt_Pause", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GifBolt_Pause(IntPtr handle);

        [DllImport(NativeLib, EntryPoint = "GifBolt_Stop", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GifBolt_Stop(IntPtr handle);

        [DllImport(NativeLib, EntryPoint = "GifBolt_SetLooping", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GifBolt_SetLooping(IntPtr handle, int loop);

        [DllImport(NativeLib, EntryPoint = "GifBolt_Render", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GifBolt_Render(IntPtr handle);
        #endregion
    }
}
