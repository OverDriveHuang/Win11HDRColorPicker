#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "HdrFormat.h"
#include "HdrSampler.h"

#include <windows.h>
#include <windowsx.h>

#include <winrt/base.h>

#include <array>
#include <cstring>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

namespace
{
constexpr int HotkeyId = 100;
constexpr UINT TimerId = 200;
constexpr int TimerMs = 250;
constexpr int OverlayWidth = 420;
constexpr int OverlayHeight = 150;

enum ControlId
{
    IdModifierWin = 1000,
    IdModifierCtrl,
    IdModifierAlt,
    IdModifierShift,
    IdKeyCombo,
    IdApplyHotkey,
    IdSampleSize,
    IdFormatLinear,
    IdFormatRgbNits,
    IdFormatYNits,
    IdFormatIctcp,
    IdCustomFormat,
    IdCopy,
    IdToggle,
    IdOutput,
    IdStatus,
    IdHelp,
};

struct AppState
{
    std::unique_ptr<hdr::HdrSampler> Sampler;
    hdr::HdrColorSample CurrentSample;
    bool HasCurrentSample = false;
    bool PickerActive = false;
    bool BorderlessAllowed = false;
    HHOOK MouseHook = nullptr;
    HWND Window = nullptr;
    HWND Overlay = nullptr;
    HWND Output = nullptr;
    HWND Status = nullptr;
    HWND CustomFormat = nullptr;
    HWND SampleSize = nullptr;
    HWND KeyCombo = nullptr;
    std::array<HWND, 4> FormatChecks{};
    std::array<HWND, 4> ModifierChecks{};
    std::wstring OverlayText;
};

AppState* g_state = nullptr;

std::wstring GetWindowTextString(HWND hwnd)
{
    const int length = GetWindowTextLengthW(hwnd);
    std::wstring text(static_cast<size_t>(length), L'\0');
    if (length > 0)
    {
        GetWindowTextW(hwnd, text.data(), length + 1);
    }

    return text;
}

void SetStatus(AppState& state, std::wstring const& text)
{
    SetWindowTextW(state.Status, text.c_str());
}

int SelectedSampleSize(AppState& state)
{
    const int index = static_cast<int>(SendMessageW(state.SampleSize, CB_GETCURSEL, 0, 0));
    if (index == CB_ERR)
    {
        return 1;
    }

    return static_cast<int>(SendMessageW(state.SampleSize, CB_GETITEMDATA, static_cast<WPARAM>(index), 0));
}

std::wstring BuildCopyText(AppState& state)
{
    if (!state.HasCurrentSample)
    {
        return L"";
    }

    const auto formats = hdr::DefaultHdrFormats();
    std::vector<std::wstring> lines;
    for (size_t i = 0; i < formats.size(); ++i)
    {
        if (Button_GetCheck(state.FormatChecks[i]) == BST_CHECKED)
        {
            lines.push_back(hdr::FormatHdrSample(state.CurrentSample, formats[i].Format, 4));
        }
    }

    const auto custom = GetWindowTextString(state.CustomFormat);
    if (!custom.empty())
    {
        lines.push_back(hdr::FormatHdrSample(state.CurrentSample, custom, 4));
    }

    std::wstring output;
    for (auto const& line : lines)
    {
        if (!output.empty())
        {
            output += L"\r\n";
        }

        output += line;
    }

    return output;
}

void CopyText(HWND owner, std::wstring const& text)
{
    if (text.empty() || !OpenClipboard(owner))
    {
        return;
    }

    EmptyClipboard();
    const size_t bytes = (text.size() + 1) * sizeof(wchar_t);
    HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE, bytes);
    if (memory)
    {
        void* buffer = GlobalLock(memory);
        if (buffer)
        {
            memcpy(buffer, text.c_str(), bytes);
            GlobalUnlock(memory);
            SetClipboardData(CF_UNICODETEXT, memory);
            memory = nullptr;
        }
    }

    if (memory)
    {
        GlobalFree(memory);
    }

    CloseClipboard();
}

void CopyCurrent(AppState& state)
{
    const auto text = BuildCopyText(state);
    CopyText(state.Window, text);
    if (!text.empty())
    {
        SetStatus(state, L"Copied current HDR color output.");
    }
}

void PositionOverlay(AppState& state)
{
    if (!state.Overlay || !state.HasCurrentSample)
    {
        return;
    }

    const int margin = 24;
    int x = state.CurrentSample.ScreenX + margin;
    int y = state.CurrentSample.ScreenY + margin;

    HMONITOR monitor = MonitorFromPoint({ state.CurrentSample.ScreenX, state.CurrentSample.ScreenY }, MONITOR_DEFAULTTONEAREST);
    MONITORINFO monitorInfo{};
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (monitor && GetMonitorInfoW(monitor, &monitorInfo))
    {
        if (x + OverlayWidth > monitorInfo.rcMonitor.right)
        {
            x = state.CurrentSample.ScreenX - OverlayWidth - margin;
        }

        if (y + OverlayHeight > monitorInfo.rcMonitor.bottom)
        {
            y = state.CurrentSample.ScreenY - OverlayHeight - margin;
        }

        x = std::max(monitorInfo.rcMonitor.left, x);
        y = std::max(monitorInfo.rcMonitor.top, y);
    }

    SetWindowPos(state.Overlay, HWND_TOPMOST, x, y, OverlayWidth, OverlayHeight, SWP_NOACTIVATE | SWP_SHOWWINDOW);
}

void RefreshSample(AppState& state)
{
    const int sampleSize = SelectedSampleSize(state);
    state.CurrentSample = state.Sampler->SampleAtCursor({
        sampleSize,
        true,
        1000,
    });
    state.HasCurrentSample = true;

    std::wostringstream status;
    status << L"Status: " << hdr::ToString(state.CurrentSample.Status)
           << L" | sample " << sampleSize << L"x" << sampleSize
           << L" actual " << state.CurrentSample.ActualWidth << L"x" << state.CurrentSample.ActualHeight
           << L" | screen (" << state.CurrentSample.ScreenX << L"," << state.CurrentSample.ScreenY << L")"
           << L" | borderless " << (state.BorderlessAllowed ? L"allowed" : L"not allowed");
    SetStatus(state, status.str());

    const auto text = BuildCopyText(state);
    state.OverlayText = text;
    SetWindowTextW(state.Output, text.c_str());
    PositionOverlay(state);
    if (state.Overlay)
    {
        InvalidateRect(state.Overlay, nullptr, TRUE);
    }
}

void SetPickerActive(AppState& state, bool active)
{
    state.PickerActive = active;
    if (active)
    {
        SetTimer(state.Window, TimerId, TimerMs, nullptr);
        SetWindowTextW(GetDlgItem(state.Window, IdToggle), L"Stop Picker");
        if (state.Overlay)
        {
            ShowWindow(state.Overlay, SW_SHOWNOACTIVATE);
        }
        RefreshSample(state);
    }
    else
    {
        KillTimer(state.Window, TimerId);
        if (state.Overlay)
        {
            ShowWindow(state.Overlay, SW_HIDE);
        }
        SetWindowTextW(GetDlgItem(state.Window, IdToggle), L"Start Picker");
        SetStatus(state, L"Picker stopped.");
    }
}

UINT ModifierFlags(AppState& state)
{
    UINT modifiers = MOD_NOREPEAT;
    if (Button_GetCheck(state.ModifierChecks[0]) == BST_CHECKED)
    {
        modifiers |= MOD_WIN;
    }
    if (Button_GetCheck(state.ModifierChecks[1]) == BST_CHECKED)
    {
        modifiers |= MOD_CONTROL;
    }
    if (Button_GetCheck(state.ModifierChecks[2]) == BST_CHECKED)
    {
        modifiers |= MOD_ALT;
    }
    if (Button_GetCheck(state.ModifierChecks[3]) == BST_CHECKED)
    {
        modifiers |= MOD_SHIFT;
    }

    return modifiers;
}

UINT SelectedKey(AppState& state)
{
    const int index = static_cast<int>(SendMessageW(state.KeyCombo, CB_GETCURSEL, 0, 0));
    if (index == CB_ERR)
    {
        return 'C';
    }

    return static_cast<UINT>(SendMessageW(state.KeyCombo, CB_GETITEMDATA, static_cast<WPARAM>(index), 0));
}

void ApplyHotkey(AppState& state)
{
    UnregisterHotKey(state.Window, HotkeyId);
    if (RegisterHotKey(state.Window, HotkeyId, ModifierFlags(state), SelectedKey(state)))
    {
        SetStatus(state, L"Activation shortcut applied.");
    }
    else
    {
        SetStatus(state, L"Activation shortcut registration failed.");
    }
}

LRESULT CALLBACK MouseHookProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (code >= 0 && g_state && g_state->PickerActive && wParam == WM_LBUTTONDOWN)
    {
        RefreshSample(*g_state);
        CopyCurrent(*g_state);
        SetPickerActive(*g_state, false);
        return 1;
    }

    return CallNextHookEx(g_state ? g_state->MouseHook : nullptr, code, wParam, lParam);
}

LRESULT CALLBACK OverlayProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    auto* state = reinterpret_cast<AppState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (message)
    {
    case WM_NCCREATE:
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams));
        return TRUE;
    case WM_ERASEBKGND:
        return 1;
    case WM_PAINT:
    {
        PAINTSTRUCT ps{};
        HDC dc = BeginPaint(hwnd, &ps);
        RECT rect{};
        GetClientRect(hwnd, &rect);

        HBRUSH background = CreateSolidBrush(RGB(32, 32, 32));
        FillRect(dc, &rect, background);
        DeleteObject(background);

        RECT swatch{ 14, 14, 78, 78 };
        COLORREF swatchColor = RGB(64, 64, 64);
        if (state && state->HasCurrentSample)
        {
            swatchColor = RGB(state->CurrentSample.Derived.Sdr.R, state->CurrentSample.Derived.Sdr.G, state->CurrentSample.Derived.Sdr.B);
        }

        HBRUSH swatchBrush = CreateSolidBrush(swatchColor);
        FillRect(dc, &swatch, swatchBrush);
        DeleteObject(swatchBrush);
        FrameRect(dc, &swatch, static_cast<HBRUSH>(GetStockObject(WHITE_BRUSH)));

        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, RGB(245, 245, 245));
        HFONT font = CreateFontW(-15, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
        HGDIOBJ oldFont = SelectObject(dc, font);

        RECT textRect{ 94, 12, OverlayWidth - 14, OverlayHeight - 14 };
        const wchar_t* text = state && !state->OverlayText.empty() ? state->OverlayText.c_str() : L"Sampling...";
        DrawTextW(dc, text, -1, &textRect, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_END_ELLIPSIS);

        SelectObject(dc, oldFont);
        DeleteObject(font);
        EndPaint(hwnd, &ps);
        return 0;
    }
    default:
        break;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}

HWND AddControl(HWND parent, wchar_t const* cls, wchar_t const* text, DWORD style, int id, int x, int y, int w, int h)
{
    return CreateWindowExW(
        0,
        cls,
        text,
        WS_CHILD | WS_VISIBLE | style,
        x,
        y,
        w,
        h,
        parent,
        reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)),
        GetModuleHandleW(nullptr),
        nullptr);
}

void PopulateControls(AppState& state)
{
    AddControl(state.Window, L"STATIC", L"Activation shortcut", 0, -1, 16, 14, 160, 22);
    state.ModifierChecks[0] = AddControl(state.Window, L"BUTTON", L"Win", BS_AUTOCHECKBOX, IdModifierWin, 16, 40, 58, 22);
    state.ModifierChecks[1] = AddControl(state.Window, L"BUTTON", L"Ctrl", BS_AUTOCHECKBOX, IdModifierCtrl, 80, 40, 58, 22);
    state.ModifierChecks[2] = AddControl(state.Window, L"BUTTON", L"Alt", BS_AUTOCHECKBOX, IdModifierAlt, 144, 40, 58, 22);
    state.ModifierChecks[3] = AddControl(state.Window, L"BUTTON", L"Shift", BS_AUTOCHECKBOX, IdModifierShift, 208, 40, 70, 22);
    Button_SetCheck(state.ModifierChecks[0], BST_CHECKED);
    Button_SetCheck(state.ModifierChecks[3], BST_CHECKED);

    state.KeyCombo = AddControl(state.Window, L"COMBOBOX", L"", CBS_DROPDOWNLIST, IdKeyCombo, 286, 38, 80, 120);
    for (wchar_t key : { L'C', L'H', L'P', L'K' })
    {
        wchar_t label[] = { key, 0 };
        const int index = static_cast<int>(SendMessageW(state.KeyCombo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(label)));
        SendMessageW(state.KeyCombo, CB_SETITEMDATA, static_cast<WPARAM>(index), static_cast<LPARAM>(key));
    }
    SendMessageW(state.KeyCombo, CB_SETCURSEL, 0, 0);
    AddControl(state.Window, L"BUTTON", L"Apply", BS_PUSHBUTTON, IdApplyHotkey, 376, 37, 74, 26);

    AddControl(state.Window, L"STATIC", L"Sample size", 0, -1, 16, 78, 120, 22);
    state.SampleSize = AddControl(state.Window, L"COMBOBOX", L"", CBS_DROPDOWNLIST, IdSampleSize, 118, 75, 110, 160);
    for (const int size : hdr::SupportedSampleSizes)
    {
        const std::wstring label = std::to_wstring(size) + L"x" + std::to_wstring(size);
        const int index = static_cast<int>(SendMessageW(state.SampleSize, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(label.c_str())));
        SendMessageW(state.SampleSize, CB_SETITEMDATA, static_cast<WPARAM>(index), static_cast<LPARAM>(size));
    }
    SendMessageW(state.SampleSize, CB_SETCURSEL, 0, 0);

    AddControl(state.Window, L"BUTTON", L"Start Picker", BS_PUSHBUTTON, IdToggle, 244, 74, 100, 28);
    AddControl(state.Window, L"BUTTON", L"Copy", BS_PUSHBUTTON, IdCopy, 354, 74, 96, 28);

    AddControl(state.Window, L"STATIC", L"Visible formats", 0, -1, 16, 120, 160, 22);
    const auto formats = hdr::DefaultHdrFormats();
    constexpr std::array<int, 4> formatIds{ IdFormatLinear, IdFormatRgbNits, IdFormatYNits, IdFormatIctcp };
    for (size_t i = 0; i < formats.size(); ++i)
    {
        state.FormatChecks[i] = AddControl(
            state.Window,
            L"BUTTON",
            formats[i].Name.c_str(),
            BS_AUTOCHECKBOX,
            formatIds[i],
            16 + static_cast<int>(i % 2) * 160,
            146 + static_cast<int>(i / 2) * 26,
            150,
            24);
        Button_SetCheck(state.FormatChecks[i], BST_CHECKED);
    }

    AddControl(state.Window, L"STATIC", L"Custom format", 0, -1, 16, 212, 150, 22);
    state.CustomFormat = AddControl(
        state.Window,
        L"EDIT",
        L"",
        WS_BORDER | ES_AUTOHSCROLL,
        IdCustomFormat,
        16,
        238,
        434,
        24);

    state.Output = AddControl(
        state.Window,
        L"EDIT",
        L"",
        WS_BORDER | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_VSCROLL,
        IdOutput,
        16,
        276,
        434,
        130);
    SetWindowTextW(state.Output, L"启动取色后，当前值会显示在鼠标旁边的 PowerToys-style 悬浮面板中；这里保留最近一次复制文本。");

    AddControl(
        state.Window,
        L"EDIT",
        hdr::FormatTokenHelp().c_str(),
        WS_BORDER | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_VSCROLL,
        IdHelp,
        16,
        416,
        434,
        120);

    state.Status = AddControl(state.Window, L"STATIC", L"Ready.", 0, IdStatus, 16, 548, 434, 42);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    auto* state = reinterpret_cast<AppState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (message)
    {
    case WM_CREATE:
    {
        auto* createdState = reinterpret_cast<AppState*>(reinterpret_cast<CREATESTRUCTW*>(lParam)->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(createdState));
        createdState->Window = hwnd;
        PopulateControls(*createdState);
        ApplyHotkey(*createdState);
        return 0;
    }
    case WM_HOTKEY:
        if (state && wParam == HotkeyId)
        {
            SetPickerActive(*state, !state->PickerActive);
        }
        return 0;
    case WM_TIMER:
        if (state && wParam == TimerId && state->PickerActive)
        {
            RefreshSample(*state);
        }
        return 0;
    case WM_COMMAND:
        if (state)
        {
            switch (LOWORD(wParam))
            {
            case IdApplyHotkey:
                ApplyHotkey(*state);
                return 0;
            case IdCopy:
                RefreshSample(*state);
                CopyCurrent(*state);
                return 0;
            case IdToggle:
                SetPickerActive(*state, !state->PickerActive);
                return 0;
            case IdSampleSize:
            case IdFormatLinear:
            case IdFormatRgbNits:
            case IdFormatYNits:
            case IdFormatIctcp:
            case IdCustomFormat:
                if (state->HasCurrentSample)
                {
                    SetWindowTextW(state->Output, BuildCopyText(*state).c_str());
                }
                return 0;
            default:
                break;
            }
        }
        break;
    case WM_DESTROY:
        if (state)
        {
            SetPickerActive(*state, false);
            UnregisterHotKey(hwnd, HotkeyId);
            if (state->MouseHook)
            {
                UnhookWindowsHookEx(state->MouseHook);
                state->MouseHook = nullptr;
            }
        }
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int showCommand)
{
    winrt::init_apartment(winrt::apartment_type::multi_threaded);
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    AppState state{};
    state.Sampler = std::make_unique<hdr::HdrSampler>();
    state.BorderlessAllowed = state.Sampler->RequestBorderlessAccess();
    g_state = &state;
    state.MouseHook = SetWindowsHookExW(WH_MOUSE_LL, MouseHookProc, instance, 0);

    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = WndProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = L"HdrPickerAppWindow";
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    RegisterClassW(&windowClass);

    WNDCLASSW overlayClass{};
    overlayClass.lpfnWndProc = OverlayProc;
    overlayClass.hInstance = instance;
    overlayClass.lpszClassName = L"HdrPickerOverlayWindow";
    overlayClass.hCursor = LoadCursorW(nullptr, IDC_CROSS);
    overlayClass.hbrBackground = reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
    RegisterClassW(&overlayClass);

    HWND window = CreateWindowExW(
        WS_EX_TOPMOST,
        windowClass.lpszClassName,
        L"HDR Picker MVP",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        486,
        640,
        nullptr,
        nullptr,
        instance,
        &state);

    if (!window)
    {
        return 1;
    }

    state.Overlay = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        overlayClass.lpszClassName,
        L"HDR Picker Overlay",
        WS_POPUP,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        OverlayWidth,
        OverlayHeight,
        nullptr,
        nullptr,
        instance,
        &state);

    ShowWindow(window, showCommand);
    UpdateWindow(window);

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0))
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    g_state = nullptr;
    return static_cast<int>(message.wParam);
}
