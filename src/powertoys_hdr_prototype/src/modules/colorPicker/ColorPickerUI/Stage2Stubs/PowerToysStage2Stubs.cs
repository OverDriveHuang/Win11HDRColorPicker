// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

using ColorPicker.Stage2;

namespace ManagedCommon
{
    public static class Logger
    {
        public static void InitializeLogger(string path) { }

        public static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            WriteLog("INFO", message, null);
        }

        public static void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            WriteLog("WARN", message, null);
        }

        public static void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            WriteLog("ERROR", message, null);
        }

        public static void LogError(string message, Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(message + " " + exception);
            WriteLog("ERROR", message, exception);
        }

        private static void WriteLog(string level, string message, Exception exception)
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToysHDRColorPicker");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "stage2.log"), $"{DateTimeOffset.Now:u} [{level}] {message}{Environment.NewLine}{exception}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }

    public static class LanguageHelper
    {
        public static string LoadLanguage() => string.Empty;
    }

    public static class RunnerHelper
    {
        public static void WaitForPowerToysRunner(int processId, Action onExited) { }
    }

    public static class OSVersionHelper
    {
        public static bool IsWindows11() => Environment.OSVersion.Version.Build >= 22000;
    }

    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback, Dispatcher dispatcher, CancellationToken exitToken) { }
    }

    public static class Helper
    {
        public static IFileSystemWatcher GetFileWatcher(string moduleName, string fileName, Action callback)
        {
            return new FileSystem().FileSystemWatcher.New();
        }

        public static string GetKeyName(uint virtualKey) => virtualKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

namespace Common.UI
{
    public static class ThemeHelpers
    {
        public static void SetAppTheme() { }
    }
}

namespace PowerToys.GPOWrapperProjection
{
    public enum GpoRuleConfigured
    {
        Unavailable = 0,
        Enabled = 1,
        Disabled = 2,
    }

    public static class GPOWrapper
    {
        public static GpoRuleConfigured GetConfiguredColorPickerEnabledValue() => GpoRuleConfigured.Enabled;
    }
}

namespace Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate
{
}

namespace Microsoft.PowerToys.Telemetry
{
    public enum PartA_PrivTags
    {
        ProductAndServiceUsage,
    }

    public sealed class ETWTrace : IDisposable
    {
        public void Dispose() { }
    }

    public sealed class PowerToysTelemetry
    {
        public static PowerToysTelemetry Log { get; } = new PowerToysTelemetry();

        public void WriteEvent(Events.IEvent telemetryEvent) { }
    }
}

namespace Microsoft.PowerToys.Telemetry.Events
{
    public interface IEvent
    {
    }

    public abstract class EventBase
    {
        public string EventName { get; set; }
    }
}

namespace Microsoft.PowerToys.Settings.UI.Library.Enumerations
{
    public enum ColorRepresentationType
    {
        HEX,
        RGB,
        HSL,
        HSV,
        CMYK,
        HSB,
        HSI,
        HWB,
        NCol,
        CIEXYZ,
        CIELAB,
        Oklab,
        Oklch,
        VEC4,
    }

    public enum ColorPickerActivationAction
    {
        OpenColorPicker,
        OpenEditor,
    }

    public enum ColorPickerClickAction
    {
        PickColorThenEditor,
        PickColorAndClose,
        Close,
    }
}

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public static class SettingsDeepLink
    {
        public enum SettingsWindow
        {
            ColorPicker,
        }

        public static void OpenSettings(SettingsWindow window)
        {
            Stage2SettingsWindow.ShowForCurrentContainer();
        }
    }
}

namespace ColorPicker.Helpers
{
    internal static class SettingsDeepLink
    {
        public enum SettingsWindow
        {
            ColorPicker,
        }

        public static void OpenSettings(SettingsWindow window)
        {
            Stage2SettingsWindow.ShowForCurrentContainer();
        }
    }
}
