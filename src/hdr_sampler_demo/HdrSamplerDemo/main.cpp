#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "HdrFormat.h"
#include "HdrSampler.h"

#include <windows.h>

#include <winrt/base.h>

#include <algorithm>
#include <cstdlib>
#include <exception>
#include <iostream>
#include <string>
#include <string_view>
#include <thread>

namespace
{
struct Options
{
    int Precision = 4;
    int IntervalMs = 250;
    int Samples = 0;
    int SampleSize = 1;
    bool Borderless = false;
};

Options ParseOptions(int argc, wchar_t** argv)
{
    Options options;
    for (int i = 1; i < argc; ++i)
    {
        const std::wstring_view arg = argv[i];
        auto requireValue = [&]() -> int
        {
            if (i + 1 >= argc)
            {
                throw std::runtime_error("Missing value for command-line option.");
            }

            ++i;
            return std::stoi(argv[i]);
        };

        if (arg == L"--once")
        {
            options.Samples = 1;
        }
        else if (arg == L"--samples")
        {
            options.Samples = requireValue();
        }
        else if (arg == L"--interval-ms")
        {
            options.IntervalMs = requireValue();
        }
        else if (arg == L"--precision")
        {
            options.Precision = requireValue();
        }
        else if (arg == L"--sample-size")
        {
            options.SampleSize = requireValue();
        }
        else if (arg == L"--borderless")
        {
            options.Borderless = true;
        }
        else if (arg == L"--help" || arg == L"-h")
        {
            std::wcout
                << L"HdrSamplerDemo --once | --samples N [--interval-ms N] [--precision N] [--sample-size N] [--borderless]\n"
                << L"Default: --samples 0 --interval-ms 250 --precision 4 --sample-size 1\n"
                << L"Supported sample sizes: 1, 3, 5, 11, 31, 51, 101\n"
                << L"--borderless requests WGC borderless access and sets IsBorderRequired(false).\n";
            std::exit(0);
        }
        else
        {
            throw std::runtime_error("Unknown command-line option.");
        }
    }

    options.Precision = std::clamp(options.Precision, 0, 9);
    options.IntervalMs = std::max(options.IntervalMs, 16);
    options.Samples = std::max(options.Samples, 0);
    if (!hdr::IsSupportedSampleSize(options.SampleSize))
    {
        throw std::runtime_error("Unsupported --sample-size. Use 1, 3, 5, 11, 31, 51, or 101.");
    }

    return options;
}

void PrintSample(hdr::HdrColorSample const& sample, int requestedSampleSize, int precision)
{
    std::wcout
        << L"status: " << hdr::ToString(sample.Status) << L" " << sample.StatusMessage << L"\n"
        << L"cursor: screen=(" << sample.ScreenX << L"," << sample.ScreenY << L")"
        << L" capture=(" << sample.CaptureX << L"," << sample.CaptureY << L")\n"
        << L"sample: requested=" << requestedSampleSize << L"x" << requestedSampleSize
        << L" actual=" << sample.ActualWidth << L"x" << sample.ActualHeight
        << L" pixels=" << sample.PixelCount << L"\n"
        << hdr::FormatDefaultOutput(sample, precision) << L"\n"
        << L"SDR projection: R=" << static_cast<int>(sample.Derived.Sdr.R)
        << L" G=" << static_cast<int>(sample.Derived.Sdr.G)
        << L" B=" << static_cast<int>(sample.Derived.Sdr.B)
        << L" A=" << static_cast<int>(sample.Derived.Sdr.A)
        << L"\n"
        << std::endl;
}
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        winrt::init_apartment(winrt::apartment_type::multi_threaded);

        const auto options = ParseOptions(argc, argv);
        hdr::HdrSampler sampler;
        if (options.Borderless)
        {
            const bool allowed = sampler.RequestBorderlessAccess();
            std::wcerr << L"Borderless capture access: " << (allowed ? L"Allowed" : L"Not allowed") << L"\n";
        }

        int sampleIndex = 0;
        while (options.Samples == 0 || sampleIndex < options.Samples)
        {
            const auto sample = sampler.SampleAtCursor({
                options.SampleSize,
                options.Borderless,
                3000,
            });
            PrintSample(sample, options.SampleSize, options.Precision);

            ++sampleIndex;
            if (sample.Status != hdr::HdrSampleStatus::Ok)
            {
                return 1;
            }

            if (options.Samples == 0 || sampleIndex < options.Samples)
            {
                std::this_thread::sleep_for(std::chrono::milliseconds(options.IntervalMs));
            }
        }

        return 0;
    }
    catch (winrt::hresult_error const& error)
    {
        std::wcerr << L"HRESULT 0x" << std::hex << static_cast<std::uint32_t>(error.code()) << L": " << error.message().c_str() << L"\n";
        return 1;
    }
    catch (std::exception const& error)
    {
        std::cerr << "Error: " << error.what() << "\n";
        return 1;
    }
}
