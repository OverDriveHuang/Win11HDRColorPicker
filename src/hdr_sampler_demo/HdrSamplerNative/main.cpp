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
};

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

        static hdr::HdrSampler sampler;
        static bool borderlessRequested = false;
        if (requestBorderless && !borderlessRequested)
        {
            sampler.RequestBorderlessAccess();
            borderlessRequested = true;
        }

        const auto sample = sampler.SampleAtCursor({ sampleSize, requestBorderless != 0, 1000 });
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
        return output->Status;
    }
    catch (...)
    {
        output->Status = -2;
        output->HasHdrData = 0;
        return -2;
    }
}
}
