# HDR Sampler Demo

这个目录包含 HDR 采样诊断 demo、focused tests 和给 PowerToys ColorPickerUI 原型使用的 native bridge。

原始目标：验证 Windows 11 上是否可以通过 `Windows.Graphics.Capture` 读取鼠标下方像素的 `R16G16B16A16_FLOAT` display-referred linear RGB 值，并输出超过 SDR 白点的 `>1.0` 数据。

当前结构：

- `../hdr_picker_core/`：可复用 HDR sampler core、颜色数学和格式 token。
- `HdrSamplerDemo/`：console 诊断客户端。
- `HdrColorTests/`：无屏幕依赖的 focused tests。
- `HdrSamplerNative/`：导出 `HdrSampler_SampleAtCursor` 的 native DLL，用于 PowerToys C# 原型调用。
- `HdrPickerApp/`：早期错误方向实验，已从 solution 构建移除，不作为产品路径。

## 输出

程序输出四组需求定义的名称：

- `linear RGB`
- `RGB nits`
- `Y nits`
- `ICtCp`

默认保留小数点后四位。可用 `--precision N` 调整控制台输出精度。

`1.0 = 80 nits` 固定写死。`ICtCp` 定义为 BT.2100-PQ。

取样区域支持 `1x1`、`3x3`、`5x5`、`11x11`、`31x31`、`51x51`、`101x101`。程序先平均区域内的 linear RGB，再计算 nits 和 ICtCp。

## 构建要求

当前机器已经安装 Visual Studio 2022 Build Tools、MSVC 和 Windows SDK 10.0.26100。本目录已完成本机 `x64|Release` 编译验证。

如果换到新机器，在 Windows 11 上需要：

1. 安装 Visual Studio 2022 或 Build Tools，包含 C++ desktop workload。
2. 确认可以还原 NuGet native packages。
3. 打开 `HdrSamplerDemo.sln`。
4. 还原 packages 后构建 `x64|Release`。

本项目提供脚本：

```powershell
.\scripts\check-env.ps1
.\scripts\restore-packages.ps1
.\scripts\build-demo.ps1
```

也可以在 Developer PowerShell 中手动执行：

```powershell
nuget restore .\HdrSamplerDemo.sln
msbuild .\HdrSamplerDemo.sln /p:Configuration=Release /p:Platform=x64
```

构建后主要产物：

```powershell
.\x64\Release\HdrSamplerDemo.exe
.\x64\Release\HdrColorTests.exe
.\x64\Release\HdrSamplerNative.dll
```

运行测试：

```powershell
.\x64\Release\HdrColorTests.exe
```

## 运行

```powershell
.\x64\Release\HdrSamplerDemo.exe --once
.\x64\Release\HdrSamplerDemo.exe --samples 20 --interval-ms 250 --precision 4 --sample-size 11
.\x64\Release\HdrSamplerDemo.exe --samples 0 --interval-ms 250 --sample-size 1 --precision 4 --borderless
```

参数：

- `--once`：采样一次后退出。
- `--samples N`：采样 N 次；`0` 表示持续采样。
- `--interval-ms N`：循环采样间隔，默认 250 ms。
- `--precision N`：输出小数位，默认 4。
- `--sample-size N`：平均取样边长，默认 1；支持 1、3、5、11、31、51、101。
- `--borderless`：请求 WGC borderless 权限，并设置 `GraphicsCaptureSession.IsBorderRequired(false)`，用于减少或消除捕获边框闪烁。

诊断 WGC 捕获路径时可以启用 trace：

```powershell
$env:HDR_SAMPLER_TRACE = "1"
.\x64\Release\HdrSamplerDemo.exe --once --sample-size 1
Remove-Item Env:HDR_SAMPLER_TRACE
```

## PowerToys 原型

正确产品路径在：

```text
..\powertoys_hdr_prototype\
```

该副本保留 PowerToys 原来的 Color Picker UI、点击动作、历史和 Color Editor，只增加 HDR sample model、HDR token formatter、以及对 `HdrSamplerNative.dll` 的调用。

## 当前限制

- PowerToys C# 原型当前需要 .NET SDK 才能构建；本机当前 `dotnet` 缺少 SDK，MSBuild 也找不到 `Microsoft.NET.Sdk`。
- 当前只验证了 native bridge、console demo 和 focused tests。
- 当前只实现 WGC + `R16G16B16A16Float` 路径。实测已经能读取 `linear RGB > 1.0` 的 HDR 数据；如果后续机型发现 WGC 被限制，再做 `IDXGIOutput5::DuplicateOutput1` demo。
- ICtCp 转换将 scRGB/P709 转 BT.2020 后进入 PQ，PQ 输入会 clamp 到 `0..1`，避免负值或超 10000 nits 破坏计算。
