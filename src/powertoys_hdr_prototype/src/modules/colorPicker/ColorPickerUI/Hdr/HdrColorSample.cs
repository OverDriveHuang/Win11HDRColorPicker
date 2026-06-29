// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// HDR prototype additions live in this copy of PowerToys Color Picker.

using System.Drawing;

namespace ColorPicker.Hdr
{
    public sealed class HdrColorSample
    {
        public bool HasHdrData { get; init; }

        public double LinearR { get; init; }

        public double LinearG { get; init; }

        public double LinearB { get; init; }

        public double NitsR { get; init; }

        public double NitsG { get; init; }

        public double NitsB { get; init; }

        public double YNits { get; init; }

        public double IctcpI { get; init; }

        public double IctcpCt { get; init; }

        public double IctcpCp { get; init; }

        public int IctcpI10 { get; init; }

        public Color SdrColor { get; init; } = Color.Transparent;

        public bool BorderlessRequested { get; init; }

        public bool BorderlessUsed { get; init; }
    }
}
