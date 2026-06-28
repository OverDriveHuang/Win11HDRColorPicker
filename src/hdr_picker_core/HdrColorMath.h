#pragma once

#include <array>
#include <cstdint>

namespace hdr
{
constexpr double SdrWhiteNits = 80.0;

struct LinearRgb
{
    double R = 0;
    double G = 0;
    double B = 0;
};

struct IctCp
{
    double I = 0;
    double Ct = 0;
    double Cp = 0;
};

struct SdrColor
{
    std::uint8_t R = 0;
    std::uint8_t G = 0;
    std::uint8_t B = 0;
    std::uint8_t A = 255;
};

struct DerivedColor
{
    LinearRgb Linear;
    LinearRgb RgbNits;
    double YNits = 0;
    IctCp Ictcp;
    int IctcpI10 = 0;
    SdrColor Sdr;
};

constexpr std::array<int, 7> SupportedSampleSizes{ 1, 3, 5, 11, 31, 51, 101 };

bool IsSupportedSampleSize(int value);
double Clamp(double value, double low, double high);
double Rec709Y(LinearRgb linearRgb);
IctCp ConvertScRgb709ToICtCpBt2100Pq(LinearRgb linearRgb);
SdrColor ProjectLinearRgbToSdr(LinearRgb linearRgb, double alpha = 1.0);
DerivedColor DeriveColor(LinearRgb linearRgb, double alpha = 1.0);
int IctcpITo10BitCode(double i);
}
