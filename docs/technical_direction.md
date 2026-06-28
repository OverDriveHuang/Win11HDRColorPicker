# 技术方向

## 已确认事实

PowerToys Color Picker 当前吸管路径不是 HDR-aware。`MouseInfoProvider.GetPixelColor` 使用 `Graphics.CopyFromScreen` 把 1x1 屏幕区域复制到 `Format32bppArgb` bitmap，然后 `GetPixel` 返回 `System.Drawing.Color`。放大窗口也使用同类 `CopyFromScreen`。后续格式化链路以 byte RGB 为核心，`ColorFormatHelper` 的 `%Re/%Gr/%Bl` 等 token 最终读取 `Color.R/G/B`。

这解释了 255 截断：HDR 信息在进入 PowerToys 当前数据模型前已经丢失。

## 捕获方案候选

### 方案 A：Windows.Graphics.Capture + FP16

使用 `Windows.Graphics.Capture` 捕获显示器或桌面项，帧池使用 `DirectXPixelFormat.R16G16B16A16Float`。微软 Advanced Color 文档把它列为 HDR/WCG 屏幕捕获路径之一，并建议用 FP16 和 scRGB 避免丢失扩展颜色数据。

优势：

- WinRT/.NET 接入相对自然，PowerToys ColorPickerUI 已经导入 CsWinRT 公共 props。
- 与现代 Windows 屏幕捕获能力一致。
- 后续做独立工具或移植 PowerToys 都可用。

风险：

- 可能涉及捕获 consent / picker / interop 获取 monitor item。
- 要处理窗口排除、光标位置映射、多显示器和 DPI。
- 需要实测是否所有 HDR 内容都能以 scRGB FP16 返回。

### 方案 B：IDXGIOutput5::DuplicateOutput1 + FP16

使用 DXGI 输出复制，但调用 `IDXGIOutput5::DuplicateOutput1` 并请求 `DXGI_FORMAT_R16G16B16A16_FLOAT`。微软文档说明旧 Desktop Duplication API 的普通桌面图像格式固定为 `DXGI_FORMAT_B8G8R8A8_UNORM`，而 Advanced Color 文档将 `DuplicateOutput1` 列为可用的 HDR/WCG 捕获 API。

优势：

- 更接近传统屏幕吸管，不需要用户选取 capture item。
- 可按物理输出处理，多显示器模型清晰。

风险：

- C#/WPF 接入复杂度更高，可能需要 C++/WinRT 或 native helper。
- 需要处理 GPU texture staging、同步和错误恢复。
- 要实测驱动和 HDR 桌面下的真实返回格式与色彩空间。

### 方案 C：继续使用 GDI/Bitmap

不适合本项目。它只能保留现有 SDR byte 行为，可作为 fallback 或对照测试。

## 当前推荐路线

先做独立原型，按下面顺序验证：

1. 用 `Windows.Graphics.Capture` + `R16G16B16A16Float` 读取鼠标下方像素，确认 HDR 测试块能返回 `>1.0`。
2. 如果 WGC 的交互/权限/延迟不适合吸管，再做 `DuplicateOutput1` 原型对照。
3. 把成功路径封装为 `HdrScreenSampler`，输出一个 HDR-aware sample model。
4. 再把 sample model 接到 PowerToys 的格式化和设置模型。验证 demo 只证明读数能力，不代表最终 UI。

## 数据模型建议

新增一个不依赖 `System.Drawing.Color` 的数据结构：

```text
HdrColorSample
- float R, G, B, A                    // display-referred linear RGB
- ColorSpaceKind SourceColorSpace     // expected: scRGB / linear P709 first
- bool HasHdrData
- byte SdrR, SdrG, SdrB               // legacy preview / SDR fallback
- double SdrWhiteNits                 // fixed 80 by requirement
- CaptureBackend Backend              // WGC, DuplicateOutput1, GDI fallback
```

现有 SDR 格式继续用 `System.Drawing.Color` 或 byte projection；HDR 格式读取 `HdrColorSample`。

## 取样平均

取样器应支持 Photoshop-like average sampling。首版尺寸列表：

```text
1x1, 3x3, 5x5, 11x11, 31x31, 51x51, 101x101
```

平均必须发生在 display-referred linear RGB 阶段：

```text
sampled pixels -> average linear RGB -> RGB nits / Y nits / ICtCp
```

不要逐像素计算 ICtCp 后再平均，因为 ICtCp/PQ 是非线性转换，顺序会改变结果。

在屏幕边缘时，取样区域可以裁剪到可用像素范围，并在验证程序中显示实际参与平均的像素数；最终产品 UI 是否显示该细节后续再定，但计算必须基于实际读取到的像素。

## UI 集成约束

最终集成应保持 PowerToys Color Picker 原版 UI/UX。实现重点在捕获、数据模型和格式 token，不在重新设计界面。

允许改动：

- 在现有 `VisibleColorFormats` 机制里新增默认隐藏的 HDR 格式项或允许用户自定义 HDR token。
- 在现有“Add custom color format”帮助说明中补充 HDR 参数。
- 保证更长的 HDR 输出字符串在现有控件中不溢出、不遮挡。

不建议改动：

- 不新增独立 HDR 面板。
- 不改变吸管主窗口的交互流程。
- 不把独立验证程序的 UI 移植回 PowerToys。

## 颜色空间和 nits

微软 Advanced Color 文档说明，在 SDR 与 HDR 同时存在的桌面合成场景，Windows 推荐用 FP16 scRGB 表示扩展颜色。DXGI color space 枚举中的 scRGB 对应 full-range RGB、linear gamma、Rec.709 primaries。因此第一阶段不要假设 API 直接返回 BT.2020 RGB。

推荐定义：

- `linear RGB`：捕获得到的 scRGB / linear P709 分量。
- `RGB nits = linear_channel * 80`，作为通道级显示参考值。
- `Y = 0.2126 * R + 0.7152 * G + 0.0722 * B`，使用 Rec.709 线性亮度系数。
- `Y nits = Y * 80`。
- `ICtCp`：BT.2100-PQ only。

`1.0 = 80 nits` 是固定需求，不从 Windows SDR white level 动态推导默认值。

默认输出保留四位小数。后续如果实现成本可控，可以在格式 token 中加入类似 `printf` 的精度控制；如果成本高，固定四位小数即可。

## ICtCp 转换策略

ICtCp 是 BT.2100 HDR 颜色表示，不应直接把 scRGB/P709 三原色当成 BT.2020 RGB 使用。

第一阶段输出 `ICtCp`，定义为 BT.2100-PQ：

1. scRGB / linear P709 -> XYZ，使用 Rec.709 D65 矩阵。
2. XYZ -> linear BT.2020 RGB，使用 BT.2020 D65 矩阵逆变换。
3. 将 linear BT.2020 RGB 转为绝对亮度归一化输入：`component * 80 / 10000`。
4. 按 BT.2100 的 ICtCp/PQ 流程：BT.2020 RGB -> LMS，LMS 通过 PQ 非线性，再 LMS' -> ICtCp。

如果捕获 API 或未来测试证明某个后端直接返回 BT.2020/PQ，则要给该后端单独分支，不能复用 scRGB 路径。

## PowerToys 集成点

最小改造点：

- `ColorPickerUI/Mouse/MouseInfoProvider.cs`：把 `GetPixelColor` 抽象为 sampler，返回 HDR-aware sample。
- `ColorPickerUI/ViewModels/MainViewModel.cs`：`SetColorDetails` 继续服务原有显示流程，但内部使用 HDR-aware sample 生成当前选中格式的字符串。
- `ManagedCommon/ColorFormatHelper.cs`：新增 HDR-aware overload 或新 helper，避免把 float HDR 值塞进 byte `Color`。
- `Settings.UI.Library/ColorPickerProperties.cs`：新增默认隐藏 HDR 格式，格式名固定为 `linear RGB`、`RGB nits`、`Y nits`、`ICtCp`，不改变原有默认可见 SDR 格式。
- `Settings.UI` Color Picker 格式帮助文案：只新增 HDR token 说明，不改弹窗交互。
- 单元测试：格式化、nits、Rec.709 -> BT.2020 -> ICtCp 转换、SDR fallback。

## 分发方向

当前不建议直接承诺“替换已安装 PowerToys 的原 Color Picker”。PowerToys 是整体应用和安装包，替换单个组件会受到签名、版本和更新机制影响。

更稳的路线：

1. 独立工具 MVP：先证明 HDR 数值链路。
2. PowerToys fork：验证能否只启用 Color Picker 或打包精简发行。
3. 上游 PR 或长期 fork：当行为、设置和测试足够稳定后再决定。
