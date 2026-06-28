#include "HdrColorMath.h"
#include "HdrFormat.h"

#include <cmath>
#include <iostream>
#include <stdexcept>
#include <string>

namespace
{
void Require(bool condition, char const* message)
{
    if (!condition)
    {
        throw std::runtime_error(message);
    }
}

void RequireNear(double actual, double expected, double tolerance, char const* message)
{
    if (std::abs(actual - expected) > tolerance)
    {
        std::cerr << message << ": actual=" << actual << " expected=" << expected << "\n";
        throw std::runtime_error(message);
    }
}

hdr::HdrColorSample MakeSample(hdr::LinearRgb rgb)
{
    hdr::HdrColorSample sample{};
    sample.Status = hdr::HdrSampleStatus::Ok;
    sample.Linear = rgb;
    sample.Alpha = 1.0;
    sample.Derived = hdr::DeriveColor(rgb, 1.0);
    sample.HasHdrData = true;
    return sample;
}

void TestSampleSizes()
{
    for (const int size : hdr::SupportedSampleSizes)
    {
        Require(hdr::IsSupportedSampleSize(size), "supported sample size rejected");
    }

    Require(!hdr::IsSupportedSampleSize(2), "unsupported sample size accepted");
    Require(!hdr::IsSupportedSampleSize(0), "zero sample size accepted");
}

void TestNitsAndY()
{
    const auto derived = hdr::DeriveColor({ 2.0, 1.0, 0.5 });
    RequireNear(derived.RgbNits.R, 160.0, 0.000001, "red nits");
    RequireNear(derived.RgbNits.G, 80.0, 0.000001, "green nits");
    RequireNear(derived.RgbNits.B, 40.0, 0.000001, "blue nits");
    RequireNear(derived.YNits, ((0.2126 * 2.0) + 0.7152 + (0.0722 * 0.5)) * 80.0, 0.000001, "Y nits");
}

void TestSdrProjection()
{
    auto black = hdr::ProjectLinearRgbToSdr({ 0.0, 0.0, 0.0 });
    Require(black.R == 0 && black.G == 0 && black.B == 0, "black projection");

    auto white = hdr::ProjectLinearRgbToSdr({ 1.0, 1.0, 1.0 });
    Require(white.R == 255 && white.G == 255 && white.B == 255, "white projection");

    auto hdrWhite = hdr::ProjectLinearRgbToSdr({ 4.0, 2.0, 1.5 });
    Require(hdrWhite.R == 255 && hdrWhite.G == 255 && hdrWhite.B == 255, "HDR clamp projection");

    auto negative = hdr::ProjectLinearRgbToSdr({ -0.1, -0.001, 0.0 });
    Require(negative.R == 0 && negative.G == 0 && negative.B == 0, "negative clamp projection");

    auto middle = hdr::ProjectLinearRgbToSdr({ 0.5, 0.5, 0.5 });
    Require(middle.R == 188 && middle.G == 188 && middle.B == 188, "sRGB OETF projection");
}

void TestFormatTokens()
{
    const auto sample = MakeSample({ 1.5, 1.0, 0.5 });
    const auto text = hdr::FormatHdrSample(sample, L"linear RGB(%Lr, %Lg, %Lb) Y=%Ny I10=%Ic", 4);
    Require(text.find(L"linear RGB(1.5000, 1.0000, 0.5000)") != std::wstring::npos, "linear token formatting");
    Require(text.find(L"Y=85.6160") != std::wstring::npos, "Y token formatting");
    Require(text.find(L"I10=") != std::wstring::npos, "I10 token formatting");
    Require(hdr::ContainsHdrToken(L"RGB nits(%Nr, %Ng, %Nb)"), "contains HDR token");
    Require(!hdr::ContainsHdrToken(L"plain text"), "false HDR token detection");
}

void TestUnavailableTokens()
{
    hdr::HdrColorSample sample{};
    sample.HasHdrData = false;
    const auto text = hdr::FormatHdrSample(sample, L"linear RGB(%Lr, %Lg, %Lb)", 4);
    Require(text == L"linear RGB(N/A, N/A, N/A)", "unavailable HDR token formatting");
}
}

int main()
{
    try
    {
        TestSampleSizes();
        TestNitsAndY();
        TestSdrProjection();
        TestFormatTokens();
        TestUnavailableTokens();
        std::cout << "HdrColorTests passed.\n";
        return 0;
    }
    catch (std::exception const& error)
    {
        std::cerr << "HdrColorTests failed: " << error.what() << "\n";
        return 1;
    }
}
