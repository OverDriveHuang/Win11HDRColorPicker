#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "HdrColorMath.h"

#include <windows.h>

#include <string>

namespace hdr
{
enum class HdrSampleStatus
{
    Ok,
    WgcUnsupported,
    BorderlessDenied,
    MonitorUnavailable,
    FrameTimeout,
    CaptureFormatUnsupported,
    DeviceLost,
    CaptureFailed,
};

struct HdrSampleOptions
{
    int SampleSize = 1;
    bool RequestBorderless = true;
    int FrameTimeoutMs = 3000;
};

struct HdrCaptureCapabilities
{
    bool WgcSupported = false;
    bool CreateFreeThreadedSupported = false;
    bool BorderlessSupported = false;
};

struct HdrColorSample
{
    HdrSampleStatus Status = HdrSampleStatus::CaptureFailed;
    LinearRgb Linear;
    double Alpha = 1.0;
    DerivedColor Derived;
    int ScreenX = 0;
    int ScreenY = 0;
    int CaptureX = 0;
    int CaptureY = 0;
    int ActualWidth = 0;
    int ActualHeight = 0;
    int PixelCount = 0;
    bool HasHdrData = false;
    bool BorderlessRequested = false;
    bool BorderlessUsed = false;
    std::wstring StatusMessage;
};

class HdrSampler
{
public:
    HdrSampler();
    ~HdrSampler();

    HdrCaptureCapabilities GetCapabilities();
    bool RequestBorderlessAccess();
    HdrColorSample SampleAtCursor(HdrSampleOptions options);
    void CloseCapture();

private:
    struct Impl;
    Impl* m_impl = nullptr;
};

const wchar_t* ToString(HdrSampleStatus status);
}
