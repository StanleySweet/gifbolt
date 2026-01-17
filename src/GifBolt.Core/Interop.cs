// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace GifBolt.Internal
{
    internal static class Native
    {
#if WINDOWS
        private const string DllName = "GifBolt.Native";
#else
        private const string DllName = "GifBolt.Native";
#endif
        // NOTE: Ces signatures supposent une future API C côté natif (C ABI)
        // qui sera fournie par la lib C++ (export C).
        // Les functions et noms sont des placeholders à synchroniser.

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr gb_decoder_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void gb_decoder_destroy(IntPtr decoder);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int gb_decoder_load_from_path(IntPtr decoder, string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_frame_count(IntPtr decoder);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_width(IntPtr decoder);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_height(IntPtr decoder);

        // -1 = loop infini, >=0 = compteur de boucles
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_loop_count(IntPtr decoder);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int gb_decoder_get_frame_delay_ms(IntPtr decoder, int index);

        // Retourne un pointer vers un buffer RGBA32 et la taille (octets)
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr gb_decoder_get_frame_pixels_rgba32(IntPtr decoder, int index, out int byteCount);
    }
}
