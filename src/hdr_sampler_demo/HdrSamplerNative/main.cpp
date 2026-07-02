#include "HdrSampler.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cwchar>
#include <dxgi1_6.h>
#include <objbase.h>
#include <vector>
#include <winrt/base.h>

extern "C"
{
struct HdrNativeSample
{
    int Status;
    int HasHdrData;
    double LinearR;
    double LinearG;
    double LinearB;
    double NitsR;
    double NitsG;
    double NitsB;
    double YNits;
    double IctcpI;
    double IctcpCt;
    double IctcpCp;
    int IctcpI10;
    unsigned char SdrR;
    unsigned char SdrG;
    unsigned char SdrB;
    unsigned char SdrA;
    int ScreenX;
    int ScreenY;
    int CaptureX;
    int CaptureY;
    int ActualWidth;
    int ActualHeight;
    int PixelCount;
    int BorderlessRequested;
    int BorderlessUsed;
};

struct HdrNativeDiagnostics
{
    int WgcSupported;
    int CreateFreeThreadedSupported;
    int BorderlessSupported;
    int BorderlessAccessChecked;
    int BorderlessAllowed;
    int LastStatus;
    int LastHadHdrData;
    int LastBorderlessRequested;
    int LastBorderlessUsed;
    int ActiveCapture;
    int SdrWhiteLevelAvailable;
    int SdrWhiteLevelRaw;
    double SdrWhiteLevelNits;
    double SdrWhiteLevelScale;
    int DxgiOutputAvailable;
    int DxgiBitsPerColor;
    int DxgiColorSpace;
    int DxgiHdrColorSpace;
    double DxgiMinLuminance;
    double DxgiMaxLuminance;
    double DxgiMaxFullFrameLuminance;
    int AdvancedColorInfoAvailable;
    int AdvancedColorSupported;
    int AdvancedColorEnabled;
    int WideColorEnforced;
    int AdvancedColorForceDisabled;
    int AdvancedColorEncoding;
    int AdvancedColorBitsPerChannel;
    int MonitorInfoAvailable;
    int MonitorLeft;
    int MonitorTop;
    int MonitorRight;
    int MonitorBottom;
    int CursorX;
    int CursorY;
    int ComparisonAvailable;
    int ComparisonGdiAvailable;
    int ComparisonSampleSize;
    int ComparisonScreenX;
    int ComparisonScreenY;
    int ComparisonCaptureX;
    int ComparisonCaptureY;
    int ComparisonGdiActualWidth;
    int ComparisonGdiActualHeight;
    int ComparisonGdiPixelCount;
    double ComparisonWgcLinearR;
    double ComparisonWgcLinearG;
    double ComparisonWgcLinearB;
    unsigned char ComparisonWgcSdrR;
    unsigned char ComparisonWgcSdrG;
    unsigned char ComparisonWgcSdrB;
    unsigned char ComparisonGdiR;
    unsigned char ComparisonGdiG;
    unsigned char ComparisonGdiB;
    double ComparisonGdiExpectedLinearR;
    double ComparisonGdiExpectedLinearG;
    double ComparisonGdiExpectedLinearB;
    double ComparisonRatioR;
    double ComparisonRatioG;
    double ComparisonRatioB;
    int ComparisonRatioRAvailable;
    int ComparisonRatioGAvailable;
    int ComparisonRatioBAvailable;
    wchar_t MonitorDeviceName[32];
    wchar_t MonitorFriendlyName[128];
};

static hdr::HdrSampler g_sampler;
static bool g_borderlessAccessChecked = false;
static bool g_borderlessAllowed = false;
static bool g_activeCapture = false;
static HdrNativeDiagnostics g_diagnostics = []
{
    HdrNativeDiagnostics diagnostics{};
    diagnostics.LastStatus = -3;
    diagnostics.DxgiColorSpace = -1;
    diagnostics.AdvancedColorEncoding = -1;
    return diagnostics;
}();

static void ResetComparisonDiagnostics()
{
    g_diagnostics.ComparisonAvailable = 0;
    g_diagnostics.ComparisonGdiAvailable = 0;
    g_diagnostics.ComparisonSampleSize = 0;
    g_diagnostics.ComparisonScreenX = 0;
    g_diagnostics.ComparisonScreenY = 0;
    g_diagnostics.ComparisonCaptureX = 0;
    g_diagnostics.ComparisonCaptureY = 0;
    g_diagnostics.ComparisonGdiActualWidth = 0;
    g_diagnostics.ComparisonGdiActualHeight = 0;
    g_diagnostics.ComparisonGdiPixelCount = 0;
    g_diagnostics.ComparisonWgcLinearR = 0.0;
    g_diagnostics.ComparisonWgcLinearG = 0.0;
    g_diagnostics.ComparisonWgcLinearB = 0.0;
    g_diagnostics.ComparisonWgcSdrR = 0;
    g_diagnostics.ComparisonWgcSdrG = 0;
    g_diagnostics.ComparisonWgcSdrB = 0;
    g_diagnostics.ComparisonGdiR = 0;
    g_diagnostics.ComparisonGdiG = 0;
    g_diagnostics.ComparisonGdiB = 0;
    g_diagnostics.ComparisonGdiExpectedLinearR = 0.0;
    g_diagnostics.ComparisonGdiExpectedLinearG = 0.0;
    g_diagnostics.ComparisonGdiExpectedLinearB = 0.0;
    g_diagnostics.ComparisonRatioR = 0.0;
    g_diagnostics.ComparisonRatioG = 0.0;
    g_diagnostics.ComparisonRatioB = 0.0;
    g_diagnostics.ComparisonRatioRAvailable = 0;
    g_diagnostics.ComparisonRatioGAvailable = 0;
    g_diagnostics.ComparisonRatioBAvailable = 0;
}

static void ResetDisplayDiagnostics()
{
    g_diagnostics.SdrWhiteLevelAvailable = 0;
    g_diagnostics.SdrWhiteLevelRaw = 0;
    g_diagnostics.SdrWhiteLevelNits = 0.0;
    g_diagnostics.SdrWhiteLevelScale = 0.0;
    g_diagnostics.DxgiOutputAvailable = 0;
    g_diagnostics.DxgiBitsPerColor = 0;
    g_diagnostics.DxgiColorSpace = -1;
    g_diagnostics.DxgiHdrColorSpace = 0;
    g_diagnostics.DxgiMinLuminance = 0.0;
    g_diagnostics.DxgiMaxLuminance = 0.0;
    g_diagnostics.DxgiMaxFullFrameLuminance = 0.0;
    g_diagnostics.AdvancedColorInfoAvailable = 0;
    g_diagnostics.AdvancedColorSupported = 0;
    g_diagnostics.AdvancedColorEnabled = 0;
    g_diagnostics.WideColorEnforced = 0;
    g_diagnostics.AdvancedColorForceDisabled = 0;
    g_diagnostics.AdvancedColorEncoding = -1;
    g_diagnostics.AdvancedColorBitsPerChannel = 0;
    g_diagnostics.MonitorInfoAvailable = 0;
    g_diagnostics.MonitorLeft = 0;
    g_diagnostics.MonitorTop = 0;
    g_diagnostics.MonitorRight = 0;
    g_diagnostics.MonitorBottom = 0;
    g_diagnostics.CursorX = 0;
    g_diagnostics.CursorY = 0;
    g_diagnostics.MonitorDeviceName[0] = L'\0';
    g_diagnostics.MonitorFriendlyName[0] = L'\0';
}

static void RefreshDxgiOutputDiagnostics(HMONITOR monitor)
{
    winrt::com_ptr<IDXGIFactory1> factory;
    if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1), factory.put_void())))
    {
        return;
    }

    for (UINT adapterIndex = 0;; ++adapterIndex)
    {
        winrt::com_ptr<IDXGIAdapter1> adapter;
        if (factory->EnumAdapters1(adapterIndex, adapter.put()) != S_OK)
        {
            break;
        }

        for (UINT outputIndex = 0;; ++outputIndex)
        {
            winrt::com_ptr<IDXGIOutput> output;
            if (adapter->EnumOutputs(outputIndex, output.put()) != S_OK)
            {
                break;
            }

            DXGI_OUTPUT_DESC outputDesc{};
            if (FAILED(output->GetDesc(&outputDesc)) || outputDesc.Monitor != monitor)
            {
                continue;
            }

            winrt::com_ptr<IDXGIOutput6> output6;
            if (FAILED(output->QueryInterface(output6.put())))
            {
                return;
            }

            DXGI_OUTPUT_DESC1 desc1{};
            if (FAILED(output6->GetDesc1(&desc1)))
            {
                return;
            }

            g_diagnostics.DxgiOutputAvailable = 1;
            g_diagnostics.DxgiBitsPerColor = static_cast<int>(desc1.BitsPerColor);
            g_diagnostics.DxgiColorSpace = static_cast<int>(desc1.ColorSpace);
            g_diagnostics.DxgiHdrColorSpace = desc1.ColorSpace == DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020 ? 1 : 0;
            g_diagnostics.DxgiMinLuminance = static_cast<double>(desc1.MinLuminance);
            g_diagnostics.DxgiMaxLuminance = static_cast<double>(desc1.MaxLuminance);
            g_diagnostics.DxgiMaxFullFrameLuminance = static_cast<double>(desc1.MaxFullFrameLuminance);
            return;
        }
    }
}

static void RefreshDisplayDiagnostics()
{
    ResetDisplayDiagnostics();

    POINT cursor{};
    if (!GetCursorPos(&cursor))
    {
        return;
    }

    const HMONITOR monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
    if (!monitor)
    {
        return;
    }

    RefreshDxgiOutputDiagnostics(monitor);

    MONITORINFOEXW monitorInfo{};
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (!GetMonitorInfoW(monitor, &monitorInfo))
    {
        return;
    }

    g_diagnostics.MonitorInfoAvailable = 1;
    g_diagnostics.MonitorLeft = monitorInfo.rcMonitor.left;
    g_diagnostics.MonitorTop = monitorInfo.rcMonitor.top;
    g_diagnostics.MonitorRight = monitorInfo.rcMonitor.right;
    g_diagnostics.MonitorBottom = monitorInfo.rcMonitor.bottom;
    g_diagnostics.CursorX = cursor.x;
    g_diagnostics.CursorY = cursor.y;
    wcsncpy_s(g_diagnostics.MonitorDeviceName, _countof(g_diagnostics.MonitorDeviceName), monitorInfo.szDevice, _TRUNCATE);

    UINT32 pathCount = 0;
    UINT32 modeCount = 0;
    UINT32 queryFlags = QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE;
    if (GetDisplayConfigBufferSizes(queryFlags, &pathCount, &modeCount) != ERROR_SUCCESS)
    {
        queryFlags = QDC_ONLY_ACTIVE_PATHS;
        if (GetDisplayConfigBufferSizes(queryFlags, &pathCount, &modeCount) != ERROR_SUCCESS)
        {
            return;
        }
    }

    std::vector<DISPLAYCONFIG_PATH_INFO> paths(pathCount);
    std::vector<DISPLAYCONFIG_MODE_INFO> modes(modeCount);
    if (QueryDisplayConfig(queryFlags, &pathCount, paths.data(), &modeCount, modes.data(), nullptr) != ERROR_SUCCESS)
    {
        return;
    }

    for (UINT32 index = 0; index < pathCount; ++index)
    {
        DISPLAYCONFIG_SOURCE_DEVICE_NAME sourceName{};
        sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
        sourceName.header.size = sizeof(sourceName);
        sourceName.header.adapterId = paths[index].sourceInfo.adapterId;
        sourceName.header.id = paths[index].sourceInfo.id;
        if (DisplayConfigGetDeviceInfo(&sourceName.header) != ERROR_SUCCESS)
        {
            continue;
        }

        if (_wcsicmp(sourceName.viewGdiDeviceName, monitorInfo.szDevice) != 0)
        {
            continue;
        }

        DISPLAYCONFIG_TARGET_DEVICE_NAME targetName{};
        targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        targetName.header.size = sizeof(targetName);
        targetName.header.adapterId = paths[index].targetInfo.adapterId;
        targetName.header.id = paths[index].targetInfo.id;
        if (DisplayConfigGetDeviceInfo(&targetName.header) == ERROR_SUCCESS)
        {
            wcsncpy_s(
                g_diagnostics.MonitorFriendlyName,
                _countof(g_diagnostics.MonitorFriendlyName),
                targetName.monitorFriendlyDeviceName,
                _TRUNCATE);
        }

        DISPLAYCONFIG_SDR_WHITE_LEVEL sdrWhiteLevel{};
        sdrWhiteLevel.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL;
        sdrWhiteLevel.header.size = sizeof(sdrWhiteLevel);
        sdrWhiteLevel.header.adapterId = paths[index].targetInfo.adapterId;
        sdrWhiteLevel.header.id = paths[index].targetInfo.id;
        if (DisplayConfigGetDeviceInfo(&sdrWhiteLevel.header) == ERROR_SUCCESS)
        {
            g_diagnostics.SdrWhiteLevelAvailable = 1;
            g_diagnostics.SdrWhiteLevelRaw = static_cast<int>(sdrWhiteLevel.SDRWhiteLevel);
            g_diagnostics.SdrWhiteLevelScale = static_cast<double>(sdrWhiteLevel.SDRWhiteLevel) / 1000.0;
            g_diagnostics.SdrWhiteLevelNits = g_diagnostics.SdrWhiteLevelScale * 80.0;
        }

        DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO advancedColor{};
        advancedColor.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
        advancedColor.header.size = sizeof(advancedColor);
        advancedColor.header.adapterId = paths[index].targetInfo.adapterId;
        advancedColor.header.id = paths[index].targetInfo.id;
        if (DisplayConfigGetDeviceInfo(&advancedColor.header) == ERROR_SUCCESS)
        {
            g_diagnostics.AdvancedColorInfoAvailable = 1;
            g_diagnostics.AdvancedColorSupported = advancedColor.advancedColorSupported ? 1 : 0;
            g_diagnostics.AdvancedColorEnabled = advancedColor.advancedColorEnabled ? 1 : 0;
            g_diagnostics.WideColorEnforced = advancedColor.wideColorEnforced ? 1 : 0;
            g_diagnostics.AdvancedColorForceDisabled = advancedColor.advancedColorForceDisabled ? 1 : 0;
            g_diagnostics.AdvancedColorEncoding = static_cast<int>(advancedColor.colorEncoding);
            g_diagnostics.AdvancedColorBitsPerChannel = static_cast<int>(advancedColor.bitsPerColorChannel);
        }

        return;
    }
}

static hdr::HdrCaptureCapabilities RefreshCapabilities(bool refreshSdrWhiteLevel)
{
    const auto capabilities = g_sampler.GetCapabilities();
    g_diagnostics.WgcSupported = capabilities.WgcSupported ? 1 : 0;
    g_diagnostics.CreateFreeThreadedSupported = capabilities.CreateFreeThreadedSupported ? 1 : 0;
    g_diagnostics.BorderlessSupported = capabilities.BorderlessSupported ? 1 : 0;
    g_diagnostics.BorderlessAccessChecked = g_borderlessAccessChecked ? 1 : 0;
    g_diagnostics.BorderlessAllowed = g_borderlessAllowed ? 1 : 0;
    g_diagnostics.ActiveCapture = g_activeCapture ? 1 : 0;
    if (refreshSdrWhiteLevel)
    {
        RefreshDisplayDiagnostics();
    }

    return capabilities;
}

__declspec(dllexport) int HdrSampler_SampleAtCursor(int sampleSize, int requestBorderless, HdrNativeSample* output)
{
    if (output == nullptr)
    {
        return -1;
    }

    try
    {
        try
        {
            winrt::init_apartment(winrt::apartment_type::multi_threaded);
        }
        catch (winrt::hresult_error const& error)
        {
            if (error.code() != RPC_E_CHANGED_MODE)
            {
                throw;
            }
        }

        const auto capabilities = RefreshCapabilities(true);
        if (requestBorderless && capabilities.BorderlessSupported && !g_borderlessAccessChecked)
        {
            g_borderlessAllowed = g_sampler.RequestBorderlessAccess();
            g_borderlessAccessChecked = true;
        }

        const bool useBorderless = requestBorderless != 0 && capabilities.BorderlessSupported && g_borderlessAllowed;
        const auto sample = g_sampler.SampleAtCursor({ sampleSize, useBorderless, 3000 });
        output->Status = static_cast<int>(sample.Status);
        output->HasHdrData = sample.HasHdrData ? 1 : 0;
        output->LinearR = sample.Derived.Linear.R;
        output->LinearG = sample.Derived.Linear.G;
        output->LinearB = sample.Derived.Linear.B;
        output->NitsR = sample.Derived.RgbNits.R;
        output->NitsG = sample.Derived.RgbNits.G;
        output->NitsB = sample.Derived.RgbNits.B;
        output->YNits = sample.Derived.YNits;
        output->IctcpI = sample.Derived.Ictcp.I;
        output->IctcpCt = sample.Derived.Ictcp.Ct;
        output->IctcpCp = sample.Derived.Ictcp.Cp;
        output->IctcpI10 = sample.Derived.IctcpI10;
        output->SdrR = sample.Derived.Sdr.R;
        output->SdrG = sample.Derived.Sdr.G;
        output->SdrB = sample.Derived.Sdr.B;
        output->SdrA = sample.Derived.Sdr.A;
        output->ScreenX = sample.ScreenX;
        output->ScreenY = sample.ScreenY;
        output->CaptureX = sample.CaptureX;
        output->CaptureY = sample.CaptureY;
        output->ActualWidth = sample.ActualWidth;
        output->ActualHeight = sample.ActualHeight;
        output->PixelCount = sample.PixelCount;
        output->BorderlessRequested = requestBorderless != 0 ? 1 : 0;
        output->BorderlessUsed = sample.BorderlessUsed ? 1 : 0;

        ResetComparisonDiagnostics();

        g_diagnostics.BorderlessAllowed = g_borderlessAllowed ? 1 : 0;
        g_diagnostics.LastStatus = output->Status;
        g_diagnostics.LastHadHdrData = output->HasHdrData;
        g_diagnostics.LastBorderlessRequested = output->BorderlessRequested;
        g_diagnostics.LastBorderlessUsed = output->BorderlessUsed;
        g_activeCapture = output->HasHdrData != 0;
        g_diagnostics.ActiveCapture = g_activeCapture ? 1 : 0;
        return output->Status;
    }
    catch (...)
    {
        output->Status = -2;
        output->HasHdrData = 0;
        ResetComparisonDiagnostics();
        g_activeCapture = false;
        g_diagnostics.LastStatus = -2;
        g_diagnostics.LastHadHdrData = 0;
        g_diagnostics.ActiveCapture = 0;
        return -2;
    }
}

__declspec(dllexport) int HdrSampler_CloseCapture()
{
    try
    {
        g_sampler.CloseCapture();
        g_activeCapture = false;
        g_diagnostics.ActiveCapture = 0;
        return 0;
    }
    catch (...)
    {
        g_activeCapture = false;
        g_diagnostics.ActiveCapture = 0;
        return -2;
    }
}

__declspec(dllexport) int HdrSampler_GetDiagnostics(HdrNativeDiagnostics* output)
{
    if (output == nullptr)
    {
        return -1;
    }

    try
    {
        try
        {
            winrt::init_apartment(winrt::apartment_type::multi_threaded);
        }
        catch (winrt::hresult_error const& error)
        {
            if (error.code() != RPC_E_CHANGED_MODE)
            {
                throw;
            }
        }

        RefreshCapabilities(true);
        *output = g_diagnostics;
        return 0;
    }
    catch (...)
    {
        output->WgcSupported = 0;
        output->CreateFreeThreadedSupported = 0;
        output->BorderlessSupported = 0;
        output->BorderlessAccessChecked = 0;
        output->BorderlessAllowed = 0;
        output->LastStatus = -2;
        output->LastHadHdrData = 0;
        output->LastBorderlessRequested = 0;
        output->LastBorderlessUsed = 0;
        output->ActiveCapture = 0;
        output->SdrWhiteLevelAvailable = 0;
        output->SdrWhiteLevelRaw = 0;
        output->SdrWhiteLevelNits = 0.0;
        output->SdrWhiteLevelScale = 0.0;
        output->DxgiOutputAvailable = 0;
        output->DxgiBitsPerColor = 0;
        output->DxgiColorSpace = -1;
        output->DxgiHdrColorSpace = 0;
        output->DxgiMinLuminance = 0.0;
        output->DxgiMaxLuminance = 0.0;
        output->DxgiMaxFullFrameLuminance = 0.0;
        output->AdvancedColorInfoAvailable = 0;
        output->AdvancedColorSupported = 0;
        output->AdvancedColorEnabled = 0;
        output->WideColorEnforced = 0;
        output->AdvancedColorForceDisabled = 0;
        output->AdvancedColorEncoding = -1;
        output->AdvancedColorBitsPerChannel = 0;
        output->MonitorInfoAvailable = 0;
        output->MonitorLeft = 0;
        output->MonitorTop = 0;
        output->MonitorRight = 0;
        output->MonitorBottom = 0;
        output->CursorX = 0;
        output->CursorY = 0;
        output->ComparisonAvailable = 0;
        output->ComparisonGdiAvailable = 0;
        output->ComparisonSampleSize = 0;
        output->ComparisonScreenX = 0;
        output->ComparisonScreenY = 0;
        output->ComparisonCaptureX = 0;
        output->ComparisonCaptureY = 0;
        output->ComparisonGdiActualWidth = 0;
        output->ComparisonGdiActualHeight = 0;
        output->ComparisonGdiPixelCount = 0;
        output->ComparisonWgcLinearR = 0.0;
        output->ComparisonWgcLinearG = 0.0;
        output->ComparisonWgcLinearB = 0.0;
        output->ComparisonWgcSdrR = 0;
        output->ComparisonWgcSdrG = 0;
        output->ComparisonWgcSdrB = 0;
        output->ComparisonGdiR = 0;
        output->ComparisonGdiG = 0;
        output->ComparisonGdiB = 0;
        output->ComparisonGdiExpectedLinearR = 0.0;
        output->ComparisonGdiExpectedLinearG = 0.0;
        output->ComparisonGdiExpectedLinearB = 0.0;
        output->ComparisonRatioR = 0.0;
        output->ComparisonRatioG = 0.0;
        output->ComparisonRatioB = 0.0;
        output->ComparisonRatioRAvailable = 0;
        output->ComparisonRatioGAvailable = 0;
        output->ComparisonRatioBAvailable = 0;
        output->MonitorDeviceName[0] = L'\0';
        output->MonitorFriendlyName[0] = L'\0';
        return -2;
    }
}
}
