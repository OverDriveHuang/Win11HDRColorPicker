// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// HDR prototype additions live in this copy of PowerToys Color Picker.

using System;
using System.Runtime.InteropServices;

namespace ColorPicker.Hdr
{
    internal static class HdrSamplerNative
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSample
        {
            public int Status;
            public int HasHdrData;
            public double LinearR;
            public double LinearG;
            public double LinearB;
            public double NitsR;
            public double NitsG;
            public double NitsB;
            public double YNits;
            public double IctcpI;
            public double IctcpCt;
            public double IctcpCp;
            public int IctcpI10;
            public byte SdrR;
            public byte SdrG;
            public byte SdrB;
            public byte SdrA;
            public int ScreenX;
            public int ScreenY;
            public int CaptureX;
            public int CaptureY;
            public int ActualWidth;
            public int ActualHeight;
            public int PixelCount;
        }

        [DllImport("HdrSamplerNative.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int HdrSampler_SampleAtCursor(int sampleSize, int requestBorderless, out NativeSample output);

        public static HdrColorSample TrySampleAtCursor(int sampleSize)
        {
            try
            {
                _ = HdrSampler_SampleAtCursor(sampleSize, 1, out var native);
                if (native.HasHdrData == 0)
                {
                    return null;
                }

                return new HdrColorSample
                {
                    HasHdrData = true,
                    LinearR = native.LinearR,
                    LinearG = native.LinearG,
                    LinearB = native.LinearB,
                    NitsR = native.NitsR,
                    NitsG = native.NitsG,
                    NitsB = native.NitsB,
                    YNits = native.YNits,
                    IctcpI = native.IctcpI,
                    IctcpCt = native.IctcpCt,
                    IctcpCp = native.IctcpCp,
                    IctcpI10 = native.IctcpI10,
                    SdrColor = System.Drawing.Color.FromArgb(native.SdrA, native.SdrR, native.SdrG, native.SdrB),
                };
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }
    }
}
