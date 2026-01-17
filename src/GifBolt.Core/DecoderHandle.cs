// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;

namespace GifBolt.Internal
{
    internal sealed class DecoderHandle : SafeHandle
    {
        public DecoderHandle() : base(IntPtr.Zero, true) {}
        public DecoderHandle(IntPtr existing) : base(IntPtr.Zero, true)
        {
            SetHandle(existing);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                Native.gb_decoder_destroy(handle);
                SetHandle(IntPtr.Zero);
            }
            return true;
        }
    }
}
