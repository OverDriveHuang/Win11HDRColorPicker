# WinHDRColorPicker

Standalone Windows 11 HDR color picker prototype based on Microsoft PowerToys Color Picker UI code, with an HDR sampling path added for Windows Graphics Capture FP16 output.

> Support scope: Windows 11 HDR only. Windows 10 is not supported for HDR/linear numeric accuracy. On some Windows 10 systems, `Windows.Graphics.Capture` FP16 readback can return SDR content at roughly 2x the expected linear value, and there is no reliable system flag for automatic correction.

## Current status

This repository contains the completed Phase 2 standalone prototype. It is not integrated back into PowerToys yet, but it keeps the PowerToys Color Picker style, color format system, palette behavior, popup picker flow, settings panel, tray entry, and configurable global shortcut.

The app window title also marks the prototype as `Windows 11 HDR only`.

## 中文说明

这是一个基于 PowerToys Color Picker UI 的 Windows 11 HDR 取色原型。它使用 `Windows.Graphics.Capture` FP16 路径读取屏幕最终显示管线中的 display-referred linear RGB，因此可以显示 SDR 白点以上的 HDR 数值、nits、Y nits 和 ICtCp。

当前正式支持范围是 Windows 11 HDR。Windows 10 不保证 HDR/linear 数值准确；部分 Windows 10 系统会把 SDR 内容读成接近 2 倍的 linear 值，而且目前没有可靠的系统标志可以自动判断和修正。

使用前建议关闭 PowerToys 自带的 Color Picker，避免全局快捷键或单实例逻辑冲突。下载请使用 GitHub Releases 中最新的 Windows x64 自包含压缩包，解压后运行 `PowerToys.ColorPickerUI.Stage2.exe`。

## Download

Download the latest self-contained Windows x64 package from GitHub Releases:

Unzip it and run:

`PowerToys.ColorPickerUI.Stage2.exe`

The package is self-contained for .NET, so it should not require a separate .NET runtime installation.

Before launching this standalone app, turn off the built-in PowerToys Color Picker feature. If PowerToys Color Picker is still enabled, the standalone app can fail to start because the global shortcut and picker instance conflict with PowerToys.

## Default popup formats

`default SDR`

`RGB = rgb(%Re, %Gr, %Bl), CIELAB = (%Lc, %Ca, %Cb), H=%Hu, S=%Sb%`

`default HDR`

`Nits = (Y=%Ny, %Nr, %Ng, %Nb), I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp`

## Source layout

- `src/powertoys_hdr_prototype/` - PowerToys-derived standalone Stage2 Color Picker UI prototype.
- `src/hdr_picker_core/` - native HDR sampling and color math core.
- `src/hdr_sampler_demo/` - native WGC FP16 sampling demo and test projects.
- `docs/requirements.md` - product requirements.
- `docs/productization_design.md` - Phase 2 design and integration notes.
- `docs/technical_direction.md` - HDR capture technical direction.

## Build

The Stage2 WPF app builds from:

`src/powertoys_hdr_prototype/src/modules/colorPicker/ColorPickerUI/ColorPickerUI.Stage2.csproj`

Example:

```powershell
dotnet build .\src\powertoys_hdr_prototype\src\modules\colorPicker\ColorPickerUI\ColorPickerUI.Stage2.csproj -c Release
```

Self-contained publish:

```powershell
dotnet publish .\src\powertoys_hdr_prototype\src\modules\colorPicker\ColorPickerUI\ColorPickerUI.Stage2.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\WinHDRColorPicker-win-x64
```

Build requirements:

- Windows 11
- .NET SDK compatible with `net10.0-windows10.0.26100.0`
- Windows SDK 10.0.26100.0 or newer
- Visual Studio Build Tools with Desktop C++ workload for native demo/core work

## License and notices

This prototype is derived from Microsoft PowerToys source code. The repository includes the PowerToys MIT license and notice file.
