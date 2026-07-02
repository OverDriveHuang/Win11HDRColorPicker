# Summary

Diagnostic-only branch `codex/diagnostic-wgc-gdi-compare` compares WGC FP16 sampling against a GDI-style SDR read at the same screen point. The purpose is to diagnose Win10 21H2 cases where WGC reports SDR content too bright even though SDR white level is still 80 nits.

The polling comparison is shown in Settings HDR diagnostics as `Last WGC/GDI comparison`; it does not change copy formats or formal product sampling behavior.

User testing on a normal Win11 machine showed the first GDI diagnostic path could read the picker/settings UI itself, commonly returning `rgb(32,32,32)` or `rgb(44,44,44)`, while WGC still returned the expected target color. The current diagnostic build follows PowerToys' own zoom capture pattern, but applies it to all visible top-level windows from the current process before `CopyFromScreen`, restores them after sampling, and reports `excludedWindows=N`.

The 2026-07-03 user log showed `topWindowBefore` and `topWindowAfterExclusion` both identify `msedge`, but GDI still disagreed with WGC/old PowerToys expectations. Because the polling diagnostics path remains unreliable, the current build adds a fixed `GDI RGB` row in the editor format list. On picker click, it captures a 1x1 old-PowerToys-style GDI RGB value, persists it with the history color item, and shows that saved value for the selected swatch. Old history or failed GDI reads show `N/A`.

Latest diagnostic build path:

`D:\Users\OverDrive\Documents\my_projects\projects\powertoys_hdr_color_picker\.agent\tmp\diagnostic-wgc-gdi-compare\publish-win-x64\PowerToys.ColorPickerUI.Stage2.exe`
