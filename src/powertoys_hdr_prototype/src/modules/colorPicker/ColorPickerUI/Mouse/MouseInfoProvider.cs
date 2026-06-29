// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
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

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateMouseInfo();
        }

        private void UpdateMouseInfo()
        {
            var mousePosition = GetCursorPosition();
            if (_previousMousePosition != mousePosition)
            {
                _previousMousePosition = mousePosition;
                MousePositionChanged?.Invoke(this, mousePosition);
            }

            var colorFormatChanged = _colorFormatChanged;
            var hdrColor = HdrSamplerNative.TrySampleAtCursor(GetSampleSize());
            HdrSampleCache.Current = hdrColor;
            var hdrColorChanged = !HdrSamplesEqual(_previousHdrColor, hdrColor);

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

        private void MouseHook_OnPrimaryMouseDown(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnPrimaryMouseDown?.Invoke(this, wParam);
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnSecondaryMouseUp?.Invoke(this, wParam);
        }

        private void MouseHook_OnMiddleMouseDown(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnMiddleMouseDown?.Invoke(this, wParam);
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
