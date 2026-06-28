// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// HDR prototype additions live in this copy of PowerToys Color Picker.

using System;
using System.Globalization;

namespace ColorPicker.Hdr
{
    public static class HdrFormatHelper
    {
        public static bool ContainsHdrToken(string format)
            => !string.IsNullOrEmpty(format) &&
               (format.Contains("%Lr", StringComparison.Ordinal) ||
                format.Contains("%Lg", StringComparison.Ordinal) ||
                format.Contains("%Lb", StringComparison.Ordinal) ||
                format.Contains("%Nr", StringComparison.Ordinal) ||
                format.Contains("%Ng", StringComparison.Ordinal) ||
                format.Contains("%Nb", StringComparison.Ordinal) ||
                format.Contains("%Ny", StringComparison.Ordinal) ||
                format.Contains("%Ii", StringComparison.Ordinal) ||
                format.Contains("%Ic", StringComparison.Ordinal) ||
                format.Contains("%Ct", StringComparison.Ordinal) ||
                format.Contains("%Cp", StringComparison.Ordinal));

        public static string ReplaceHdrTokens(string format, HdrColorSample sample)
        {
            if (string.IsNullOrEmpty(format))
            {
                return format;
            }

            string valueOrNa(double value) => sample?.HasHdrData == true ? CleanZero(value).ToString("0.0000", CultureInfo.InvariantCulture) : "N/A";
            string intOrNa(int value) => sample?.HasHdrData == true ? value.ToString(CultureInfo.InvariantCulture) : "N/A";

            return format
                .Replace("%Lr", valueOrNa(sample?.LinearR ?? 0), StringComparison.Ordinal)
                .Replace("%Lg", valueOrNa(sample?.LinearG ?? 0), StringComparison.Ordinal)
                .Replace("%Lb", valueOrNa(sample?.LinearB ?? 0), StringComparison.Ordinal)
                .Replace("%Nr", valueOrNa(sample?.NitsR ?? 0), StringComparison.Ordinal)
                .Replace("%Ng", valueOrNa(sample?.NitsG ?? 0), StringComparison.Ordinal)
                .Replace("%Nb", valueOrNa(sample?.NitsB ?? 0), StringComparison.Ordinal)
                .Replace("%Ny", valueOrNa(sample?.YNits ?? 0), StringComparison.Ordinal)
                .Replace("%Ii", valueOrNa(sample?.IctcpI ?? 0), StringComparison.Ordinal)
                .Replace("%Ic", intOrNa(sample?.IctcpI10 ?? 0), StringComparison.Ordinal)
                .Replace("%Ct", valueOrNa(sample?.IctcpCt ?? 0), StringComparison.Ordinal)
                .Replace("%Cp", valueOrNa(sample?.IctcpCp ?? 0), StringComparison.Ordinal);
        }

        private static double CleanZero(double value)
            => Math.Abs(value) < 0.0000005 ? 0 : value;
    }
}
