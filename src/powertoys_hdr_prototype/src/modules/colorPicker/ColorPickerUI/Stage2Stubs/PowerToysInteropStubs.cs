// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.

namespace PowerToys.Interop
{
    internal static class Constants
    {
        public static string TerminateColorPickerSharedEvent()
        {
            return "Local\\Stage2_TerminateColorPicker";
        }

        public static string ShowColorPickerSharedEvent()
        {
            return "Local\\Stage2_ShowColorPicker";
        }

        public static string ColorPickerSendSettingsTelemetryEvent()
        {
            return "Local\\Stage2_ColorPickerSendSettingsTelemetry";
        }
    }
}
