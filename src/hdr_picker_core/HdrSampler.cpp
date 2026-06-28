#include "HdrSampler.h"

#include <d3d11_4.h>
#include <dxgi1_6.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <winrt/Windows.Security.Authorization.AppCapabilityAccess.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <iterator>
#include <mutex>
#include <stdexcept>

namespace capture = winrt::Windows::Graphics::Capture;
namespace directx = winrt::Windows::Graphics::DirectX;
namespace d3d11 = winrt::Windows::Graphics::DirectX::Direct3D11;
namespace appcap = winrt::Windows::Security::Authorization::AppCapabilityAccess;

namespace hdr
{
namespace
{
struct MonitorSampleTarget
{
    HMONITOR Monitor = nullptr;
    RECT MonitorRect{};
    POINT Cursor{};
    int X = 0;
    int Y = 0;
};

template <typename T>
winrt::com_ptr<T> GetDXGIInterfaceFromObject(winrt::Windows::Foundation::IInspectable const& object)
{
    auto access = object.as<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<T> result;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<T>(), result.put_void()));
    return result;
}

float HalfToFloat(std::uint16_t half)
{
    const std::uint32_t sign = (static_cast<std::uint32_t>(half & 0x8000)) << 16;
    std::uint32_t exponent = (half >> 10) & 0x1f;
    std::uint32_t mantissa = half & 0x03ff;
    std::uint32_t bits = 0;

    if (exponent == 0)
    {
        if (mantissa == 0)
        {
            bits = sign;
        }
        else
        {
            exponent = 1;
            while ((mantissa & 0x0400) == 0)
            {
                mantissa <<= 1;
                --exponent;
            }

            mantissa &= 0x03ff;
            const std::uint32_t floatExponent = exponent + (127 - 15);
            bits = sign | (floatExponent << 23) | (mantissa << 13);
        }
    }
    else if (exponent == 0x1f)
    {
        bits = sign | 0x7f800000 | (mantissa << 13);
    }
    else
    {
        const std::uint32_t floatExponent = exponent + (127 - 15);
        bits = sign | (floatExponent << 23) | (mantissa << 13);
    }

    float value = 0;
    std::memcpy(&value, &bits, sizeof(value));
    return value;
}

d3d11::IDirect3DDevice CreateWinRtD3DDevice(winrt::com_ptr<ID3D11Device> const& device)
{
    auto dxgiDevice = device.as<IDXGIDevice>();
    winrt::com_ptr<::IInspectable> inspectable;
    winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.get(), inspectable.put()));
    return inspectable.as<d3d11::IDirect3DDevice>();
}

winrt::com_ptr<ID3D11Device> CreateD3D11Device()
{
    constexpr D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
    };

    winrt::com_ptr<ID3D11Device> device;
    winrt::com_ptr<ID3D11DeviceContext> context;
    D3D_FEATURE_LEVEL actualLevel = D3D_FEATURE_LEVEL_11_0;

    winrt::check_hresult(D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        featureLevels,
        static_cast<UINT>(std::size(featureLevels)),
        D3D11_SDK_VERSION,
        device.put(),
        &actualLevel,
        context.put()));

    return device;
}

capture::GraphicsCaptureItem CreateItemForMonitor(HMONITOR monitor)
{
    auto interopFactory = winrt::get_activation_factory<capture::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    capture::GraphicsCaptureItem item{ nullptr };
    winrt::check_hresult(interopFactory->CreateForMonitor(
        monitor,
        winrt::guid_of<capture::GraphicsCaptureItem>(),
        winrt::put_abi(item)));
    return item;
}

MonitorSampleTarget GetCurrentMonitorTarget(capture::GraphicsCaptureItem const& item)
{
    POINT cursor{};
    if (!GetCursorPos(&cursor))
    {
        throw std::runtime_error("GetCursorPos failed.");
    }

    const HMONITOR monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
    if (!monitor)
    {
        throw std::runtime_error("MonitorFromPoint failed.");
    }

    MONITORINFO monitorInfo{};
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (!GetMonitorInfoW(monitor, &monitorInfo))
    {
        throw std::runtime_error("GetMonitorInfoW failed.");
    }

    const auto itemSize = item.Size();
    const int monitorWidth = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
    const int monitorHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
    const double scaleX = static_cast<double>(itemSize.Width) / static_cast<double>(monitorWidth);
    const double scaleY = static_cast<double>(itemSize.Height) / static_cast<double>(monitorHeight);

    const int x = static_cast<int>(std::floor((cursor.x - monitorInfo.rcMonitor.left) * scaleX));
    const int y = static_cast<int>(std::floor((cursor.y - monitorInfo.rcMonitor.top) * scaleY));

    return {
        monitor,
        monitorInfo.rcMonitor,
        cursor,
        std::clamp(x, 0, itemSize.Width - 1),
        std::clamp(y, 0, itemSize.Height - 1),
    };
}
}

struct HdrSampler::Impl
{
    winrt::com_ptr<ID3D11Device> D3DDevice;
    d3d11::IDirect3DDevice WinRtDevice{ nullptr };

    void EnsureDevice()
    {
        if (!D3DDevice)
        {
            D3DDevice = CreateD3D11Device();
            WinRtDevice = CreateWinRtD3DDevice(D3DDevice);
        }
    }
};

HdrSampler::HdrSampler() :
    m_impl(new Impl())
{
}

HdrSampler::~HdrSampler()
{
    delete m_impl;
}

bool HdrSampler::RequestBorderlessAccess()
{
    const auto status = capture::GraphicsCaptureAccess::RequestAccessAsync(capture::GraphicsCaptureAccessKind::Borderless).get();
    return status == appcap::AppCapabilityAccessStatus::Allowed;
}

HdrColorSample HdrSampler::SampleAtCursor(HdrSampleOptions options)
{
    HdrColorSample result{};
    result.Status = HdrSampleStatus::CaptureFailed;

    if (!IsSupportedSampleSize(options.SampleSize))
    {
        result.StatusMessage = L"Unsupported sample size.";
        return result;
    }

    if (!capture::GraphicsCaptureSession::IsSupported())
    {
        result.Status = HdrSampleStatus::WgcUnsupported;
        result.StatusMessage = L"Windows.Graphics.Capture is not supported.";
        return result;
    }

    try
    {
        m_impl->EnsureDevice();

        POINT cursor{};
        if (!GetCursorPos(&cursor))
        {
            result.Status = HdrSampleStatus::MonitorUnavailable;
            result.StatusMessage = L"GetCursorPos failed.";
            return result;
        }

        const HMONITOR monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
        if (!monitor)
        {
            result.Status = HdrSampleStatus::MonitorUnavailable;
            result.StatusMessage = L"MonitorFromPoint failed.";
            return result;
        }

        auto item = CreateItemForMonitor(monitor);
        auto target = GetCurrentMonitorTarget(item);
        result.ScreenX = target.Cursor.x;
        result.ScreenY = target.Cursor.y;
        result.CaptureX = target.X;
        result.CaptureY = target.Y;

        auto framePool = capture::Direct3D11CaptureFramePool::CreateFreeThreaded(
            m_impl->WinRtDevice,
            directx::DirectXPixelFormat::R16G16B16A16Float,
            1,
            item.Size());
        auto session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled(false);
        if (options.RequestBorderless)
        {
            session.IsBorderRequired(false);
        }

        winrt::handle frameEvent{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        if (!frameEvent)
        {
            throw std::runtime_error("CreateEventW failed.");
        }

        std::mutex frameMutex;
        capture::Direct3D11CaptureFrame capturedFrame{ nullptr };
        auto revoker = framePool.FrameArrived(winrt::auto_revoke, [&](capture::Direct3D11CaptureFramePool const& sender, winrt::Windows::Foundation::IInspectable const&)
        {
            auto frame = sender.TryGetNextFrame();
            if (frame)
            {
                std::scoped_lock lock(frameMutex);
                if (!capturedFrame)
                {
                    capturedFrame = frame;
                    SetEvent(frameEvent.get());
                }
            }
        });

        session.StartCapture();
        const DWORD waitResult = WaitForSingleObject(frameEvent.get(), static_cast<DWORD>(std::max(options.FrameTimeoutMs, 1)));
        if (waitResult != WAIT_OBJECT_0)
        {
            revoker.revoke();
            session.Close();
            framePool.Close();
            result.Status = HdrSampleStatus::FrameTimeout;
            result.StatusMessage = L"Timed out waiting for a WGC frame.";
            return result;
        }

        capture::Direct3D11CaptureFrame frame{ nullptr };
        {
            std::scoped_lock lock(frameMutex);
            frame = capturedFrame;
        }

        auto sourceTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>(frame.Surface());
        D3D11_TEXTURE2D_DESC sourceDesc{};
        sourceTexture->GetDesc(&sourceDesc);
        if (sourceDesc.Format != DXGI_FORMAT_R16G16B16A16_FLOAT)
        {
            revoker.revoke();
            session.Close();
            framePool.Close();
            result.Status = HdrSampleStatus::CaptureFormatUnsupported;
            result.StatusMessage = L"Captured texture format was not R16G16B16A16_FLOAT.";
            return result;
        }

        const int halfSize = options.SampleSize / 2;
        const int left = std::max(0, target.X - halfSize);
        const int top = std::max(0, target.Y - halfSize);
        const int right = std::min(static_cast<int>(sourceDesc.Width), target.X + halfSize + 1);
        const int bottom = std::min(static_cast<int>(sourceDesc.Height), target.Y + halfSize + 1);
        const int sampleWidth = std::max(1, right - left);
        const int sampleHeight = std::max(1, bottom - top);

        D3D11_TEXTURE2D_DESC stagingDesc = sourceDesc;
        stagingDesc.Width = static_cast<UINT>(sampleWidth);
        stagingDesc.Height = static_cast<UINT>(sampleHeight);
        stagingDesc.MipLevels = 1;
        stagingDesc.ArraySize = 1;
        stagingDesc.BindFlags = 0;
        stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        stagingDesc.MiscFlags = 0;
        stagingDesc.Usage = D3D11_USAGE_STAGING;

        winrt::com_ptr<ID3D11Texture2D> stagingTexture;
        winrt::check_hresult(m_impl->D3DDevice->CreateTexture2D(&stagingDesc, nullptr, stagingTexture.put()));

        winrt::com_ptr<ID3D11DeviceContext> context;
        m_impl->D3DDevice->GetImmediateContext(context.put());

        D3D11_BOX box{};
        box.left = static_cast<UINT>(left);
        box.top = static_cast<UINT>(top);
        box.front = 0;
        box.right = static_cast<UINT>(right);
        box.bottom = static_cast<UINT>(bottom);
        box.back = 1;

        context->CopySubresourceRegion(stagingTexture.get(), 0, 0, 0, 0, sourceTexture.get(), 0, &box);

        D3D11_MAPPED_SUBRESOURCE mapped{};
        winrt::check_hresult(context->Map(stagingTexture.get(), 0, D3D11_MAP_READ, 0, &mapped));

        double rSum = 0;
        double gSum = 0;
        double bSum = 0;
        double aSum = 0;
        int pixelCount = 0;
        const auto base = static_cast<const std::byte*>(mapped.pData);
        for (int y = 0; y < sampleHeight; ++y)
        {
            const auto row = reinterpret_cast<const std::uint16_t*>(base + (static_cast<size_t>(mapped.RowPitch) * y));
            for (int x = 0; x < sampleWidth; ++x)
            {
                const auto pixel = row + (x * 4);
                rSum += static_cast<double>(HalfToFloat(pixel[0]));
                gSum += static_cast<double>(HalfToFloat(pixel[1]));
                bSum += static_cast<double>(HalfToFloat(pixel[2]));
                aSum += static_cast<double>(HalfToFloat(pixel[3]));
                ++pixelCount;
            }
        }

        context->Unmap(stagingTexture.get(), 0);

        sourceTexture = nullptr;
        frame = nullptr;
        capturedFrame = nullptr;
        revoker.revoke();
        session.Close();
        framePool.Close();

        result.Status = HdrSampleStatus::Ok;
        result.Linear = {
            rSum / static_cast<double>(pixelCount),
            gSum / static_cast<double>(pixelCount),
            bSum / static_cast<double>(pixelCount),
        };
        result.Alpha = aSum / static_cast<double>(pixelCount);
        result.Derived = DeriveColor(result.Linear, result.Alpha);
        result.ActualWidth = sampleWidth;
        result.ActualHeight = sampleHeight;
        result.PixelCount = pixelCount;
        result.HasHdrData = true;
        result.StatusMessage = L"Ok";
        return result;
    }
    catch (winrt::hresult_error const& error)
    {
        result.Status = HdrSampleStatus::DeviceLost;
        result.StatusMessage = error.message().c_str();
        m_impl->D3DDevice = nullptr;
        m_impl->WinRtDevice = nullptr;
        return result;
    }
    catch (std::exception const& error)
    {
        result.Status = HdrSampleStatus::CaptureFailed;
        result.StatusMessage.assign(error.what(), error.what() + std::strlen(error.what()));
        return result;
    }
}

const wchar_t* ToString(HdrSampleStatus status)
{
    switch (status)
    {
    case HdrSampleStatus::Ok:
        return L"Ok";
    case HdrSampleStatus::WgcUnsupported:
        return L"WgcUnsupported";
    case HdrSampleStatus::BorderlessDenied:
        return L"BorderlessDenied";
    case HdrSampleStatus::MonitorUnavailable:
        return L"MonitorUnavailable";
    case HdrSampleStatus::FrameTimeout:
        return L"FrameTimeout";
    case HdrSampleStatus::CaptureFormatUnsupported:
        return L"CaptureFormatUnsupported";
    case HdrSampleStatus::DeviceLost:
        return L"DeviceLost";
    case HdrSampleStatus::CaptureFailed:
    default:
        return L"CaptureFailed";
    }
}
}
