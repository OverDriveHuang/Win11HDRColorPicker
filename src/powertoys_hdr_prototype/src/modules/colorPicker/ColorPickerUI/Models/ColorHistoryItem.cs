// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// HDR prototype additions live in this copy of PowerToys Color Picker.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;

using ColorPicker.Hdr;

namespace ColorPicker.Models
{
    public sealed class ColorHistoryItem
    {
        private const string Prefix = "HDR2";

        public Color Color { get; set; }

        public HdrColorSample HdrSample { get; set; }

        public bool HasGdiColor { get; set; }

        public Color GdiColor { get; set; }

        public static ColorHistoryItem FromColor(Color color, HdrColorSample hdrSample = null, Color? gdiColor = null)
            => new ColorHistoryItem
            {
                Color = color,
                HdrSample = hdrSample,
                HasGdiColor = gdiColor.HasValue,
                GdiColor = gdiColor.GetValueOrDefault(Colors.Transparent),
            };

        public static ColorHistoryItem Parse(string value)
        {
            var parts = (value ?? string.Empty).Split('|');
            if (parts.Length >= 4 && parts[0] != Prefix)
            {
                return FromColor(ParseColor(parts, 0));
            }

            if (parts.Length < 6 || parts[0] != Prefix)
            {
                return FromColor(Colors.Transparent);
            }

            var color = ParseColor(parts, 1);
            var hasHdr = ParseInt(parts[5]) != 0;
            Color? gdiColor = null;
            if (!hasHdr || parts.Length < 21)
            {
                if (parts.Length >= 11 && ParseInt(parts[6]) != 0)
                {
                    gdiColor = ParseColor(parts, 7);
                }

                return FromColor(color, null, gdiColor);
            }

            if (parts.Length >= 26 && ParseInt(parts[21]) != 0)
            {
                gdiColor = ParseColor(parts, 22);
            }

            return FromColor(
                color,
                new HdrColorSample
                {
                    HasHdrData = true,
                    LinearR = ParseDouble(parts[6]),
                    LinearG = ParseDouble(parts[7]),
                    LinearB = ParseDouble(parts[8]),
                    NitsR = ParseDouble(parts[9]),
                    NitsG = ParseDouble(parts[10]),
                    NitsB = ParseDouble(parts[11]),
                    YNits = ParseDouble(parts[12]),
                    IctcpI = ParseDouble(parts[13]),
                    IctcpCt = ParseDouble(parts[14]),
                    IctcpCp = ParseDouble(parts[15]),
                    IctcpI10 = ParseInt(parts[16]),
                    SdrColor = System.Drawing.Color.FromArgb(ParseInt(parts[20]), ParseInt(parts[17]), ParseInt(parts[18]), ParseInt(parts[19])),
                },
                gdiColor);
        }

        public string Serialize()
        {
            if (HdrSample?.HasHdrData != true)
            {
                if (HasGdiColor)
                {
                    return string.Join(
                        "|",
                        Prefix,
                        Color.A.ToString(CultureInfo.InvariantCulture),
                        Color.R.ToString(CultureInfo.InvariantCulture),
                        Color.G.ToString(CultureInfo.InvariantCulture),
                        Color.B.ToString(CultureInfo.InvariantCulture),
                        "0",
                        "1",
                        GdiColor.A.ToString(CultureInfo.InvariantCulture),
                        GdiColor.R.ToString(CultureInfo.InvariantCulture),
                        GdiColor.G.ToString(CultureInfo.InvariantCulture),
                        GdiColor.B.ToString(CultureInfo.InvariantCulture));
                }

                return string.Join(
                    "|",
                    Prefix,
                    Color.A.ToString(CultureInfo.InvariantCulture),
                    Color.R.ToString(CultureInfo.InvariantCulture),
                    Color.G.ToString(CultureInfo.InvariantCulture),
                    Color.B.ToString(CultureInfo.InvariantCulture),
                    "0");
            }

            var fields = new List<string>
            {
                Prefix,
                Color.A.ToString(CultureInfo.InvariantCulture),
                Color.R.ToString(CultureInfo.InvariantCulture),
                Color.G.ToString(CultureInfo.InvariantCulture),
                Color.B.ToString(CultureInfo.InvariantCulture),
                "1",
                FormatDouble(HdrSample.LinearR),
                FormatDouble(HdrSample.LinearG),
                FormatDouble(HdrSample.LinearB),
                FormatDouble(HdrSample.NitsR),
                FormatDouble(HdrSample.NitsG),
                FormatDouble(HdrSample.NitsB),
                FormatDouble(HdrSample.YNits),
                FormatDouble(HdrSample.IctcpI),
                FormatDouble(HdrSample.IctcpCt),
                FormatDouble(HdrSample.IctcpCp),
                HdrSample.IctcpI10.ToString(CultureInfo.InvariantCulture),
                HdrSample.SdrColor.R.ToString(CultureInfo.InvariantCulture),
                HdrSample.SdrColor.G.ToString(CultureInfo.InvariantCulture),
                HdrSample.SdrColor.B.ToString(CultureInfo.InvariantCulture),
                HdrSample.SdrColor.A.ToString(CultureInfo.InvariantCulture),
            };

            if (HasGdiColor)
            {
                fields.Add("1");
                fields.Add(GdiColor.A.ToString(CultureInfo.InvariantCulture));
                fields.Add(GdiColor.R.ToString(CultureInfo.InvariantCulture));
                fields.Add(GdiColor.G.ToString(CultureInfo.InvariantCulture));
                fields.Add(GdiColor.B.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join("|", fields);
        }

        public bool HasSameSample(ColorHistoryItem other)
        {
            if (other == null || !HasSameSdrColor(other.Color))
            {
                return false;
            }

            var hasHdr = HdrSample?.HasHdrData == true;
            var otherHasHdr = other.HdrSample?.HasHdrData == true;
            if (!hasHdr || !otherHasHdr)
            {
                return hasHdr == otherHasHdr;
            }

            return SameDisplayedDouble(HdrSample.LinearR, other.HdrSample.LinearR)
                && SameDisplayedDouble(HdrSample.LinearG, other.HdrSample.LinearG)
                && SameDisplayedDouble(HdrSample.LinearB, other.HdrSample.LinearB)
                && SameDisplayedDouble(HdrSample.NitsR, other.HdrSample.NitsR)
                && SameDisplayedDouble(HdrSample.NitsG, other.HdrSample.NitsG)
                && SameDisplayedDouble(HdrSample.NitsB, other.HdrSample.NitsB)
                && SameDisplayedDouble(HdrSample.YNits, other.HdrSample.YNits)
                && SameDisplayedDouble(HdrSample.IctcpI, other.HdrSample.IctcpI)
                && SameDisplayedDouble(HdrSample.IctcpCt, other.HdrSample.IctcpCt)
                && SameDisplayedDouble(HdrSample.IctcpCp, other.HdrSample.IctcpCp)
                && HdrSample.IctcpI10 == other.HdrSample.IctcpI10;
        }

        public bool HasSameSdrColor(Color color)
            => Color.A == color.A && Color.R == color.R && Color.G == color.G && Color.B == color.B;

        private static Color ParseColor(string[] parts, int offset)
            => new Color
            {
                A = ParseByte(parts[offset]),
                R = ParseByte(parts[offset + 1]),
                G = ParseByte(parts[offset + 2]),
                B = ParseByte(parts[offset + 3]),
            };

        private static byte ParseByte(string value)
            => byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (byte)0;

        private static int ParseInt(string value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

        private static double ParseDouble(string value)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0;

        private static string FormatDouble(double value)
            => value.ToString("R", CultureInfo.InvariantCulture);

        private static bool SameDisplayedDouble(double left, double right)
            => Math.Round(left, 4, MidpointRounding.AwayFromZero) == Math.Round(right, 4, MidpointRounding.AwayFromZero);
    }
}
