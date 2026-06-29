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
            public int BorderlessRequested;
            public int BorderlessUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeDiagnostics
        {
            public int WgcSupported;
            public int CreateFreeThreadedSupported;
            public int BorderlessSupported;
            public int BorderlessAccessChecked;
            public int BorderlessAllowed;
            public int LastStatus;
            public int LastHadHdrData;
            public int LastBorderlessRequested;
            public int LastBorderlessUsed;
            public int ActiveCapture;
        }

        [DllImport("HdrSamplerNative.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int HdrSampler_SampleAtCursor(int sampleSize, int requestBorderless, out NativeSample output);

        [DllImport("HdrSamplerNative.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int HdrSampler_GetDiagnostics(out NativeDiagnostics output);

        [DllImport("HdrSamplerNative.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int HdrSampler_CloseCapture();

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
                    BorderlessRequested = native.BorderlessRequested != 0,
                    BorderlessUsed = native.BorderlessUsed != 0,
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

        public static string GetDiagnosticsText()
        {
            try
            {
                _ = HdrSampler_GetDiagnostics(out var diagnostics);
                return FormatDiagnostics(diagnostics);
            }
            catch (DllNotFoundException)
            {
                return "Native sampler: HdrSamplerNative.dll not found\nHDR values will show N/A";
            }
            catch (EntryPointNotFoundException)
            {
                return "Native sampler: diagnostics entry point not found\nHDR values will show N/A";
            }
            catch (BadImageFormatException)
            {
                return "Native sampler: architecture mismatch\nHDR values will show N/A";
            }
        }

        public static void CloseCapture()
        {
            try
            {
                _ = HdrSampler_CloseCapture();
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (BadImageFormatException)
            {
            }
        }

        private static string FormatDiagnostics(NativeDiagnostics diagnostics)
        {
            if (diagnostics.WgcSupported == 0)
            {
                return "WGC: unsupported\nHDR values will show N/A";
            }

            if (diagnostics.CreateFreeThreadedSupported == 0)
            {
                return "WGC: supported\nFP16 capture: CreateFreeThreaded unavailable\nHDR values will show N/A";
            }

            var borderless = diagnostics.BorderlessSupported == 0
                ? "Borderless: unavailable, using bordered capture"
                : diagnostics.BorderlessAccessChecked == 0
                    ? "Borderless: supported, access not requested yet"
                    : diagnostics.BorderlessAllowed != 0
                        ? "Borderless: enabled"
                        : "Borderless: not allowed, using bordered capture";

            return string.Join(
                "\n",
                "WGC: supported",
                "FP16 capture: supported",
                borderless,
                diagnostics.ActiveCapture != 0 ? "Capture session: active" : "Capture session: inactive",
                $"Last sample: {StatusToText(diagnostics.LastStatus, diagnostics.LastHadHdrData != 0)}");
        }

        private static string StatusToText(int status, bool hasHdrData)
        {
            switch (status)
            {
                case -3:
                    return "No sample yet";
                case -2:
                    return "Native sampler failed";
                case 0:
                    return hasHdrData ? "OK" : "OK, no HDR data";
                case 1:
                    return "WGC unsupported";
                case 3:
                    return "Monitor unavailable";
                case 4:
                    return "Frame timeout";
                case 5:
                    return "Capture format unsupported";
                case 6:
                    return "Device lost";
                case 7:
                    return "Capture failed";
                default:
                    return $"Status {status}";
            }
        }
    }
}
