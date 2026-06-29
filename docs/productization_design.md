# 产品化技术方案

## 目标

把已验证的 WGC FP16 HDR 采样能力做成可维护的 HDR 屏幕吸管，实现完整需求：

- 读取 display-referred linear RGB，允许 `>1.0`。
- 输出 `linear RGB`、`RGB nits`、`Y nits`、`ICtCp`。
- 固定 `1.0 = 80 nits`。
- 支持 `1x1` 到 `101x101` 的平均取样。
- 平均顺序固定为先平均 linear RGB，再计算 nits 和 ICtCp。
- 如果集成 PowerToys，保持原版 Color Picker UI/UX，只增加必要格式参数和采样大小配置。
- 新增能力必须在 UI 中可见和可配置；命令行参数只允许存在于诊断 demo，不作为产品配置入口。

## 已确认技术基线

- 当前 PowerToys Color Picker 的 `MouseInfoProvider.GetPixelColor` 走 GDI `Graphics.CopyFromScreen` + `Bitmap.GetPixel`，数据进入系统时已经是 `System.Drawing.Color` byte RGB，因此 HDR 被截断到 SDR/255。
- WGC `Direct3D11CaptureFramePool` 使用 `DirectXPixelFormat.R16G16B16A16Float` 能在用户 HDR 屏幕上读到 `linear RGB > 1.0`。
- `GraphicsCaptureAccessKind.Borderless` + `GraphicsCaptureSession.IsBorderRequired(false)` 已实测可消除捕获边框闪烁。
- 捕获得到的数据按 Windows Advanced Color 路径处理为 scRGB / linear Rec.709 display-referred RGB；不要把它直接当 BT.2020 RGB。

## 总体路线

采用分层实现，避免把 PowerToys UI、HDR 捕获和色彩计算混在一起：

```text
产品 UI / PowerToys 集成
        |
格式系统和设置模型
        |
HDR sample model + conversion
        |
WGC borderless FP16 capture core
        |
Windows 11 D3D11 / WGC
```

第一阶段做独立 HDR picker 原型，第二阶段把同一套核心接入 PowerToys Color Picker。

原因：

- 已验证的 demo 是 C++/WinRT/D3D11，抽成 native core 成本最低。
- PowerToys Color Picker 当前是 C# WPF + Settings WinUI，直接在 C# 里重写 WGC/D3D readback 风险更高。
- 独立工具可先验证热键、捕获生命周期、格式输出和平均采样，不被 PowerToys 全仓构建和打包复杂度拖住。
- 后续接入 PowerToys 时，UI 改动可以保持最小。

## 模块设计

### 1. Native HDR capture core

建议新建 reusable native 模块，例如：

```text
src/hdr_picker_core/
```

职责：

- 创建 D3D11 device。
- 请求 WGC borderless 权限。
- 基于 HMONITOR 创建 `GraphicsCaptureItem`。
- 维护 `Direct3D11CaptureFramePool` 和 `GraphicsCaptureSession`。
- 使用 `R16G16B16A16Float` 获取 FP16 scRGB frame。
- 将屏幕坐标映射到 capture item 坐标。
- 读取鼠标中心的 `NxN` 区域到 staging texture。
- 平均 linear RGB。
- 返回纯数据结构，不依赖 UI。

建议接口：

```cpp
enum class HdrSampleStatus
{
    Ok,
    WgcUnsupported,
    BorderlessDenied,
    MonitorUnavailable,
    FrameTimeout,
    CaptureFormatUnsupported,
    DeviceLost,
    SdrFallbackOnly,
};

struct HdrSampleOptions
{
    int sampleSize;          // 1, 3, 5, 11, 31, 51, 101
    bool requestBorderless;  // default true
};

struct HdrColorSample
{
    HdrSampleStatus status;
    float r;
    float g;
    float b;
    float a;
    int screenX;
    int screenY;
    int captureX;
    int captureY;
    int actualWidth;
    int actualHeight;
    int pixelCount;
    bool hasHdrData;
    unsigned char sdrR;
    unsigned char sdrG;
    unsigned char sdrB;
    unsigned char sdrA;
};
```

PowerToys / C# 调用时提供 C ABI wrapper，避免 C++ ABI 跨边界问题：

```cpp
extern "C" __declspec(dllexport)
int HdrSampler_Create(HdrSamplerHandle** handle);

extern "C" __declspec(dllexport)
int HdrSampler_SampleAtCursor(
    HdrSamplerHandle* handle,
    HdrSampleOptions options,
    HdrColorSample* sample);

extern "C" __declspec(dllexport)
void HdrSampler_Destroy(HdrSamplerHandle* handle);
```

实现注意：

- 产品中不要像 demo 那样每次采样都新建 session。应在 picker 激活时创建 session，在 picker 关闭时释放。
- 鼠标跨显示器时，切换到新 HMONITOR 并重建对应 session。
- `IsCursorCaptureEnabled(false)`，避免吸管测到自己的光标。
- `IsBorderRequired(false)`，并在启动阶段请求 `GraphicsCaptureAccessKind::Borderless`。
- 如果 borderless 被拒绝，继续可用但通过 status 明确标记；最终 UI 不伪装为完全正常。

### 2. HDR color model and conversion

建议新建共享计算模块，例如：

```text
src/hdr_color_math/
```

或作为 `hdr_picker_core` 的无平台依赖子模块。

输入为平均后的 scRGB / linear Rec.709：

```text
linear RGB = (R, G, B)
```

输出：

```text
RGB nits = (R * 80, G * 80, B * 80)
Y = 0.2126 * R + 0.7152 * G + 0.0722 * B
Y nits = Y * 80
```

ICtCp 定义：

1. scRGB / linear Rec.709 -> XYZ。
2. XYZ -> linear BT.2020 RGB。
3. 转为 PQ 绝对亮度归一输入：`component * 80 / 10000`。
4. BT.2100 PQ ICtCp：BT.2020 RGB -> LMS -> PQ -> ICtCp。

处理策略：

- `linear RGB` 和 `RGB nits` 保留真实捕获值，包括 `>1.0`。
- 极小负值允许来自 scRGB/滤波/合成误差；格式化时把 `-0.0000` 归零。
- PQ 输入在 ICtCp 路径中 clamp 到 `0..1`，避免负亮度或超过 10000 nits 破坏 PQ 计算。
- 不做 HLG。
- 不从 Windows SDR white slider 推导 nits 基准；固定 80。

### 3. Legacy SDR compatibility

PowerToys 原有格式不应该长期依赖第二套读屏接口。首选方案是：

```text
WGC FP16 linear RGB sample
        |
        +--> HDR outputs: linear RGB / RGB nits / Y nits / ICtCp
        |
        +--> SDR projection: System.Drawing.Color-compatible byte RGB
```

也就是说，同一个屏幕坐标只读一次，HDR 和旧 SDR 格式共享同一个采样结果。这样可以避免两个 API 在 HDR 桌面下读到不同时间点、不同合成路径或不同坐标映射的数据。

SDR byte 投影定义：

1. 输入是平均后的 scRGB / linear Rec.709 `R, G, B`。
2. 每个通道先 clamp 到 `0..1`。
3. 应用 sRGB OETF：

```text
if c <= 0.0031308:
    srgb = 12.92 * c
else:
    srgb = 1.055 * pow(c, 1 / 2.4) - 0.055
```

4. `byte = round(srgb * 255)`，再 clamp 到 `0..255`。
5. alpha 首版固定为 `255`，除非未来捕获路径能提供有意义 alpha。

这样得到的 `sdrR/sdrG/sdrB/sdrA` 用来构造 `System.Drawing.Color`，继续喂给 PowerToys 原来的格式逻辑：

- HEX
- RGB
- HSL/HSV/HSB/HSI/HWB
- CMYK
- CIEXYZ/CIELAB
- Oklab/Oklch
- Decimal / HEX Int
- color name

这个投影不会保留 HDR 亮度；它是为了兼容旧 SDR 表达。HDR 值本身仍由 `linear RGB` 等新 token 输出。

是否需要保留旧 GDI 路径：

- 作为 fallback 保留，不作为正常路径。
- 当 WGC 不支持、D3D device lost、frame timeout、capture item 创建失败时，旧 SDR token 可以用 GDI 继续输出。
- 在 fallback 状态下，HDR token 输出 `N/A`。
- 可保留一个诊断/对照开关，用来验证当前 PowerToys 的 255 clamp 行为，但不要作为产品默认读屏路径。

平均采样与旧格式：

- 采样大小设置同样影响 SDR 投影。
- 即 `11x11` 时先平均 linear RGB，再投影成 `System.Drawing.Color`。
- 这和原 PowerToys 的 `1x1` 旧行为不完全相同，但符合新吸管平均功能的定义，也保证 HDR/SDR 输出来自同一个平均样本。

### 4. Format system

PowerToys 现有格式 token 是大小写敏感的 `%??` 加可选单字符格式。为了最小侵入，HDR 首版也使用两字符 token。

所有新增 HDR token 必须出现在 PowerToys 现有 `Add custom color format` / `ColorFormatEditor` 的参数说明区域。不能只在 README、命令行帮助或隐藏配置中说明。

建议新增 token：

| Token | 含义 | 示例 |
|---|---|---|
| `%Lr` | linear RGB red | `1.5703` |
| `%Lg` | linear RGB green | `1.5713` |
| `%Lb` | linear RGB blue | `1.5732` |
| `%Nr` | red nits | `125.6250` |
| `%Ng` | green nits | `125.7031` |
| `%Nb` | blue nits | `125.8594` |
| `%Ny` | Y nits | `125.6978` |
| `%Ii` | ICtCp I | `0.5312` |
| `%Ic` | ICtCp I 10-bit PQ code value, `round(I * 1023)` | `543` |
| `%Ct` | ICtCp Ct | `0.0001` |
| `%Cp` | ICtCp Cp | `-0.0001` |

默认格式名必须按需求精确命名：

```text
linear RGB = linear RGB(%Lr, %Lg, %Lb)
RGB nits  = RGB nits(%Nr, %Ng, %Nb)
Y nits    = Y nits(%Ny)
ICtCp     = ICtCp(I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp)
```

精度策略：

- 首版 HDR token 默认固定四位小数。
- `%Ic` 是 10-bit code value，默认输出整数；它不受四位小数规则影响。
- 暂不复用现有 `%Reff` 那类 `f/F` 两位小数语义，因为 HDR 需求默认是四位。
- 后续如果要做 printf-like 精度，建议扩展 parser 支持 `%.3Lr` 或 `%Lr{3}`，但这属于第二阶段。首版不要为了精度语法扩大改动面。

UI 要求：

- 在现有格式列表中增加默认隐藏格式：
  - `linear RGB`
  - `RGB nits`
  - `Y nits`
  - `ICtCp`
- 在 `Add custom color format` 的帮助说明中列出 HDR token、说明它们大小写敏感，并说明默认四位小数。
- 预览文本继续使用现有格式预览区域，不新增独立 HDR 面板。
- 如果格式字符串变长，只做不溢出/不遮挡的容纳修正，不改变弹窗交互结构。

格式不可用时：

- 如果 `HdrColorSample.hasHdrData == false`，HDR token 返回 `N/A`，不要返回 `0.0000`。
- 原有 SDR token 继续按原逻辑输出。
- 这样当 WGC 不可用或降级时，用户能看出 HDR 数据不可用。

### 5. Sampling-size setting

新增采样大小配置：

```text
1x1, 3x3, 5x5, 11x11, 31x31, 51x51, 101x101
```

计算规则：

```text
screen point -> clipped NxN region -> average linear RGB -> derived outputs
```

屏幕边缘时裁剪区域，只平均实际可读像素。产品 UI 不一定显示 `actualWidth/actualHeight/pixelCount`，但内部 sample model 保留这些字段，方便诊断。

PowerToys 设置页只新增一个现有风格的下拉设置，不改变 Color Picker 主 UI。

UI 要求：

- 采样大小必须在 Color Picker 设置页中显示为下拉框/ComboBox。
- 选项固定为 `1x1`, `3x3`, `5x5`, `11x11`, `31x31`, `51x51`, `101x101`。
- 默认值为 `1x1`，以兼容 PowerToys 当前行为。
- 该配置影响 HDR token 和旧 SDR 格式的共同采样源。
- 不使用命令行参数、注册表隐藏项或手写 JSON 作为产品主配置方式。

### 6. Standalone picker prototype

建议第一可用产品先做独立工具：

```text
src/hdr_picker_app/
```

功能范围：

- 全局热键启动。
- 可配置 activation shortcut，设置界面应沿用 PowerToys Color Picker 的概念和默认值，而不是硬编码快捷键。
- 捕获鼠标位置颜色。
- 支持连续预览和点击复制。
- 支持采样大小设置。
- 支持四个 HDR 输出格式和用户自定义格式字符串。
- 使用 borderless WGC，不闪屏幕边缘。
- 保留 console demo 作为低层诊断工具。

UI 策略：

- 不追求重新设计 PowerToys。
- 第一版独立工具也尽量复用 PowerToys Color Picker 的交互模型、格式列表模型和设置概念，而不是重新设计一套产品。
- 独立工具允许用更小的外壳承载功能，但新增配置仍应有可见 UI：activation shortcut、format 列表/format editor、sample-size 下拉框、启用/复制行为。
- 不把命令行参数作为独立工具的正式用户配置入口；命令行只保留给 `hdr_sampler_demo` 诊断程序。
- 这个独立工具用于产品验证和日常使用；PowerToys 集成另做。

### 7. PowerToys integration

PowerToys 集成目标是最小改造。

关键落点：

```text
src/modules/colorPicker/ColorPickerUI/Mouse/MouseInfoProvider.cs
src/modules/colorPicker/ColorPickerUI/ViewModels/MainViewModel.cs
src/common/ManagedCommon/ColorFormatHelper.cs
src/settings-ui/Settings.UI.Library/ColorPickerProperties.cs
src/settings-ui/Settings.UI/SettingsXAML/Controls/ColorFormatEditor.xaml(.cs)
```

建议保持原有 SDR 路径，新增并行 HDR sample：

- `MouseInfoProvider` 保留当前 `Color CurrentColor` 和 `MouseColorChanged`，减少现有 UI/历史/复制逻辑回归。
- 新增 `HdrColorSample CurrentHdrSample` 和对应事件，或新增包含 SDR + HDR 的 event args。
- 原有 SDR token 继续使用 `System.Drawing.Color`，但该 `Color` 默认由 HDR sample 的 SDR projection 生成，而不是另行调用 GDI。
- 新 HDR token 使用 `HdrColorSample`。
- 当用户选择的格式不含 HDR token 时，HDR sampler 可以懒启动或不参与格式化；但 picker 激活期间为了预览一致性可以保持常驻。

`ColorFormatHelper` 改造：

- 保留现有 `GetStringRepresentation(Color? color, string formatString)`。
- 新增 overload：

```csharp
public static string GetStringRepresentation(
    Color? color,
    HdrColorSample? hdrSample,
    string formatString)
```

- parser 首先识别 HDR token；没有 HDR token 的格式走旧路径。
- 旧格式和旧单测不应改变。

设置改造：

- 保留 PowerToys 现有 `ActivationShortcut` 设置入口和行为；如果做独立工具，也提供同等 activation shortcut 设置 UI。
- `ColorPickerProperties.VisibleColorFormats` 添加默认隐藏项：
  - `linear RGB`
  - `RGB nits`
  - `Y nits`
  - `ICtCp`
- 添加 `SampleSize` 属性，默认 `1`。
- 在 Color Picker 设置页新增 sample-size 下拉框，沿用 PowerToys 设置页现有控件风格。
- 迁移旧设置时给缺省值，不破坏已有用户配置。
- `ColorFormatEditor` 的帮助列表追加 HDR token 说明；不改变弹窗布局和交互，只确保长文本不溢出。

## 降级和错误处理

必须区分“读到黑色/0”和“HDR 数据不可用”。

降级场景：

- Windows 不支持 WGC。
- borderless 权限拒绝。
- D3D device lost。
- 捕获 frame timeout。
- 捕获返回格式不是 FP16。
- 目标显示器/会话不可捕获。

行为：

- 原有 SDR PowerToys 取色仍可继续。
- HDR token 输出 `N/A`。
- 内部记录 status，诊断 demo/日志可显示具体状态。
- 不把 SDR byte 值反推成 HDR float。
- 如果 WGC 失败但旧 GDI fallback 成功，旧 SDR 格式继续输出；HDR 格式仍输出 `N/A`。

## 测试计划

### Unit tests

- sample-size 白名单：只接受 `1, 3, 5, 11, 31, 51, 101`。
- averaging：先平均 linear RGB，再派生输出。
- `RGB nits = channel * 80`。
- `Y nits` 使用 Rec.709 系数。
- ICtCp 参考样例：
  - black。
  - SDR white `(1,1,1)`。
  - gray HDR `(2,2,2)`。
  - colored HDR sample。
- token parser：
  - HDR token 大小写敏感。
  - 旧 token 输出不变。
  - HDR unavailable 输出 `N/A`。
- SDR projection：
  - linear `(0,0,0)` -> byte `(0,0,0)`。
  - linear `(1,1,1)` -> byte `(255,255,255)`。
  - linear `>1` -> byte clamp 到 `255`。
  - negative / tiny negative -> byte clamp 到 `0`。
  - mid-gray follows sRGB OETF, not a simple `linear * 255` mapping。
- 设置迁移：旧 `ColorPickerProperties` 缺少 sample size 和 HDR formats 时能补默认值。

### Manual validation

- 当前 PowerToys 对 HDR 高光读取仍显示 255，作为对照。
- demo/新工具同点读取 `linear RGB > 1.0`。
- SDR 白块接近 `1.0` / `80 nits`。
- `--borderless` 或产品捕获不再闪边。
- 采样大小从 `1x1` 切到 `11x11` 后输出更稳定。

### Compatibility validation

- SDR-only 显示器不崩溃，HDR token 显示 `N/A` 或正常 scRGB SDR 范围值，取决于后端是否返回 FP16 sample。
- 多显示器坐标映射正确。
- 鼠标跨显示器时捕获 session 正确切换。
- PowerToys 原有 HEX/RGB/HSL/历史记录/复制行为不回归。

## 实施阶段

### Phase 1: Core extraction

- 从 `src/hdr_sampler_demo/` 抽出 WGC FP16 capture core。
- 保留 demo，但让 demo 调用 core。
- 建立 core unit tests。

### Phase 2: Standalone MVP

- 做独立 picker shell。
- 接入 core、format helper、sample-size setting。
- 支持连续预览和复制。
- 用真实 HDR 内容验证日常使用体验。

### Phase 3: PowerToys fork integration

- 在独立 PowerToys fork/source location 中改，不在 `.agent/cache/PowerToys` 里直接开发。
- 添加 native helper 到 ColorPickerUI。
- 扩展 settings、format helper、help text。
- 跑 PowerToys ColorPicker 相关单测。

### Phase 4: Distribution decision

- 如果独立工具已满足需求，先打独立发布包。
- 如果 PowerToys 集成体验稳定，再评估：
  - 只替换 Color Picker 相关组件是否现实。
  - 维护 PowerToys fork。
  - 提交上游 PR。

## 当前实现状态

Phase 1 已完成。Phase 2 在用户反馈后纠正为 PowerToys-code-based prototype，而不是从头写独立窗口。

- `src/hdr_picker_core/` 已抽出可复用 core：
  - WGC monitor capture。
  - `R16G16B16A16Float` sample readback。
  - borderless capture request/use。
  - sample-size whitelist and clipped averaging。
  - SDR projection。
  - nits、Y nits、BT.2100-PQ ICtCp、ICtCp I 10-bit code value。
  - HDR format token formatter and `N/A` behavior。
- `src/hdr_sampler_demo/HdrSamplerDemo/` 已改为调用 core 的 console 诊断客户端。
- `src/hdr_sampler_demo/HdrColorTests/` 覆盖 sample size、nits/Y、SDR projection、token formatting、HDR unavailable。
- `src/hdr_sampler_demo/HdrSamplerNative/` 导出 native DLL，供 PowerToys C# 原型调用 WGC FP16 core。
- `src/powertoys_hdr_prototype/` 是从 PowerToys 源码复制出的原型位置，保留原 ColorPickerUI 浮层、点击动作、历史和 ColorEditor flow，只扩展 HDR sample/token plumbing。

验证：

- `.\scripts\build-demo.ps1`：Release x64 native/demo/test 构建通过，0 warning / 0 error。
- `.\x64\Release\HdrColorTests.exe`：通过。
- `src/powertoys_hdr_prototype/src/modules/colorPicker/ColorPickerUI/ColorPickerUI.Stage2.csproj`：Release 构建通过，0 warning / 0 error。
- 用户在 Windows 11 HDR 环境验证了 Stage2 独立版的快捷键启动、ESC 取消、鼠标确认、HDR/SDR 格式显示和主题修复。

当前 Phase 2 修复：

- WGC / `CreateFreeThreaded` / borderless API 使用 runtime feature detection。
- borderless 不支持或未授权时，HDR 采样走有边框 WGC fallback；WGC/FP16 不可用时 HDR token 输出 `N/A`。
- picker 活动期间复用同一个 WGC session/frame pool，避免每次 mouse sample 都重建 capture session。
- Stage2 Settings 底部增加简洁 HDR diagnostics 区域。
- Stage2 独立版使用 `RegisterHotKey` 启动 picker；低级键盘 hook 只处理 picker 激活后的 `Esc` / `Enter` / `Space`，并通过 WPF dispatcher 执行关闭/确认逻辑。

剩余：

- Win10 21H2 实机验证尚未完成；如果仍有问题，后续用新的 bug task 和 diagnostics/log 继续修。
- full PowerToys integration 仍属于 Phase 3。

## 当前里程碑范围

当前实现目标做到 Phase 2：PowerToys-code-based HDR picker prototype。

包含：

- Phase 1 core extraction。
- Phase 2 PowerToys ColorPickerUI source prototype。
- 保留 PowerToys 原有 UI/UX、点击动作、历史和 ColorEditor。
- 在 PowerToys 设置/格式系统中提供 visible HDR formats、sample size、custom format/token help。

不包含：

- 修改 `.agent/cache/PowerToys/` 缓存本体。
- 打包或替换已安装 PowerToys 的 Color Picker 组件。
- 上游 PR 或长期 fork 决策。
