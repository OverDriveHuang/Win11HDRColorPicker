// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

using ColorPicker.Hdr;
using ColorPicker.Helpers;
using ColorPicker.Settings;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Mouse
{
    [Export(typeof(IMouseInfoProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MouseInfoProvider : IMouseInfoProvider
    {
        private readonly double _mousePullInfoIntervalInMs;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly MouseHook _mouseHook;
        private readonly IUserSettings _userSettings;
        private readonly AppStateHandler _appStateHandler;
        private System.Windows.Point _previousMousePosition = new System.Windows.Point(-1, 1);
        private Color _previousColor = Color.Transparent;
        private HdrColorSample _previousHdrColor;
        private bool _colorFormatChanged;

        [ImportingConstructor]
        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
        {
            _mousePullInfoIntervalInMs = 1000.0 / GetMainDisplayRefreshRate();
            _timer.Interval = TimeSpan.FromMilliseconds(_mousePullInfoIntervalInMs);
            _timer.Tick += Timer_Tick;

            if (appStateMonitor != null)
            {
                appStateMonitor.AppShown += AppStateMonitor_AppShown;
                appStateMonitor.AppClosed += AppStateMonitor_AppClosed;
                appStateMonitor.AppHidden += AppStateMonitor_AppClosed;
            }

            _mouseHook = new MouseHook();
            _userSettings = userSettings;
            _appStateHandler = appStateMonitor;
            _userSettings.CopiedColorRepresentation.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
            _userSettings.CopiedColorRepresentationFormat.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
            if (_userSettings is IHdrSamplerSettings sampleSettings)
            {
                sampleSettings.SampleSize.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
            }

            _previousMousePosition = GetCursorPosition();
            _previousHdrColor = null;
            HdrSampleCache.Current = null;
            _previousColor = GetSdrColor(_previousMousePosition, _previousHdrColor);
        }

        public event EventHandler<Color> MouseColorChanged;

        public event EventHandler<HdrColorSample> MouseHdrColorChanged;

        public event EventHandler<System.Windows.Point> MousePositionChanged;

        public event EventHandler<Tuple<System.Windows.Point, bool>> OnMouseWheel;

        public event PrimaryMouseDownEventHandler OnPrimaryMouseDown;

        public event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        public event MiddleMouseDownEventHandler OnMiddleMouseDown;

        public System.Windows.Point CurrentPosition
        {
            get
            {
                return _previousMousePosition;
            }
        }

        public Color CurrentColor
        {
            get
            {
                return _previousColor;
            }
        }

        public HdrColorSample CurrentHdrColor
        {
            get
            {
                return _previousHdrColor;
            }
        }

        public bool TryGetGdiColorAtCurrentPosition(out Color color)
            => TryGetGdiColorAtScreenPosition(_previousMousePosition, out color);

        public bool TryGetGdiColorAtScreenPosition(System.Windows.Point screenPosition, out Color color)
        {
            try
            {
                color = GetPixelColor(screenPosition);
                return true;
            }
            catch (Exception)
            {
                color = Color.Transparent;
                return false;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateMouseInfo();
        }

        private void UpdateMouseInfo()
        {
            var mousePosition = GetCursorPosition();
            var mousePositionChanged = _previousMousePosition != mousePosition;
            if (_previousMousePosition != mousePosition)
            {
                _previousMousePosition = mousePosition;
                MousePositionChanged?.Invoke(this, mousePosition);
            }

            var colorFormatChanged = _colorFormatChanged;
            var sampleSize = GetSampleSize();
            var hdrColor = HdrSamplerNative.TrySampleAtCursor(sampleSize);
            HdrSampleCache.Current = hdrColor;
            var hdrColorChanged = !HdrSamplesEqual(_previousHdrColor, hdrColor);
            if (hdrColor?.HasHdrData == true && (mousePositionChanged || hdrColorChanged || colorFormatChanged))
            {
                HdrSamplerNative.UpdateGdiComparison(
                    hdrColor,
                    sampleSize,
                    (int)mousePosition.X,
                    (int)mousePosition.Y,
                    GetGdiExpectedColor(mousePosition, sampleSize));
            }

            // Keep SDR formats and the swatch visually aligned with the HDR sample.
            // GDI remains the fallback when WGC/HDR sampling is unavailable.
            var color = GetSdrColor(mousePosition, hdrColor);
            if (_previousColor != color || colorFormatChanged)
            {
                _previousColor = color;
                MouseColorChanged?.Invoke(this, color);
            }

            if (hdrColorChanged || colorFormatChanged)
            {
                _previousHdrColor = hdrColor;
                MouseHdrColorChanged?.Invoke(this, hdrColor);
            }

            _colorFormatChanged = false;
        }

        private static bool HdrSamplesEqual(HdrColorSample left, HdrColorSample right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.HasHdrData == right.HasHdrData
                   && left.LinearR == right.LinearR
                   && left.LinearG == right.LinearG
                   && left.LinearB == right.LinearB
                   && left.NitsR == right.NitsR
                   && left.NitsG == right.NitsG
                   && left.NitsB == right.NitsB
                   && left.YNits == right.YNits
                   && left.IctcpI == right.IctcpI
                   && left.IctcpCt == right.IctcpCt
                   && left.IctcpCp == right.IctcpCp
                   && left.IctcpI10 == right.IctcpI10;
        }

        private static Color GetPixelColor(System.Windows.Point mousePosition)
        {
            var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) // Ensure Graphics object is disposed
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                return bmp.GetPixel(0, 0);
            }
        }

        private HdrSamplerNative.GdiExpectedColor GetGdiExpectedColor(System.Windows.Point mousePosition, int sampleSize)
        {
            var halfSize = sampleSize / 2;
            var rect = new Rectangle(
                (int)mousePosition.X - halfSize,
                (int)mousePosition.Y - halfSize,
                sampleSize,
                sampleSize);

            try
            {
                return CaptureGdiExpectedColorWithPowerToysExclusion(rect);
            }
            catch (Exception)
            {
                return new HdrSamplerNative.GdiExpectedColor(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty, string.Empty);
            }
        }

        private HdrSamplerNative.GdiExpectedColor CaptureGdiExpectedColorWithPowerToysExclusion(Rectangle rect)
        {
            var samplePoint = new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));
            var topWindowBeforeExclusion = DescribeWindowAtPoint(samplePoint);
            var excludedWindows = ExcludeCurrentProcessWindows();

            try
            {
                var topWindowAfterExclusion = DescribeWindowAtPoint(samplePoint);
                var gdi = CaptureGdiExpectedColor(rect);
                return new HdrSamplerNative.GdiExpectedColor(
                    gdi.Available,
                    gdi.R,
                    gdi.G,
                    gdi.B,
                    gdi.ExpectedLinearR,
                    gdi.ExpectedLinearG,
                    gdi.ExpectedLinearB,
                    gdi.ActualWidth,
                    gdi.ActualHeight,
                    gdi.PixelCount,
                    excludedWindows.Count,
                    topWindowBeforeExclusion,
                    topWindowAfterExclusion);
            }
            finally
            {
                foreach (var hwnd in excludedWindows)
                {
                    WindowCaptureExclusionHelper.Include(hwnd);
                }
            }
        }

        private List<IntPtr> ExcludeCurrentProcessWindows()
        {
            var excludedWindows = new List<IntPtr>();
            foreach (var hwnd in GetCurrentProcessVisibleWindows())
            {
                if (WindowCaptureExclusionHelper.Exclude(hwnd))
                {
                    excludedWindows.Add(hwnd);
                }
            }

            return excludedWindows;
        }

        private static List<IntPtr> GetCurrentProcessVisibleWindows()
        {
            var windows = new List<IntPtr>();
            var currentProcessId = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                {
                    return true;
                }

                GetWindowThreadProcessId(hwnd, out var processId);
                if (processId == currentProcessId)
                {
                    windows.Add(hwnd);
                }

                return true;
            }, IntPtr.Zero);

            return windows;
        }

        private static HdrSamplerNative.GdiExpectedColor CaptureGdiExpectedColor(Rectangle rect)
        {
            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                double rByteSum = 0;
                double gByteSum = 0;
                double bByteSum = 0;
                double rLinearSum = 0;
                double gLinearSum = 0;
                double bLinearSum = 0;
                var pixelCount = 0;
                for (var y = 0; y < bmp.Height; ++y)
                {
                    for (var x = 0; x < bmp.Width; ++x)
                    {
                        var pixel = bmp.GetPixel(x, y);
                        rByteSum += pixel.R;
                        gByteSum += pixel.G;
                        bByteSum += pixel.B;
                        rLinearSum += SrgbByteToLinear(pixel.R);
                        gLinearSum += SrgbByteToLinear(pixel.G);
                        bLinearSum += SrgbByteToLinear(pixel.B);
                        ++pixelCount;
                    }
                }

                return new HdrSamplerNative.GdiExpectedColor(
                    true,
                    AverageToByte(rByteSum, pixelCount),
                    AverageToByte(gByteSum, pixelCount),
                    AverageToByte(bByteSum, pixelCount),
                    rLinearSum / pixelCount,
                    gLinearSum / pixelCount,
                    bLinearSum / pixelCount,
                    bmp.Width,
                    bmp.Height,
                    pixelCount,
                    0,
                    string.Empty,
                    string.Empty);
            }
        }

        private static string DescribeWindowAtPoint(Point point)
        {
            var hwnd = WindowFromPoint(new NativePoint { X = point.X, Y = point.Y });
            if (hwnd == IntPtr.Zero)
            {
                return "none";
            }

            GetWindowThreadProcessId(hwnd, out var processId);

            var processName = "unknown";
            try
            {
                processName = Process.GetProcessById((int)processId).ProcessName;
            }
            catch (Exception)
            {
            }

            return $"hwnd=0x{hwnd.ToInt64():X}, pid={processId}, process={processName}, class={GetWindowClass(hwnd)}, title={GetWindowTitle(hwnd)}";
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var builder = new StringBuilder(128);
            GetWindowText(hwnd, builder, builder.Capacity);
            return TruncateWindowText(builder.ToString());
        }

        private static string GetWindowClass(IntPtr hwnd)
        {
            var builder = new StringBuilder(128);
            GetClassName(hwnd, builder, builder.Capacity);
            return TruncateWindowText(builder.ToString());
        }

        private static string TruncateWindowText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "empty";
            }

            value = value.Replace('\r', ' ').Replace('\n', ' ');
            return value.Length <= 80 ? value : value.Substring(0, 80);
        }

        private static byte AverageToByte(double sum, int count)
            => (byte)Math.Round(Math.Min(Math.Max(sum / count, 0), 255));

        private static double SrgbByteToLinear(byte value)
        {
            var srgb = value / 255.0;
            return srgb <= 0.04045
                ? srgb / 12.92
                : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;

            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private static Color GetSdrColor(System.Windows.Point mousePosition, HdrColorSample hdrColor)
            => hdrColor?.HasHdrData == true ? hdrColor.SdrColor : GetPixelColor(mousePosition);

        private static System.Windows.Point GetCursorPosition()
        {
            GetCursorPos(out PointInter lpPoint);
            return (System.Windows.Point)lpPoint;
        }

        private static double GetMainDisplayRefreshRate()
        {
            double refreshRate = 60.0;

            foreach (var monitor in MonitorResolutionHelper.AllMonitors)
            {
                if (monitor.IsPrimary && EnumDisplaySettingsW(monitor.Name, ENUM_CURRENT_SETTINGS, out DEVMODEW lpDevMode))
                {
                    refreshRate = (double)lpDevMode.dmDisplayFrequency;
                    break;
                }
            }

            return refreshRate;
        }

        private void AppStateMonitor_AppClosed(object sender, EventArgs e)
        {
            DisposeHook();
        }

        private void AppStateMonitor_AppShown(object sender, EventArgs e)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(UpdateMouseInfo), DispatcherPriority.ApplicationIdle);

            _mouseHook.OnPrimaryMouseDown += MouseHook_OnPrimaryMouseDown;
            _mouseHook.OnMouseWheel += MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp += MouseHook_OnSecondaryMouseUp;
            _mouseHook.OnMiddleMouseDown += MouseHook_OnMiddleMouseDown;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.SetColorPickerCursor();
            }
        }

        private void MouseHook_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var zoomIn = e.Delta > 0;
            OnMouseWheel?.Invoke(this, new Tuple<System.Windows.Point, bool>(_previousMousePosition, zoomIn));
        }

        private void MouseHook_OnPrimaryMouseDown(object sender, IntPtr wParam, System.Windows.Point screenPosition)
        {
            _previousMousePosition = screenPosition;
            DisposeHook();
            OnPrimaryMouseDown?.Invoke(this, wParam, screenPosition);
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam, System.Windows.Point screenPosition)
        {
            _previousMousePosition = screenPosition;
            DisposeHook();
            OnSecondaryMouseUp?.Invoke(this, wParam, screenPosition);
        }

        private void MouseHook_OnMiddleMouseDown(object sender, IntPtr wParam, System.Windows.Point screenPosition)
        {
            _previousMousePosition = screenPosition;
            DisposeHook();
            OnMiddleMouseDown?.Invoke(this, wParam, screenPosition);
        }

        private void CopiedColorRepresentation_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _colorFormatChanged = true;
        }

        private int GetSampleSize()
            => _userSettings is IHdrSamplerSettings sampleSettings ? sampleSettings.SampleSize.Value : 1;

        private void DisposeHook()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }

            HdrSamplerNative.CloseCapture();
            _previousMousePosition = new System.Windows.Point(-1, 1);
            _mouseHook.OnPrimaryMouseDown -= MouseHook_OnPrimaryMouseDown;
            _mouseHook.OnMouseWheel -= MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp -= MouseHook_OnSecondaryMouseUp;
            _mouseHook.OnMiddleMouseDown -= MouseHook_OnMiddleMouseDown;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.RestoreOriginalCursors();
            }
        }
    }
}
