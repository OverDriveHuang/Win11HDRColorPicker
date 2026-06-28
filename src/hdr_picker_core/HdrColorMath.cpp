#include "HdrColorMath.h"

#include <algorithm>
#include <cmath>

namespace hdr
{
bool IsSupportedSampleSize(int value)
{
    return std::find(SupportedSampleSizes.begin(), SupportedSampleSizes.end(), value) != SupportedSampleSizes.end();
}

double Clamp(double value, double low, double high)
{
    return std::min(std::max(value, low), high);
}

double Rec709Y(LinearRgb linearRgb)
{
    return (0.2126 * linearRgb.R) + (0.7152 * linearRgb.G) + (0.0722 * linearRgb.B);
}

namespace
{
double PqEncode(double normalizedLuminance)
{
    const double m1 = 2610.0 / 16384.0;
    const double m2 = 2523.0 / 32.0;
    const double c1 = 3424.0 / 4096.0;
    const double c2 = 2413.0 / 128.0;
    const double c3 = 2392.0 / 128.0;

    const double x = std::pow(Clamp(normalizedLuminance, 0.0, 1.0), m1);
    return std::pow((c1 + (c2 * x)) / (1.0 + (c3 * x)), m2);
}

std::uint8_t LinearChannelToSdrByte(double linear)
{
    const double c = Clamp(linear, 0.0, 1.0);
    const double srgb = c <= 0.0031308 ? (12.92 * c) : ((1.055 * std::pow(c, 1.0 / 2.4)) - 0.055);
    return static_cast<std::uint8_t>(std::lround(Clamp(srgb, 0.0, 1.0) * 255.0));
}
}

IctCp ConvertScRgb709ToICtCpBt2100Pq(LinearRgb linearRgb)
{
    const double x =
        (0.41239079926595948 * linearRgb.R) +
        (0.35758433938387796 * linearRgb.G) +
        (0.18048078840183429 * linearRgb.B);
    const double y =
        (0.21263900587151036 * linearRgb.R) +
        (0.71516867876775593 * linearRgb.G) +
        (0.07219231536073372 * linearRgb.B);
    const double z =
        (0.01933081871559185 * linearRgb.R) +
        (0.11919477979462599 * linearRgb.G) +
        (0.95053215224966058 * linearRgb.B);

    const double bt2020R = (1.7166511879712674 * x) - (0.35567078377639233 * y) - (0.25336628137365974 * z);
    const double bt2020G = (-0.6666843518324890 * x) + (1.6164812366349395 * y) + (0.01576854581391113 * z);
    const double bt2020B = (0.017639857445310783 * x) - (0.042770613257808524 * y) + (0.9421031212354738 * z);

    const double r = Clamp((bt2020R * SdrWhiteNits) / 10000.0, 0.0, 1.0);
    const double g = Clamp((bt2020G * SdrWhiteNits) / 10000.0, 0.0, 1.0);
    const double b = Clamp((bt2020B * SdrWhiteNits) / 10000.0, 0.0, 1.0);

    const double l = ((1688.0 * r) + (2146.0 * g) + (262.0 * b)) / 4096.0;
    const double m = ((683.0 * r) + (2951.0 * g) + (462.0 * b)) / 4096.0;
    const double s = ((99.0 * r) + (309.0 * g) + (3688.0 * b)) / 4096.0;

    const double lp = PqEncode(l);
    const double mp = PqEncode(m);
    const double sp = PqEncode(s);

    return {
        (0.5 * lp) + (0.5 * mp),
        ((6610.0 * lp) - (13613.0 * mp) + (7003.0 * sp)) / 4096.0,
        ((17933.0 * lp) - (17390.0 * mp) - (543.0 * sp)) / 4096.0,
    };
}

SdrColor ProjectLinearRgbToSdr(LinearRgb linearRgb, double alpha)
{
    return {
        LinearChannelToSdrByte(linearRgb.R),
        LinearChannelToSdrByte(linearRgb.G),
        LinearChannelToSdrByte(linearRgb.B),
        static_cast<std::uint8_t>(std::lround(Clamp(alpha, 0.0, 1.0) * 255.0)),
    };
}

DerivedColor DeriveColor(LinearRgb linearRgb, double alpha)
{
    const auto ictcp = ConvertScRgb709ToICtCpBt2100Pq(linearRgb);
    return {
        linearRgb,
        { linearRgb.R * SdrWhiteNits, linearRgb.G * SdrWhiteNits, linearRgb.B * SdrWhiteNits },
        Rec709Y(linearRgb) * SdrWhiteNits,
        ictcp,
        IctcpITo10BitCode(ictcp.I),
        ProjectLinearRgbToSdr(linearRgb, alpha),
    };
}

int IctcpITo10BitCode(double i)
{
    return static_cast<int>(std::lround(Clamp(i, 0.0, 1.0) * 1023.0));
}
}
