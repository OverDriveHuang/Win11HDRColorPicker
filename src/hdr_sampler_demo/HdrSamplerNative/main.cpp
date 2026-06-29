#include "HdrSampler.h"

#include <objbase.h>
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
};

static hdr::HdrSampler g_sampler;
static bool g_borderlessAccessChecked = false;
static bool g_borderlessAllowed = false;
static bool g_activeCapture = false;
static HdrNativeDiagnostics g_diagnostics{ 0, 0, 0, 0, 0, -3, 0, 0, 0, 0 };

static hdr::HdrCaptureCapabilities RefreshCapabilities()
{
    const auto capabilities = g_sampler.GetCapabilities();
    g_diagnostics.WgcSupported = capabilities.WgcSupported ? 1 : 0;
    g_diagnostics.CreateFreeThreadedSupported = capabilities.CreateFreeThreadedSupported ? 1 : 0;
    g_diagnostics.BorderlessSupported = capabilities.BorderlessSupported ? 1 : 0;
    g_diagnostics.BorderlessAccessChecked = g_borderlessAccessChecked ? 1 : 0;
    g_diagnostics.BorderlessAllowed = g_borderlessAllowed ? 1 : 0;
    g_diagnostics.ActiveCapture = g_activeCapture ? 1 : 0;
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

        const auto capabilities = RefreshCapabilities();
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

        RefreshCapabilities();
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
        return -2;
    }
}
}
