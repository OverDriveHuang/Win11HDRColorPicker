# 需求边界

## 核心需求

做一个 Windows 11 上的 HDR-aware 屏幕吸管，主要针对 PowerToys Color Picker 的现有限制：当屏幕内容超过 SDR 白点时，现有吸管只能读到 8-bit SDR byte 值，通道最大为 255，不能表达 HDR 的 `>1.0` 数据。

目标输出应包括：

- `linear RGB`：display-referred linear RGB，SDR 范围约为 `0..1`，HDR 范围允许 `>1`。
- `RGB nits`：RGB 通道 nits，固定按 `channel_value * 80` 解释。
- `Y nits`：按线性 RGB 计算加权亮度 `Y` 后乘以 80。
- `ICtCp`：只做 BT.2100-PQ，用于 HDR/WCG 相关分析，需明确输入颜色空间和转换路径。除显示 `I/Ct/Cp` 浮点值外，还要支持显示 `I` 分量换算到 10-bit PQ code value 后的整数值，即 `round(I * 1023)`。
- 可配置格式：尽量沿用 PowerToys Color Picker 的格式配置体验，但新增 HDR token 或 HDR 专用格式。

取样范围应支持类似 Photoshop 吸管的平均模式。至少支持：

- `1x1`
- `3x3`
- `5x5`
- `11x11`
- `31x31`
- `51x51`
- `101x101`

计算顺序固定为：先在取样区域内对 display-referred linear RGB 做逐通道平均，再用平均后的 RGB 计算 `RGB nits`、`Y nits` 和 `ICtCp`。不要先逐像素算 nits/ICtCp 后再平均。

输出格式名按上面的字符串，大小写敏感。

数值精度：

- 默认保留小数点后四位，例如 `.0000`。
- 如果实现成本可控，后续格式 token 可支持类似 C/C++ `printf` 的精度控制。
- 如果精度配置成本过高，首版固定四位小数。

nits 基准固定为 `1.0 = 80 nits`。这是需求确定项，不再读取 Windows SDR white level 作为默认基准。

### Win10 SDR white level 验证和归一化

在 Windows 10 21H2 + NVIDIA + 外接显示器上，用户已观察到 SDR 图片通过 WGC FP16 采样得到的 `linear RGB` 约为预期 sRGB 反伽马 linear 值的 2 倍，例如白色约为 `(2.0, 2.0, 2.0)`。这不是预期行为，需要先诊断原因，再决定是否做归一化修正。

诊断必须读取当前采样点所在显示器的 SDR white level，而不是主显示器、第一条 display path 或任意全局值。当前显示器应按鼠标位置或实际采样 monitor 匹配到对应 display target 后查询。

首步只把当前显示器的 SDR white level 写入 diagnostics/log，用于实机验证：

- raw SDR white level 值。
- 换算后的 nits。
- 相对 80 nits 的倍率。
- WGC FP16 白点读数与该倍率是否吻合。

如果实机确认 Win10 异常值与 `SDR white level / 80` 一致，则后续可以增加基于当前显示器 SDR white level 的归一化：在受影响环境中将 WGC FP16 采样值除以该倍率，使 SDR 图片白点回到 `linear RGB ~= 1.0`。该修正必须按当前显示器逐点应用，不能使用固定 `2x` 或跨显示器缓存错用。

### 历史色块和 HDR 数据保存

颜色历史不能只保存 SDR `A/R/G/B` 值。每次点击采样并写入历史时，必须同时保存该色块当时对应的 HDR sample 数据，包括 `linear RGB`、`RGB nits`、`Y nits`、`ICtCp` 和用于判断可用性的状态信息。

历史色块、颜色编辑器面板和复制操作必须使用该色块自己保存的 HDR sample 进行 HDR token 格式化，不能使用当前鼠标位置、最近一次吸管采样或全局临时缓存替代。切换不同历史色块时，`linear RGB`、`RGB nits`、`Y nits`、`ICtCp`、`default HDR` 等 HDR 格式的显示值和复制值都必须随所选色块一起变化。

如果某条历史记录没有保存 HDR sample，例如旧版本迁移记录，或采样时 HDR 数据不可用，则该色块的 HDR token 输出应显示 `N/A`。不能复用最近一次可用 HDR sample 来填充旧色块或不可用色块。

## UI/UX 硬约束

最终产品不重新设计 UI。PowerToys Color Picker 的主界面、吸管交互、颜色编辑器、设置页和“Add custom color format”弹窗应保持原版体验。

允许的 UI 变化仅限：

- 在现有格式系统里新增 HDR 参数/token、预置格式和帮助说明。
- 现有格式字符串区域可能因为 HDR 字符串更长而需要不溢出、不遮挡的容纳处理。
- 预览文本可以显示更长的 HDR 数值，但不新增独立面板或改变交互流程。

不允许的 UI 变化：

- 不做新的视觉风格、布局重排或额外功能页。
- 不把验证 demo 的界面当作最终产品 UI。
- 不为了 HDR 输出把原有 SDR RGB/HEX/历史记录/复制流程改成另一套交互。

## Phase 2 standalone shell 修订需求

Phase 2 是一个可独立运行的 PowerToys ColorPickerUI-code-based shell。它可以不嵌入 PowerToys 主应用，但设置、格式编辑、热键、托盘和退出行为必须接近 PowerToys 的使用方式，而不是 demo app 的临时控件。

### 默认格式

默认格式列表只保留以下格式：

- `RGB`
- `HSL`
- `HSV`
- `HSB`
- `CIEXYZ`，UI 显示可写作 `CIE XYZ`
- `CIELAB`，UI 显示可写作 `CIE L*a*b*`
- `linear RGB`
- `RGB nits`
- `Y nits`
- `ICtCp`

默认再新增两个组合格式，用来避免单行过长：

- `default SDR`：显示传统 SDR 派生读数，首版包含 `RGB (8bit)`、`CIELAB` 和 HSB 的 `H/S`。
- `default HDR`：显示 HDR 采样读数，首版包含 `Y nits`、RGB nits 和 `ICtCp`；`linear RGB` 保留为独立格式，不放入默认 HDR 组合。

暂定 `default SDR` 格式字符串为：

```text
RGB = rgb(%Re, %Gr, %Bl), CIELAB = (%Lc, %Ca, %Cb), H=%Hu, S=%Sb%
```

暂定 `default HDR` 格式字符串为：

```text
Nits = (Y=%Ny, %Nr, %Ng, %Nb), I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp
```

旧的单行 `default` 组合格式应迁移或替换为上述两个默认格式，避免悬浮窗横向溢出。

### 格式设置 UI

格式设置应模仿 PowerToys Color Picker 设置页，而不是左侧长列表 + 右侧内联编辑器。

要求：

- 设置页必须提供一个悬浮窗/吸管 tooltip 显示格式选择控件，例如 `Picker popup format` 下拉框。该下拉框从已启用的格式中选择一个，写入 PowerToys 原有的 `CopiedColorRepresentation` / `CopiedColorRepresentationFormat` 设置；悬浮窗显示内容必须跟随该选择，而不是固定为某个默认格式。
- `Picker popup format` 控件的标签必须完整可读，下拉框不应横向铺满设置页，并且需要和下方 `Color formats` 配置卡片保留清晰间距。
- 顶部有一个 `Color formats` 设置卡片/header，说明文字为配置颜色格式，右侧有 `Add new format` 按钮。
- 格式列表每一行显示格式名称和预览文本。
- 每一行有启用/隐藏 toggle。
- 每一行有更多操作入口，用于编辑、删除、上移、下移等；Phase 2 至少需要编辑和删除。
- 新增/编辑格式必须打开二级窗口/对话框，不在主设置页内联展开编辑。
- 二级窗口包含：
  - 标题：新增时为 `Add custom color format`，编辑时为 `Edit custom color format`。
  - `Name` 输入框。
  - `Format` 输入框。
  - 实时预览。
  - `Save`/`Update` 和 `Cancel`。
  - 参数帮助说明。

参数帮助说明必须像 PowerToys 原窗口一样清晰排列：先写 token，再写含义，使用类似表格/多列布局；不能把所有 token 和说明写成一段自动换行的长文本。HDR token 应加入同一个帮助区域或紧邻的 HDR 区域，仍保持 token-含义表格样式。

必须支持用户自定义组合字符串，例如：

```text
L* = %Lc, a* = %Ca, b* = %Cb, H = %Hu, S = %Sl
```

该字符串应作为一个自定义格式正常预览、保存、启用、复制。

### 设置项精简

Stage2 设置界面不要暴露以下临时/demo 控件：

- `Copy as`
- `Show color name`
- `Primary click`
- `Middle click`
- `Secondary click`

默认点击行为保持接近 PowerToys：激活 picker 后左键拾取并按当前行为进入/更新颜色编辑器；不要让用户在 Stage2 临时设置窗里直接看到内部 click action 枚举。

### 采样大小

sample size 设置不能显示裸数字。必须显示为：

- `1x1`
- `3x3`
- `5x5`
- `11x11`
- `31x31`
- `51x51`
- `101x101`

内部仍可保存整数边长。

### 快捷键设置

快捷键设置不能是普通自由文本框。它必须是一个快捷键录入控件：

- 用户点击控件后，控件监听下一组键位组合。
- 默认值为 `Win + Shift + C`。
- 应支持用户改成例如 `Win + Shift + A`。
- 录入状态必须能取消，例如通过 `Esc` 或 `Cancel` 控件退出录制而不修改现有快捷键。
- 必须提供恢复默认值的入口，将快捷键重置为 `Win + Shift + C`。
- 修改后必须重新注册全局热键，旧热键必须失效，新热键必须生效。
- 如果快捷键注册失败，例如被 PowerToys 或其他程序占用，应在 UI 或日志里明确提示，不能静默失败。
- Stage2 独立版使用 `RegisterHotKey` 作为激活路径。低级键盘 hook 只用于 picker 激活后的 `Esc` / `Enter` / `Space` 控制，以及快捷键录入窗口的按键捕获；不要让低级 hook 同时触发 picker 启动，避免重复启动/关闭 session。
- 快捷键应要求至少一个修饰键加一个非修饰键，避免误设为普通单键。

### 构建时间显示

Stage2 独立版必须在 Color Picker 主面板和 Settings 窗口显示当前二进制的构建时间，精确到秒。

要求：

- 构建时间不能靠每次手动改源码。
- 构建流程应自动生成一个构建变量或生成源码，例如 `Build yyyy-MM-dd HH:mm:ss`。
- UI 只引用这个自动生成的变量。
- 同一次构建产物中，Color Picker 主面板和 Settings 窗口显示的构建时间必须一致。

### Picker 取消行为

当吸管悬浮 picker 已激活但尚未拾取颜色时：

- `Esc` 必须取消本次 picker session。
- 取消时不写入历史、不复制颜色、不执行左键拾取。
- 取消后应回到可再次通过快捷键启动的状态。

### 托盘和退出行为

Phase 2 standalone shell 是后台常驻程序，因此必须提供托盘入口。

托盘要求：

- 启动后显示托盘图标。
- 托盘图标的主要用途是重新打开 Color Picker 面板/设置面板，避免主窗口隐藏后用户找不到入口。
- 托盘菜单至少包含：
  - `Open Color Picker`
  - `Settings`
  - `Exit`
- 关闭主面板时默认隐藏到托盘，不直接退出进程。
- `Exit` 必须真正退出进程、注销全局热键、释放托盘图标和采样资源。
- 如果 picker 悬浮层打开时主面板隐藏，托盘图标仍然可用。

## 第一阶段不做

- 不输出或维护完整 PowerToys 全功能发行版。
- 不把 Game Bar 的截图功能复刻成主要产品目标。
- 不读取视频/游戏源的编码级 PQ/HLG 原始值；本项目目标是屏幕最终显示管线中的 display-referred 像素值。
- 不设计新 UI；验证阶段的小 demo 只用于证明可读取 `>1.0` 的 HDR 值。

## 关键验收标准

1. 在 Windows 11 HDR 显示器上，读取 SDR 白色测试块时，linear RGB 应接近 `(1, 1, 1)`，加权亮度应接近 80 nits。
2. 读取已知 scRGB HDR 测试块，例如 `(2, 2, 2)` 或单通道 `>1` 测试块时，工具应输出 `>1.0` 的 linear 值和 `>80` 的 nits，而不是 255。
3. 同一测试块用当前 PowerToys Color Picker 读取时应能复现 255 截断，作为对照。
4. 当系统、显示器、内容或捕获 API 只能提供 SDR 数据时，工具必须明确标识降级状态，不能伪造 HDR 数值。
5. 输出格式可以在设置中配置，且现有 SDR RGB/HEX 行为不被破坏。
6. 最终 PowerToys 集成应保持原版 UI/UX，只扩展格式参数能力。
7. 平均取样必须先平均 linear RGB，再进行 nits 和 ICtCp 派生计算。
8. 历史色块必须保存点击采样当时的 HDR sample；切换历史色块和复制 HDR 格式时必须使用该色块自己的 HDR 数据。没有保存或不能读取 HDR 数据的色块必须显示/复制 `N/A`，不能沿用最近一次吸管采样值。
9. Windows 10 SDR 图片出现 WGC FP16 读数约 2 倍时，必须先通过当前显示器 SDR white level diagnostics 验证倍率来源；如果确认倍率吻合，修正应按当前显示器的 `SDR white level / 80` 做归一化，不能使用固定倍率或取错显示器。

## 待决策问题

- 首个可用版本是独立工具，还是基于 PowerToys fork？
- HDR 捕获主路径选 `Windows.Graphics.Capture` 还是 `IDXGIOutput5::DuplicateOutput1`？
- 最终可配置格式 token 的具体命名可以由实现决定，但必须大小写敏感，并在 PowerToys 原有说明区域列出。
- 是否实现 printf-style 精度控制取决于实现成本；默认四位小数是首选下限。
