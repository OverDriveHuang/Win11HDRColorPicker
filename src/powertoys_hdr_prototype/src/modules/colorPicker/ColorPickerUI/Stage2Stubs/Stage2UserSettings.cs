// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;

using ColorPicker.Common;
using ColorPicker.Settings;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace ColorPicker.Stage2
{
    [Export(typeof(IUserSettings))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class Stage2UserSettings : IUserSettings, IHdrSamplerSettings
    {
        public const string DefaultFormatName = DefaultSdrFormatName;
        public const string DefaultSdrFormatName = "default SDR";
        public const string DefaultHdrFormatName = "default HDR";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly HashSet<string> LegacyDefaultNames = new HashSet<string>
        {
            "default", "HEX", "RGB", "HSL", "HSV", "CMYK", "HSB", "HSI", "HWB", "NCol", "CIEXYZ", "CIELAB", "Oklab", "Oklch", "VEC4", "Decimal", "HEX Int", "linear RGB", "RGB nits", "Y nits", "ICtCp",
        };

        private bool _loading;

        public Stage2UserSettings()
        {
            ActivationShortcut = new SettingItem<string>("Win + Shift + C");
            ChangeCursor = new SettingItem<bool>(true);
            CopiedColorRepresentation = new SettingItem<string>(DefaultFormatName);
            CopiedColorRepresentationFormat = new SettingItem<string>(DefaultFormatString);
            ActivationAction = new SettingItem<ColorPickerActivationAction>(ColorPickerActivationAction.OpenColorPicker);
            PrimaryClickAction = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorThenEditor);
            MiddleClickAction = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorAndClose);
            SecondaryClickAction = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.Close);
            ColorHistoryLimit = new SettingItem<int>(20);
            ShowColorName = new SettingItem<bool>(false);
            SampleSize = new SettingItem<int>(1);

            ColorFormats = new ObservableCollection<Stage2ColorFormatSetting>(CreateDefaultFormats());
            VisibleColorFormats = new ObservableCollection<KeyValuePair<string, string>>();
            RebuildVisibleColorFormats();
            Load();
            WirePersistence();
        }

        public SettingItem<string> ActivationShortcut { get; }

        public SettingItem<bool> ChangeCursor { get; }

        public SettingItem<string> CopiedColorRepresentation { get; set; }

        public SettingItem<string> CopiedColorRepresentationFormat { get; set; }

        public SettingItem<ColorPickerActivationAction> ActivationAction { get; }

        public SettingItem<ColorPickerClickAction> PrimaryClickAction { get; }

        public SettingItem<ColorPickerClickAction> MiddleClickAction { get; }

        public SettingItem<ColorPickerClickAction> SecondaryClickAction { get; }

        public RangeObservableCollection<string> ColorHistory { get; } = new RangeObservableCollection<string>();

        public SettingItem<int> ColorHistoryLimit { get; }

        public ObservableCollection<KeyValuePair<string, string>> VisibleColorFormats { get; }

        public ObservableCollection<Stage2ColorFormatSetting> ColorFormats { get; }

        public SettingItem<bool> ShowColorName { get; }

        public SettingItem<int> SampleSize { get; }

        public static string DefaultFormatString => DefaultSdrFormatString;

        public static string DefaultSdrFormatString
            => "RGB = rgb(%Re, %Gr, %Bl), CIELAB = (%Lc, %Ca, %Cb), H=%Hu, S=%Sb%";

        public static string DefaultHdrFormatString
            => "Nits = (Y=%Ny, %Nr, %Ng, %Nb), I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp";

        public void SendSettingsTelemetry() { }

        public static IReadOnlyList<Stage2ColorFormatSetting> CreateDefaultFormats()
            => new List<Stage2ColorFormatSetting>
            {
                new Stage2ColorFormatSetting(DefaultSdrFormatName, DefaultSdrFormatString, true),
                new Stage2ColorFormatSetting(DefaultHdrFormatName, DefaultHdrFormatString, true),
                new Stage2ColorFormatSetting("RGB", "rgb(%Re, %Gr, %Bl)", true),
                new Stage2ColorFormatSetting("HSL", "hsl(%Hu, %Sl%, %Ll%)", true),
                new Stage2ColorFormatSetting("HSV", "hsv(%Hu, %Sb%, %Va%)", true),
                new Stage2ColorFormatSetting("HSB", "hsb(%Hu, %Sb%, %Br%)", true),
                new Stage2ColorFormatSetting("CIE XYZ", "XYZ(%Xv, %Yv, %Zv)", true),
                new Stage2ColorFormatSetting("CIE L*a*b*", "CIELab(%Lc, %Ca, %Cb)", true),
                new Stage2ColorFormatSetting("linear RGB", "linear RGB(%Lr, %Lg, %Lb)", true),
                new Stage2ColorFormatSetting("RGB nits", "RGB nits(%Nr, %Ng, %Nb)", true),
                new Stage2ColorFormatSetting("Y nits", "Y nits(%Ny)", true),
                new Stage2ColorFormatSetting("ICtCp", "ICtCp(I=%Ii, I10=%Ic, Ct=%Ct, Cp=%Cp)", true),
            };

        public void SetFormat(string name, string format)
            => SetFormat(name, name, format);

        public void SetFormat(string oldName, string newName, string format)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            var existing = ColorFormats.FirstOrDefault(item => item.Name == oldName);
            if (existing == null)
            {
                existing = new Stage2ColorFormatSetting(newName, format ?? string.Empty, true);
                ColorFormats.Add(existing);
            }
            else
            {
                existing.Name = newName;
                existing.Format = format ?? string.Empty;
            }

            if (CopiedColorRepresentation.Value == oldName)
            {
                CopiedColorRepresentation.Value = newName;
                CopiedColorRepresentationFormat.Value = existing.Format;
            }

            RebuildVisibleColorFormats();
            Save();
        }

        public void SetFormatEnabled(string name, bool enabled)
        {
            var existing = ColorFormats.FirstOrDefault(item => item.Name == name);
            if (existing == null)
            {
                return;
            }

            existing.IsEnabled = enabled;
            RebuildVisibleColorFormats();
            EnsureCopiedFormatEnabled();
            Save();
        }

        public void SetCopiedFormat(string name)
        {
            var selected = ColorFormats.FirstOrDefault(item => item.Name == name);
            if (selected == null)
            {
                return;
            }

            if (!selected.IsEnabled)
            {
                selected.IsEnabled = true;
                RebuildVisibleColorFormats();
            }

            CopiedColorRepresentation.Value = selected.Name;
            CopiedColorRepresentationFormat.Value = selected.Format;
            Save();
        }

        public void MoveFormat(string name, int direction)
        {
            var currentIndex = ColorFormats.IndexOf(ColorFormats.FirstOrDefault(item => item.Name == name));
            if (currentIndex < 0)
            {
                return;
            }

            var newIndex = currentIndex + direction;
            if (newIndex < 0 || newIndex >= ColorFormats.Count)
            {
                return;
            }

            ColorFormats.Move(currentIndex, newIndex);
            RebuildVisibleColorFormats();
            Save();
        }

        public void DeleteFormat(string name)
        {
            var existing = ColorFormats.FirstOrDefault(item => item.Name == name);
            if (existing == null)
            {
                return;
            }

            ColorFormats.Remove(existing);
            RebuildVisibleColorFormats();
            EnsureCopiedFormatEnabled();
            Save();
        }

        public void Save()
        {
            if (_loading)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            var snapshot = new Stage2SettingsSnapshot
            {
                ActivationShortcut = ActivationShortcut.Value,
                ChangeCursor = ChangeCursor.Value,
                CopiedColorRepresentation = CopiedColorRepresentation.Value,
                CopiedColorRepresentationFormat = CopiedColorRepresentationFormat.Value,
                ActivationAction = ActivationAction.Value,
                PrimaryClickAction = PrimaryClickAction.Value,
                MiddleClickAction = MiddleClickAction.Value,
                SecondaryClickAction = SecondaryClickAction.Value,
                ColorHistoryLimit = ColorHistoryLimit.Value,
                ShowColorName = ShowColorName.Value,
                SampleSize = SampleSize.Value,
                ColorFormats = ColorFormats.Select(item => new Stage2ColorFormatSnapshot
                {
                    Name = item.Name,
                    Format = item.Format,
                    IsEnabled = item.IsEnabled,
                }).ToList(),
            };

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }

        private static string SettingsPath
            => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "PowerToysHDRColorPicker", "stage2-settings.json");

        private void Load()
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var saveAfterLoad = false;
            _loading = true;
            try
            {
                var snapshot = JsonSerializer.Deserialize<Stage2SettingsSnapshot>(File.ReadAllText(SettingsPath));
                if (snapshot == null)
                {
                    return;
                }

                ActivationShortcut.Value = string.IsNullOrWhiteSpace(snapshot.ActivationShortcut) ? "Win + Shift + C" : snapshot.ActivationShortcut;
                ChangeCursor.Value = snapshot.ChangeCursor;
                ActivationAction.Value = snapshot.ActivationAction;
                PrimaryClickAction.Value = snapshot.PrimaryClickAction;
                MiddleClickAction.Value = snapshot.MiddleClickAction;
                SecondaryClickAction.Value = snapshot.SecondaryClickAction;
                ColorHistoryLimit.Value = snapshot.ColorHistoryLimit <= 0 ? 20 : snapshot.ColorHistoryLimit;
                ShowColorName.Value = snapshot.ShowColorName;
                SampleSize.Value = NormalizeSampleSize(snapshot.SampleSize);

                ColorFormats.Clear();
                if (snapshot.ColorFormats?.Count > 0)
                {
                    foreach (var item in snapshot.ColorFormats)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Name))
                        {
                            ColorFormats.Add(new Stage2ColorFormatSetting(item.Name, RepairKnownBrokenFormat(item.Name, item.Format), item.IsEnabled));
                        }
                    }
                }
                else if (snapshot.VisibleColorFormats?.Count > 0)
                {
                    foreach (var item in MigrateLegacyVisibleFormats(snapshot.VisibleColorFormats))
                    {
                        ColorFormats.Add(item);
                    }
                }

                EnsureDefaultFormats();
                RebuildVisibleColorFormats();

                CopiedColorRepresentation.Value = string.IsNullOrWhiteSpace(snapshot.CopiedColorRepresentation) ? DefaultFormatName : snapshot.CopiedColorRepresentation;
                EnsureCopiedFormatEnabled();
                saveAfterLoad = true;
            }
            finally
            {
                _loading = false;
            }

            if (saveAfterLoad)
            {
                Save();
            }
        }

        private void WirePersistence()
        {
            ActivationShortcut.PropertyChanged += (_, __) => Save();
            ChangeCursor.PropertyChanged += (_, __) => Save();
            CopiedColorRepresentation.PropertyChanged += (_, __) =>
            {
                var selected = ColorFormats.FirstOrDefault(item => item.Name == CopiedColorRepresentation.Value);
                if (selected != null)
                {
                    CopiedColorRepresentationFormat.Value = selected.Format;
                }

                Save();
            };
            CopiedColorRepresentationFormat.PropertyChanged += (_, __) => Save();
            ActivationAction.PropertyChanged += (_, __) => Save();
            PrimaryClickAction.PropertyChanged += (_, __) => Save();
            MiddleClickAction.PropertyChanged += (_, __) => Save();
            SecondaryClickAction.PropertyChanged += (_, __) => Save();
            ColorHistoryLimit.PropertyChanged += (_, __) => Save();
            ShowColorName.PropertyChanged += (_, __) => Save();
            SampleSize.PropertyChanged += (_, __) =>
            {
                var normalized = NormalizeSampleSize(SampleSize.Value);
                if (normalized != SampleSize.Value)
                {
                    SampleSize.Value = normalized;
                    return;
                }

                Save();
            };
        }

        private void RebuildVisibleColorFormats()
        {
            VisibleColorFormats.Clear();
            foreach (var item in ColorFormats.Where(item => item.IsEnabled))
            {
                VisibleColorFormats.Add(new KeyValuePair<string, string>(item.Name, item.Format));
            }
        }

        private void EnsureDefaultFormats()
        {
            foreach (var item in CreateDefaultFormats())
            {
                var existing = ColorFormats.FirstOrDefault(format => format.Name == item.Name);
                if (existing == null)
                {
                    ColorFormats.Add(item);
                }
                else if (IsPinnedDefaultFormat(existing.Name))
                {
                    existing.Format = item.Format;
                    existing.IsEnabled = true;
                }
            }

            for (int i = ColorFormats.Count - 1; i >= 0; i--)
            {
                var item = ColorFormats[i];
                if (LegacyDefaultNames.Contains(item.Name) && CreateDefaultFormats().All(defaultItem => defaultItem.Name != item.Name))
                {
                    ColorFormats.RemoveAt(i);
                }
            }

            MoveFormatToIndex(DefaultSdrFormatName, 0);
            MoveFormatToIndex(DefaultHdrFormatName, 1);
        }

        private void MoveFormatToIndex(string name, int targetIndex)
        {
            var item = ColorFormats.FirstOrDefault(format => format.Name == name);
            if (item == null)
            {
                return;
            }

            var currentIndex = ColorFormats.IndexOf(item);
            if (currentIndex >= 0 && currentIndex != targetIndex && targetIndex < ColorFormats.Count)
            {
                ColorFormats.Move(currentIndex, targetIndex);
            }
        }

        private void EnsureCopiedFormatEnabled()
        {
            var selected = ColorFormats.FirstOrDefault(item => item.Name == CopiedColorRepresentation.Value && item.IsEnabled)
                           ?? ColorFormats.FirstOrDefault(item => item.Name == DefaultFormatName)
                           ?? ColorFormats.FirstOrDefault(item => item.IsEnabled);
            if (selected != null)
            {
                selected.IsEnabled = true;
                CopiedColorRepresentation.Value = selected.Name;
                CopiedColorRepresentationFormat.Value = selected.Format;
                RebuildVisibleColorFormats();
            }
        }

        private static IEnumerable<Stage2ColorFormatSetting> MigrateLegacyVisibleFormats(Dictionary<string, string> visibleFormats)
        {
            var defaults = CreateDefaultFormats().ToDictionary(item => item.Name, item => item);
            foreach (var item in defaults.Values)
            {
                yield return item;
            }

            foreach (var item in visibleFormats)
            {
                if (LegacyDefaultNames.Contains(item.Key) || defaults.ContainsKey(item.Key))
                {
                    continue;
                }

                yield return new Stage2ColorFormatSetting(item.Key, item.Value, true);
            }
        }

        private static int NormalizeSampleSize(int value)
        {
            int[] allowed = { 1, 3, 5, 11, 31, 51, 101 };
            return allowed.Contains(value) ? value : 1;
        }

        private static string RepairKnownBrokenFormat(string name, string format)
        {
            if (name == "default")
            {
                return DefaultSdrFormatString;
            }

            var broken = new Dictionary<string, string>
            {
                ["RGB"] = "rgb(%R, %G, %B)",
                ["HSL"] = "hsl(%H, %S%, %L%)",
                ["HSV"] = "hsv(%H, %S%, %V%)",
                ["CIE L*a*b*"] = "CIELAB(%Cl, %Ca, %Cb)",
                ["CIELAB"] = "CIELAB(%Cl, %Ca, %Cb)",
            };

            var repaired = CreateDefaultFormats().FirstOrDefault(item => item.Name == name);
            return broken.TryGetValue(name, out var brokenFormat) && format == brokenFormat && repaired != null ? repaired.Format : format ?? string.Empty;
        }

        private static bool IsPinnedDefaultFormat(string name)
            => name == DefaultSdrFormatName || name == DefaultHdrFormatName;

        private sealed class Stage2SettingsSnapshot
        {
            public string ActivationShortcut { get; set; }

            public bool ChangeCursor { get; set; } = true;

            public string CopiedColorRepresentation { get; set; }

            public string CopiedColorRepresentationFormat { get; set; }

            public ColorPickerActivationAction ActivationAction { get; set; } = ColorPickerActivationAction.OpenColorPicker;

            public ColorPickerClickAction PrimaryClickAction { get; set; } = ColorPickerClickAction.PickColorThenEditor;

            public ColorPickerClickAction MiddleClickAction { get; set; } = ColorPickerClickAction.PickColorAndClose;

            public ColorPickerClickAction SecondaryClickAction { get; set; } = ColorPickerClickAction.Close;

            public int ColorHistoryLimit { get; set; } = 20;

            public bool ShowColorName { get; set; }

            public int SampleSize { get; set; } = 1;

            public List<Stage2ColorFormatSnapshot> ColorFormats { get; set; }

            public Dictionary<string, string> VisibleColorFormats { get; set; }
        }
    }

    public sealed class Stage2ColorFormatSetting
    {
        public Stage2ColorFormatSetting()
        {
        }

        public Stage2ColorFormatSetting(string name, string format, bool isEnabled)
        {
            Name = name;
            Format = format;
            IsEnabled = isEnabled;
        }

        public string Name { get; set; }

        public string Format { get; set; }

        public bool IsEnabled { get; set; }
    }

    internal sealed class Stage2ColorFormatSnapshot
    {
        public string Name { get; set; }

        public string Format { get; set; }

        public bool IsEnabled { get; set; }
    }
}

namespace ColorPicker.Settings
{
    public interface IHdrSamplerSettings
    {
        SettingItem<int> SampleSize { get; }
    }
}
