// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// HDR prototype additions live in this copy of PowerToys Color Picker.

using System;
using System.Drawing;
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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
            public int SdrWhiteLevelAvailable;
            public int SdrWhiteLevelRaw;
            public double SdrWhiteLevelNits;
            public double SdrWhiteLevelScale;
            public int DxgiOutputAvailable;
            public int DxgiBitsPerColor;
            public int DxgiColorSpace;
            public int DxgiHdrColorSpace;
            public double DxgiMinLuminance;
            public double DxgiMaxLuminance;
            public double DxgiMaxFullFrameLuminance;
            public int AdvancedColorInfoAvailable;
            public int AdvancedColorSupported;
            public int AdvancedColorEnabled;
            public int WideColorEnforced;
            public int AdvancedColorForceDisabled;
            public int AdvancedColorEncoding;
            public int AdvancedColorBitsPerChannel;
            public int MonitorInfoAvailable;
            public int MonitorLeft;
            public int MonitorTop;
            public int MonitorRight;
            public int MonitorBottom;
            public int CursorX;
            public int CursorY;
            public int ComparisonAvailable;
            public int ComparisonGdiAvailable;
            public int ComparisonSampleSize;
            public int ComparisonScreenX;
            public int ComparisonScreenY;
            public int ComparisonCaptureX;
            public int ComparisonCaptureY;
            public int ComparisonGdiActualWidth;
            public int ComparisonGdiActualHeight;
            public int ComparisonGdiPixelCount;
            public double ComparisonWgcLinearR;
            public double ComparisonWgcLinearG;
            public double ComparisonWgcLinearB;
            public byte ComparisonWgcSdrR;
            public byte ComparisonWgcSdrG;
            public byte ComparisonWgcSdrB;
            public byte ComparisonGdiR;
            public byte ComparisonGdiG;
            public byte ComparisonGdiB;
            public double ComparisonGdiExpectedLinearR;
            public double ComparisonGdiExpectedLinearG;
            public double ComparisonGdiExpectedLinearB;
            public double ComparisonRatioR;
            public double ComparisonRatioG;
            public double ComparisonRatioB;
            public int ComparisonRatioRAvailable;
            public int ComparisonRatioGAvailable;
            public int ComparisonRatioBAvailable;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string MonitorDeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string MonitorFriendlyName;
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
                    ScreenX = native.ScreenX,
                    ScreenY = native.ScreenY,
                    CaptureX = native.CaptureX,
                    CaptureY = native.CaptureY,
                    ActualWidth = native.ActualWidth,
                    ActualHeight = native.ActualHeight,
                    PixelCount = native.PixelCount,
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

        private static GdiComparison _lastGdiComparison;

        public static void UpdateGdiComparison(HdrColorSample sample, int sampleSize, int screenX, int screenY, GdiExpectedColor gdi)
        {
            if (sample?.HasHdrData != true)
            {
                _lastGdiComparison = null;
                return;
            }

            _lastGdiComparison = new GdiComparison
            {
                SampleSize = sampleSize,
                WgcScreenX = sample.ScreenX,
                WgcScreenY = sample.ScreenY,
                WgcCaptureX = sample.CaptureX,
                WgcCaptureY = sample.CaptureY,
                WgcActualWidth = sample.ActualWidth,
                WgcActualHeight = sample.ActualHeight,
                WgcPixelCount = sample.PixelCount,
                ManagedScreenX = screenX,
                ManagedScreenY = screenY,
                WgcLinearR = sample.LinearR,
                WgcLinearG = sample.LinearG,
                WgcLinearB = sample.LinearB,
                WgcSdrR = sample.SdrColor.R,
                WgcSdrG = sample.SdrColor.G,
                WgcSdrB = sample.SdrColor.B,
                GdiAvailable = gdi.Available,
                GdiR = gdi.R,
                GdiG = gdi.G,
                GdiB = gdi.B,
                GdiExpectedLinearR = gdi.ExpectedLinearR,
                GdiExpectedLinearG = gdi.ExpectedLinearG,
                GdiExpectedLinearB = gdi.ExpectedLinearB,
                GdiActualWidth = gdi.ActualWidth,
                GdiActualHeight = gdi.ActualHeight,
                GdiPixelCount = gdi.PixelCount,
                HiddenWindowCount = gdi.HiddenWindowCount,
                TopWindowBeforeExclusion = gdi.TopWindowBeforeExclusion,
                TopWindowAfterExclusion = gdi.TopWindowAfterExclusion,
            };
        }

        public readonly struct GdiExpectedColor
        {
            public GdiExpectedColor(
                bool available,
                byte r,
                byte g,
                byte b,
                double expectedLinearR,
                double expectedLinearG,
                double expectedLinearB,
                int actualWidth,
                int actualHeight,
                int pixelCount,
                int hiddenWindowCount,
                string topWindowBeforeExclusion,
                string topWindowAfterExclusion)
            {
                Available = available;
                R = r;
                G = g;
                B = b;
                ExpectedLinearR = expectedLinearR;
                ExpectedLinearG = expectedLinearG;
                ExpectedLinearB = expectedLinearB;
                ActualWidth = actualWidth;
                ActualHeight = actualHeight;
                PixelCount = pixelCount;
                HiddenWindowCount = hiddenWindowCount;
                TopWindowBeforeExclusion = topWindowBeforeExclusion ?? string.Empty;
                TopWindowAfterExclusion = topWindowAfterExclusion ?? string.Empty;
            }

            public bool Available { get; }

            public byte R { get; }

            public byte G { get; }

            public byte B { get; }

            public double ExpectedLinearR { get; }

            public double ExpectedLinearG { get; }

            public double ExpectedLinearB { get; }

            public int ActualWidth { get; }

            public int ActualHeight { get; }

            public int PixelCount { get; }

            public int HiddenWindowCount { get; }

            public string TopWindowBeforeExclusion { get; }

            public string TopWindowAfterExclusion { get; }
        }

        private sealed class GdiComparison
        {
            public int SampleSize { get; init; }

            public int WgcScreenX { get; init; }

            public int WgcScreenY { get; init; }

            public int WgcCaptureX { get; init; }

            public int WgcCaptureY { get; init; }

            public int WgcActualWidth { get; init; }

            public int WgcActualHeight { get; init; }

            public int WgcPixelCount { get; init; }

            public int ManagedScreenX { get; init; }

            public int ManagedScreenY { get; init; }

            public double WgcLinearR { get; init; }

            public double WgcLinearG { get; init; }

            public double WgcLinearB { get; init; }

            public byte WgcSdrR { get; init; }

            public byte WgcSdrG { get; init; }

            public byte WgcSdrB { get; init; }

            public bool GdiAvailable { get; init; }

            public byte GdiR { get; init; }

            public byte GdiG { get; init; }

            public byte GdiB { get; init; }

            public double GdiExpectedLinearR { get; init; }

            public double GdiExpectedLinearG { get; init; }

            public double GdiExpectedLinearB { get; init; }

            public int GdiActualWidth { get; init; }

            public int GdiActualHeight { get; init; }

            public int GdiPixelCount { get; init; }

            public int HiddenWindowCount { get; init; }

            public string TopWindowBeforeExclusion { get; init; }

            public string TopWindowAfterExclusion { get; init; }
        }

        private static string FormatDiagnostics(NativeDiagnostics diagnostics)
        {
            if (diagnostics.WgcSupported == 0)
            {
                return string.Join(
                    "\n",
                    "WGC: unsupported",
                    FormatMonitor(diagnostics),
                    FormatSdrWhiteLevel(diagnostics),
                    FormatDxgiOutput(diagnostics),
                    FormatAdvancedColor(diagnostics),
                    "HDR values will show N/A");
            }

            if (diagnostics.CreateFreeThreadedSupported == 0)
            {
                return string.Join(
                    "\n",
                    "WGC: supported",
                    "FP16 capture: CreateFreeThreaded unavailable",
                    FormatMonitor(diagnostics),
                    FormatSdrWhiteLevel(diagnostics),
                    FormatDxgiOutput(diagnostics),
                    FormatAdvancedColor(diagnostics),
                    "HDR values will show N/A");
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
                FormatMonitor(diagnostics),
                FormatSdrWhiteLevel(diagnostics),
                FormatDxgiOutput(diagnostics),
                FormatAdvancedColor(diagnostics),
                diagnostics.ActiveCapture != 0 ? "Capture session: active" : "Capture session: inactive",
                $"Last sample: {StatusToText(diagnostics.LastStatus, diagnostics.LastHadHdrData != 0)}",
                FormatGdiComparison());
        }

        private static string FormatMonitor(NativeDiagnostics diagnostics)
        {
            if (diagnostics.MonitorInfoAvailable == 0)
            {
                return "Monitor: unavailable";
            }

            var friendlyName = string.IsNullOrWhiteSpace(diagnostics.MonitorFriendlyName)
                ? "unknown"
                : diagnostics.MonitorFriendlyName;

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Monitor: {0}, name={1}, cursor=({2},{3}), bounds=[{4},{5},{6},{7}]",
                diagnostics.MonitorDeviceName,
                friendlyName,
                diagnostics.CursorX,
                diagnostics.CursorY,
                diagnostics.MonitorLeft,
                diagnostics.MonitorTop,
                diagnostics.MonitorRight,
                diagnostics.MonitorBottom);
        }

        private static string FormatSdrWhiteLevel(NativeDiagnostics diagnostics)
        {
            if (diagnostics.SdrWhiteLevelAvailable == 0)
            {
                return "SDR white level: unavailable";
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "SDR white level: raw={0}, {1:0.##} nits, scale={2:0.####}x",
                diagnostics.SdrWhiteLevelRaw,
                diagnostics.SdrWhiteLevelNits,
                diagnostics.SdrWhiteLevelScale);
        }

        private static string FormatDxgiOutput(NativeDiagnostics diagnostics)
        {
            if (diagnostics.DxgiOutputAvailable == 0)
            {
                return "DXGI output: unavailable";
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "DXGI output: bpc={0}, colorSpace={1} ({2}), HDR={3}, nits=[min={4:0.####}, max={5:0.####}, full={6:0.####}]",
                diagnostics.DxgiBitsPerColor,
                DxgiColorSpaceToText(diagnostics.DxgiColorSpace),
                diagnostics.DxgiColorSpace,
                diagnostics.DxgiHdrColorSpace != 0 ? "yes" : "no",
                diagnostics.DxgiMinLuminance,
                diagnostics.DxgiMaxLuminance,
                diagnostics.DxgiMaxFullFrameLuminance);
        }

        private static string FormatAdvancedColor(NativeDiagnostics diagnostics)
        {
            if (diagnostics.AdvancedColorInfoAvailable == 0)
            {
                return "Advanced color: unavailable";
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Advanced color: supported={0}, enabled={1}, wide={2}, forceDisabled={3}, encoding={4} ({5}), bpc={6}",
                YesNo(diagnostics.AdvancedColorSupported),
                YesNo(diagnostics.AdvancedColorEnabled),
                YesNo(diagnostics.WideColorEnforced),
                YesNo(diagnostics.AdvancedColorForceDisabled),
                AdvancedColorEncodingToText(diagnostics.AdvancedColorEncoding),
                diagnostics.AdvancedColorEncoding,
                diagnostics.AdvancedColorBitsPerChannel);
        }

        private static string YesNo(int value)
        {
            return value != 0 ? "yes" : "no";
        }

        private static string FormatComparison(NativeDiagnostics diagnostics)
        {
            if (diagnostics.ComparisonAvailable == 0)
            {
                return "Last WGC/GDI comparison: No sample yet";
            }

            if (diagnostics.ComparisonGdiAvailable == 0)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Last WGC/GDI comparison:\n  point: screen=({0},{1}), capture=({2},{3}), sample={4}x{4}\n  WGC linear=({5:0.####}, {6:0.####}, {7:0.####}), WGC SDR RGB=rgb({8}, {9}, {10})\n  GDI: unavailable",
                    diagnostics.ComparisonScreenX,
                    diagnostics.ComparisonScreenY,
                    diagnostics.ComparisonCaptureX,
                    diagnostics.ComparisonCaptureY,
                    diagnostics.ComparisonSampleSize,
                    diagnostics.ComparisonWgcLinearR,
                    diagnostics.ComparisonWgcLinearG,
                    diagnostics.ComparisonWgcLinearB,
                    diagnostics.ComparisonWgcSdrR,
                    diagnostics.ComparisonWgcSdrG,
                    diagnostics.ComparisonWgcSdrB);
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Last WGC/GDI comparison:\n  point: screen=({0},{1}), capture=({2},{3}), sample={4}x{4}, gdiArea={5}x{6}, gdiPixels={7}\n  WGC linear=({8:0.####}, {9:0.####}, {10:0.####}), WGC SDR RGB=rgb({11}, {12}, {13})\n  GDI RGB=rgb({14}, {15}, {16}), GDI expected linear=({17:0.####}, {18:0.####}, {19:0.####})\n  ratio WGC/expected=({20}, {21}, {22})",
                diagnostics.ComparisonScreenX,
                diagnostics.ComparisonScreenY,
                diagnostics.ComparisonCaptureX,
                diagnostics.ComparisonCaptureY,
                diagnostics.ComparisonSampleSize,
                diagnostics.ComparisonGdiActualWidth,
                diagnostics.ComparisonGdiActualHeight,
                diagnostics.ComparisonGdiPixelCount,
                diagnostics.ComparisonWgcLinearR,
                diagnostics.ComparisonWgcLinearG,
                diagnostics.ComparisonWgcLinearB,
                diagnostics.ComparisonWgcSdrR,
                diagnostics.ComparisonWgcSdrG,
                diagnostics.ComparisonWgcSdrB,
                diagnostics.ComparisonGdiR,
                diagnostics.ComparisonGdiG,
                diagnostics.ComparisonGdiB,
                diagnostics.ComparisonGdiExpectedLinearR,
                diagnostics.ComparisonGdiExpectedLinearG,
                diagnostics.ComparisonGdiExpectedLinearB,
                FormatRatio(diagnostics.ComparisonRatioR, diagnostics.ComparisonRatioRAvailable),
                FormatRatio(diagnostics.ComparisonRatioG, diagnostics.ComparisonRatioGAvailable),
                FormatRatio(diagnostics.ComparisonRatioB, diagnostics.ComparisonRatioBAvailable));
        }

        private static string FormatGdiComparison()
        {
            var comparison = _lastGdiComparison;
            if (comparison == null)
            {
                return "Last WGC/GDI comparison: No sample yet";
            }

            if (!comparison.GdiAvailable)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Last WGC/GDI comparison:\n  point: managedScreen=({0},{1}), wgcScreen=({2},{3}), capture=({4},{5}), sample={6}x{6}, wgcArea={7}x{8}, wgcPixels={9}, excludedWindows={10}\n  topWindowBefore={11}\n  topWindowAfterExclusion={12}\n  WGC linear=({13:0.####}, {14:0.####}, {15:0.####}), WGC SDR RGB=rgb({16}, {17}, {18})\n  GDI CopyFromScreen: unavailable",
                    comparison.ManagedScreenX,
                    comparison.ManagedScreenY,
                    comparison.WgcScreenX,
                    comparison.WgcScreenY,
                    comparison.WgcCaptureX,
                    comparison.WgcCaptureY,
                    comparison.SampleSize,
                    comparison.WgcActualWidth,
                    comparison.WgcActualHeight,
                    comparison.WgcPixelCount,
                    comparison.HiddenWindowCount,
                    comparison.TopWindowBeforeExclusion,
                    comparison.TopWindowAfterExclusion,
                    comparison.WgcLinearR,
                    comparison.WgcLinearG,
                    comparison.WgcLinearB,
                    comparison.WgcSdrR,
                    comparison.WgcSdrG,
                    comparison.WgcSdrB);
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Last WGC/GDI comparison:\n  point: managedScreen=({0},{1}), wgcScreen=({2},{3}), capture=({4},{5}), sample={6}x{6}, wgcArea={7}x{8}, wgcPixels={9}, gdiArea={10}x{11}, gdiPixels={12}, excludedWindows={13}\n  topWindowBefore={14}\n  topWindowAfterExclusion={15}\n  WGC linear=({16:0.####}, {17:0.####}, {18:0.####}), WGC SDR RGB=rgb({19}, {20}, {21})\n  GDI CopyFromScreen RGB=rgb({22}, {23}, {24}), GDI expected linear=({25:0.####}, {26:0.####}, {27:0.####})\n  ratio WGC/expected=({28}, {29}, {30})",
                comparison.ManagedScreenX,
                comparison.ManagedScreenY,
                comparison.WgcScreenX,
                comparison.WgcScreenY,
                comparison.WgcCaptureX,
                comparison.WgcCaptureY,
                comparison.SampleSize,
                comparison.WgcActualWidth,
                comparison.WgcActualHeight,
                comparison.WgcPixelCount,
                comparison.GdiActualWidth,
                comparison.GdiActualHeight,
                comparison.GdiPixelCount,
                comparison.HiddenWindowCount,
                comparison.TopWindowBeforeExclusion,
                comparison.TopWindowAfterExclusion,
                comparison.WgcLinearR,
                comparison.WgcLinearG,
                comparison.WgcLinearB,
                comparison.WgcSdrR,
                comparison.WgcSdrG,
                comparison.WgcSdrB,
                comparison.GdiR,
                comparison.GdiG,
                comparison.GdiB,
                comparison.GdiExpectedLinearR,
                comparison.GdiExpectedLinearG,
                comparison.GdiExpectedLinearB,
                FormatRatio(comparison.WgcLinearR, comparison.GdiExpectedLinearR),
                FormatRatio(comparison.WgcLinearG, comparison.GdiExpectedLinearG),
                FormatRatio(comparison.WgcLinearB, comparison.GdiExpectedLinearB));
        }

        private static string FormatRatio(double value, int available)
        {
            return available != 0
                ? value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                : "N/A";
        }

        private static string FormatRatio(double numerator, double denominator)
        {
            return Math.Abs(denominator) > 0.0000001
                ? (numerator / denominator).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
                : "N/A";
        }

        private static string DxgiColorSpaceToText(int value)
        {
            switch (value)
            {
                case 0:
                    return "RGB_FULL_G22_NONE_P709";
                case 1:
                    return "RGB_FULL_G10_NONE_P709";
                case 12:
                    return "RGB_FULL_G2084_NONE_P2020";
                case 17:
                    return "RGB_FULL_G22_NONE_P2020";
                default:
                    return "Unknown";
            }
        }

        private static string AdvancedColorEncodingToText(int value)
        {
            switch (value)
            {
                case 0:
                    return "RGB";
                case 1:
                    return "YCbCr444";
                case 2:
                    return "YCbCr422";
                case 3:
                    return "YCbCr420";
                case 4:
                    return "Intensity";
                default:
                    return "Unknown";
            }
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
