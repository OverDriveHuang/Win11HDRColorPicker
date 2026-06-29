// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using ManagedCommon;
using Forms = System.Windows.Forms;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Keyboard
{
    [Export(typeof(KeyboardMonitor))]
    public sealed class KeyboardMonitor : IDisposable
    {
        private const int HotKeyId = 0x48445232;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private bool _started;
        private IntPtr _windowHandle;
        private HwndSource _hwndSource;
        private GlobalKeyboardHook _keyboardHook;
        private Forms.NotifyIcon _notifyIcon;

        [ImportingConstructor]
        public KeyboardMonitor(AppStateHandler appStateHandler, IUserSettings userSettings)
        {
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _userSettings.ActivationShortcut.PropertyChanged += ActivationShortcut_PropertyChanged;
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                RegisterCurrentHotKey();
                StartKeyboardHook();
                EnsureTrayIcon();
                _appStateHandler.OpenColorEditor();
            }), DispatcherPriority.ApplicationIdle);
        }

        public void Dispose()
        {
            _userSettings.ActivationShortcut.PropertyChanged -= ActivationShortcut_PropertyChanged;
            UnregisterCurrentHotKey();
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        private void ActivationShortcut_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_started)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(RegisterCurrentHotKey), DispatcherPriority.ApplicationIdle);
            }
        }

        private void RegisterCurrentHotKey()
        {
            UnregisterCurrentHotKey();

            _windowHandle = _appStateHandler.GetMainWindowHandle();
            if (_windowHandle == IntPtr.Zero && Application.Current.MainWindow != null)
            {
                _windowHandle = new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
            }

            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WndProc);

            var (modifiers, virtualKey) = ParseShortcut(_userSettings.ActivationShortcut.Value);
            if (modifiers == 0)
            {
                Logger.LogWarning($"Invalid Color Picker hotkey '{_userSettings.ActivationShortcut.Value}'. Falling back to Win + Shift + C.");
                modifiers = ModWin | ModShift;
                virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.C);
            }

            if (!RegisterHotKey(_windowHandle, HotKeyId, modifiers | MOD_NOREPEAT, virtualKey))
            {
                Logger.LogWarning($"Failed to register Color Picker hotkey '{_userSettings.ActivationShortcut.Value}'. Another app may already own it.");
            }
        }

        private void UnregisterCurrentHotKey()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HotKeyId);
            }

            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotKeyId)
            {
                _appStateHandler.StartUserSession();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void StartKeyboardHook()
        {
            if (_keyboardHook != null)
            {
                return;
            }

            try
            {
                _keyboardHook = new GlobalKeyboardHook();
                _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start Color Picker keyboard hook.", ex);
            }
        }

        private void Hook_KeyboardPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyDown && e.KeyboardState != GlobalKeyboardHook.KeyboardState.SysKeyDown)
            {
                return;
            }

            var virtualCode = e.KeyboardData.VirtualCode;
            if (virtualCode == KeyInterop.VirtualKeyFromKey(Key.Escape))
            {
                if (_appStateHandler.IsColorPickerVisible())
                {
                    e.Handled = true;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _appStateHandler.HandleEscPressed()), DispatcherPriority.Input);
                }

                return;
            }

            if (virtualCode == KeyInterop.VirtualKeyFromKey(Key.Space) || virtualCode == KeyInterop.VirtualKeyFromKey(Key.Enter))
            {
                if (_appStateHandler.IsColorPickerVisible())
                {
                    e.Handled = true;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => _appStateHandler.HandleEnterPressed()), DispatcherPriority.Input);
                }
            }
        }

        private void EnsureTrayIcon()
        {
            if (_notifyIcon != null)
            {
                return;
            }

            _notifyIcon = new Forms.NotifyIcon();
            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule?.FileName);
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Text = "HDR Color Picker";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += (_, __) => Application.Current.Dispatcher.Invoke(_appStateHandler.OpenColorEditor);

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Color Picker", null, (_, __) => Application.Current.Dispatcher.Invoke(_appStateHandler.OpenColorEditor));
            menu.Items.Add("Settings", null, (_, __) => Application.Current.Dispatcher.Invoke(() => SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ColorPicker)));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => Application.Current.Dispatcher.Invoke(() =>
            {
                Dispose();
                Application.Current.Shutdown();
            }));
            _notifyIcon.ContextMenuStrip = menu;
        }

        private static (uint Modifiers, uint VirtualKey) ParseShortcut(string shortcut)
        {
            uint modifiers = 0;
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.C);

            foreach (var rawPart in (shortcut ?? "Win + Shift + C").Split('+'))
            {
                var part = rawPart.Trim();
                if (part.Equals("Win", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModWin;
                }
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModShift;
                }
                else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModControl;
                }
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModAlt;
                }
                else if (part.Length == 1)
                {
                    virtualKey = (uint)char.ToUpperInvariant(part[0]);
                }
                else if (Enum.TryParse<Key>(part, ignoreCase: true, out var key))
                {
                    virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                }
            }

            return (modifiers, virtualKey);
        }
    }
}
