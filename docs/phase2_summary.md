# Summary

Task: Productization and integration plan

Final status: completed for Phase 2.

## Outcome

This task produced a PowerToys ColorPickerUI-code-based standalone HDR color picker prototype. It is not integrated back into PowerToys Runner, but it reuses the PowerToys Color Picker UI/editor flow and adds the HDR sampling and formatting path required for daily testing.

The earlier from-scratch Win32 picker experiment is superseded. The accepted product shell is the Stage2 detached ColorPickerUI executable under:

```text
src/powertoys_hdr_prototype/src/modules/colorPicker/ColorPickerUI/ColorPickerUI.Stage2.csproj
```

## What Changed

- Added reusable HDR capture/sampling/conversion code from the WGC FP16 demo path.
- Added a native bridge for the PowerToys C# prototype.
- Added a Stage2 WPF launcher that reuses PowerToys ColorPickerUI views, view models, mouse lifecycle, floating picker, color editor, history, and format system.
- Added HDR sample sidecar plumbing and HDR format tokens:
  - `linear RGB`
  - `RGB nits`
  - `Y nits`
  - `ICtCp`
- Preserved legacy PowerToys SDR formatting by feeding the original formatter from the HDR sampler's SDR projection when available.
- Added PowerToys-style format settings:
  - enabled/hidden format rows
  - add/edit custom format dialog
  - token help including HDR tokens
  - `Picker popup format` selector
- Added sample-size settings using `1x1`, `3x3`, `5x5`, `11x11`, `31x31`, `51x51`, `101x101`.
- Added shortcut capture with cancel/reset default. Later Stage2 fixes removed low-level keyboard-hook activation fallback; picker activation now uses `RegisterHotKey`, while the low-level hook is limited to active-session Esc/Enter/Space handling and shortcut recording.
- Added tray icon with open/settings/exit behavior.
- Added second-precision build timestamp display in both the main picker panel and Settings window.
- Final default formats:
  - `default SDR`: `RGB = rgb(%Re, %Gr, %Bl), CIELAB = (%Lc, %Ca, %Cb), H=%Hu, S=%Sb%`
  - `default HDR`: `Nits = (Y=%Ny, %Nr, %Ng, %Nb), I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp`

## Verification

- Stage2 Release build passed with 0 warnings and 0 errors:

```text
dotnet build src/modules/colorPicker/ColorPickerUI/ColorPickerUI.Stage2.csproj -c Release
```

- Format probe passed for the final `default SDR` and `default HDR` strings.
- Stage2 startup smoke test passed from both build output and self-contained publish output.
- Local settings migration wrote the final `default HDR` format into:

```text
%LOCALAPPDATA%\PowerToysHDRColorPicker\stage2-settings.json
```

- Self-contained `win-x64` release package was produced under the task-local temporary release directory:

```text
.agent/tmp/release/WinHDRColorPicker-stage2-win-x64.zip
```

- The standalone public repository was published:

```text
https://github.com/OverDriveHuang/WinHDRColorPicker
```

Public repository commit:

```text
319f1e9 Add standalone HDR color picker prototype
```

The runnable package is included in that repository at:

```text
releases/WinHDRColorPicker-stage2-win-x64.zip
```

## Remaining Follow-Up

- Phase 3 PowerToys integration is deferred.
- Compare current upstream PowerToys Color Picker SDR clamp behavior against this prototype on the same HDR target when Phase 3 starts.
- Further bug fixes should be opened as new tasks, per user confirmation.

## Notes

Reusable project-level design context is in `synthesis/productization_design.md` and `synthesis/requirements.md`.
