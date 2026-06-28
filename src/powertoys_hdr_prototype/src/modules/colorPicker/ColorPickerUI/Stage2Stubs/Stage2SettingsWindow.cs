// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ColorPicker.Hdr;
using ColorPicker.Helpers;
using ColorPicker.Keyboard;
using ColorPicker.Settings;
using ManagedCommon;

namespace ColorPicker.Stage2
{
    internal sealed class Stage2SettingsWindow : Window
    {
        private static Stage2SettingsWindow _current;

        private readonly IUserSettings _settings;
        private readonly Stage2UserSettings _stage2Settings;
        private readonly StackPanel _formatRows = new StackPanel();
        private readonly ComboBox _hoverFormatComboBox = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly TextBlock _shortcutStatus = new TextBlock { Margin = new Thickness(120, 4, 0, 0), Opacity = 0.75 };
        private readonly Button _shortcutButton = new Button { MinWidth = 260, HorizontalContentAlignment = HorizontalAlignment.Left };
        private readonly Button _shortcutCancelButton = new Button { Content = "Cancel", MinWidth = 76, IsEnabled = false };
        private readonly Button _shortcutResetButton = new Button { Content = "Reset default", MinWidth = 104 };
        private readonly HashSet<int> _shortcutPressedKeys = new HashSet<int>();
        private GlobalKeyboardHook _shortcutCaptureHook;
        private bool _capturingShortcut;
        private bool _refreshingHoverFormat;

        private Stage2SettingsWindow(IUserSettings settings)
        {
            _settings = settings;
            _stage2Settings = settings as Stage2UserSettings;

            Title = $"Color Picker Settings - {Stage2BuildInfo.BuildLabel}";
            Width = 960;
            Height = 760;
            MinWidth = 760;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = BuildContent();
            RefreshFormatRows();
            Closed += (_, __) =>
            {
                DisposeShortcutCaptureHook();
                _current = null;
            };
        }

        public static void ShowForCurrentContainer()
        {
            var settings = Bootstrapper.Container.GetExportedValue<IUserSettings>();
            if (_current == null)
            {
                _current = new Stage2SettingsWindow(settings);
                if (Application.Current?.MainWindow != null)
                {
                    _current.Owner = Application.Current.MainWindow;
                }
            }

            _current.Show();
            _current.Activate();
        }

        private UIElement BuildContent()
        {
            var panel = new StackPanel { Margin = new Thickness(18) };

            panel.Children.Add(Header("Activation"));
            _shortcutButton.Content = _settings.ActivationShortcut.Value;
            _shortcutButton.Click += (_, __) => BeginShortcutCapture();
            _shortcutButton.PreviewKeyDown += ShortcutButton_PreviewKeyDown;
            _shortcutCancelButton.Click += (_, __) => CancelShortcutCapture();
            _shortcutResetButton.Click += (_, __) => ResetShortcutToDefault();

            var shortcutControls = new Grid();
            shortcutControls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shortcutControls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shortcutControls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            shortcutControls.Children.Add(_shortcutButton);
            Grid.SetColumn(_shortcutCancelButton, 1);
            _shortcutCancelButton.Margin = new Thickness(8, 0, 0, 0);
            shortcutControls.Children.Add(_shortcutCancelButton);
            Grid.SetColumn(_shortcutResetButton, 2);
            _shortcutResetButton.Margin = new Thickness(8, 0, 0, 0);
            shortcutControls.Children.Add(_shortcutResetButton);

            var shortcutPanel = new StackPanel();
            shortcutPanel.Children.Add(Labeled("Shortcut", shortcutControls));
            shortcutPanel.Children.Add(_shortcutStatus);
            panel.Children.Add(shortcutPanel);

            panel.Children.Add(Header("Sampling"));
            if (_settings is IHdrSamplerSettings sampleSettings)
            {
                var sampleSize = new ComboBox
                {
                    ItemsSource = new[] { "1x1", "3x3", "5x5", "11x11", "31x31", "51x51", "101x101" },
                    SelectedItem = $"{sampleSettings.SampleSize.Value}x{sampleSettings.SampleSize.Value}",
                    MinWidth = 140,
                };
                sampleSize.SelectionChanged += (_, __) =>
                {
                    if (sampleSize.SelectedItem is string selected && int.TryParse(selected.Split('x')[0], out var value))
                    {
                        sampleSettings.SampleSize.Value = value;
                    }
                };
                panel.Children.Add(Labeled("Sample size", sampleSize));
            }

            panel.Children.Add(Header("Color formats"));
            panel.Children.Add(BuildHoverFormatSelector());
            panel.Children.Add(BuildFormatHeaderCard());
            panel.Children.Add(_formatRows);

            panel.Children.Add(Header("Picker behavior"));
            var changeCursor = new CheckBox { Content = "Change cursor while picking", IsChecked = _settings.ChangeCursor.Value };
            changeCursor.Checked += (_, __) => _settings.ChangeCursor.Value = true;
            changeCursor.Unchecked += (_, __) => _settings.ChangeCursor.Value = false;
            panel.Children.Add(changeCursor);

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = panel,
            };
        }

        private UIElement BuildHoverFormatSelector()
        {
            _hoverFormatComboBox.SelectionChanged += (_, __) =>
            {
                if (_refreshingHoverFormat || _hoverFormatComboBox.SelectedItem is not string selectedName)
                {
                    return;
                }

                _stage2Settings?.SetCopiedFormat(selectedName);
            };

            RefreshHoverFormatSelector();

            var panel = new DockPanel { Margin = new Thickness(0, 6, 0, 14), LastChildFill = false };
            panel.Children.Add(new TextBlock
            {
                Text = "Picker popup format",
                Width = 190,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            panel.Children.Add(_hoverFormatComboBox);
            return panel;
        }

        private UIElement BuildFormatHeaderCard()
        {
            var border = CardBorder();
            border.Margin = new Thickness(0, 0, 0, 6);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            border.Child = grid;

            grid.Children.Add(new TextBlock
            {
                Text = "☑",
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            text.Children.Add(new TextBlock { Text = "Color formats", FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = "Configure the color formats (edit, delete, hide, reorder them)", Opacity = 0.7 });
            grid.Children.Add(text);

            var addButton = new Button { Content = "Add new format", MinWidth = 120, Padding = new Thickness(12, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            addButton.Click += (_, __) => OpenFormatDialog(null);
            Grid.SetColumn(addButton, 2);
            grid.Children.Add(addButton);

            return border;
        }

        private void RefreshFormatRows()
        {
            _formatRows.Children.Clear();
            if (_stage2Settings == null)
            {
                return;
            }

            RefreshHoverFormatSelector();
            foreach (var format in _stage2Settings.ColorFormats)
            {
                _formatRows.Children.Add(BuildFormatRow(format));
            }
        }

        private void RefreshHoverFormatSelector()
        {
            if (_stage2Settings == null)
            {
                return;
            }

            _refreshingHoverFormat = true;
            try
            {
                var enabledNames = _stage2Settings.ColorFormats
                    .Where(format => format.IsEnabled)
                    .Select(format => format.Name)
                    .ToList();

                _hoverFormatComboBox.ItemsSource = enabledNames;
                _hoverFormatComboBox.SelectedItem = enabledNames.Contains(_settings.CopiedColorRepresentation.Value)
                    ? _settings.CopiedColorRepresentation.Value
                    : enabledNames.FirstOrDefault();
            }
            finally
            {
                _refreshingHoverFormat = false;
            }
        }

        private UIElement BuildFormatRow(Stage2ColorFormatSetting format)
        {
            var border = CardBorder();
            border.Margin = new Thickness(0, 0, 0, 6);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            border.Child = grid;

            var text = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = format.Name, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = FormatPreview(format.Format), Opacity = 0.72, TextTrimming = TextTrimming.CharacterEllipsis });
            grid.Children.Add(text);

            var toggle = new CheckBox { IsChecked = format.IsEnabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
            toggle.Checked += (_, __) => SetFormatEnabled(format.Name, true);
            toggle.Unchecked += (_, __) => SetFormatEnabled(format.Name, false);
            Grid.SetColumn(toggle, 1);
            grid.Children.Add(toggle);

            var menuButton = new Button { Content = "...", MinWidth = 36, Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            menuButton.ContextMenu = BuildRowMenu(format);
            menuButton.Click += (_, __) => menuButton.ContextMenu.IsOpen = true;
            Grid.SetColumn(menuButton, 2);
            grid.Children.Add(menuButton);

            return border;
        }

        private ContextMenu BuildRowMenu(Stage2ColorFormatSetting format)
        {
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "Edit" };
            edit.Click += (_, __) => OpenFormatDialog(format);
            menu.Items.Add(edit);

            var moveUp = new MenuItem { Header = "Move up" };
            moveUp.Click += (_, __) =>
            {
                _stage2Settings.MoveFormat(format.Name, -1);
                RefreshFormatRows();
            };
            menu.Items.Add(moveUp);

            var moveDown = new MenuItem { Header = "Move down" };
            moveDown.Click += (_, __) =>
            {
                _stage2Settings.MoveFormat(format.Name, 1);
                RefreshFormatRows();
            };
            menu.Items.Add(moveDown);

            var delete = new MenuItem { Header = "Delete" };
            delete.Click += (_, __) =>
            {
                _stage2Settings.DeleteFormat(format.Name);
                RefreshFormatRows();
            };
            menu.Items.Add(delete);
            return menu;
        }

        private void OpenFormatDialog(Stage2ColorFormatSetting format)
        {
            if (_stage2Settings == null)
            {
                return;
            }

            var dialog = new Stage2FormatDialog(format);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _stage2Settings.SetFormat(format?.Name ?? dialog.FormatName, dialog.FormatName, dialog.FormatString);
                RefreshFormatRows();
            }
        }

        private void SetFormatEnabled(string name, bool enabled)
        {
            _stage2Settings?.SetFormatEnabled(name, enabled);
            RefreshFormatRows();
        }

        private void ShortcutButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturingShortcut)
            {
                return;
            }

            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                CancelShortcutCapture();
                return;
            }

            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (IsModifierKey(key))
            {
                _shortcutPressedKeys.Add(virtualKey);
                _shortcutStatus.Text = "Press one regular key to complete the shortcut.";
                return;
            }

            CompleteShortcutFromVirtualKey(virtualKey);
        }

        private void BeginShortcutCapture()
        {
            _capturingShortcut = true;
            _shortcutPressedKeys.Clear();
            _shortcutButton.Content = "Press shortcut...";
            _shortcutCancelButton.IsEnabled = true;
            _shortcutStatus.Text = "Press at least one modifier and one regular key. Esc or Cancel stops recording.";
            _shortcutButton.Focus();
            StartShortcutCaptureHook();
        }

        private void CancelShortcutCapture()
        {
            if (!_capturingShortcut)
            {
                return;
            }

            EndShortcutCapture();
            _shortcutButton.Content = _settings.ActivationShortcut.Value;
            _shortcutStatus.Text = "Shortcut capture canceled.";
        }

        private void ResetShortcutToDefault()
        {
            EndShortcutCapture();
            _settings.ActivationShortcut.Value = "Win + Shift + C";
            _shortcutButton.Content = _settings.ActivationShortcut.Value;
            _shortcutStatus.Text = "Shortcut reset to default.";
        }

        private void CompleteShortcutFromVirtualKey(int virtualKey)
        {
            if (IsModifierVirtualKey(virtualKey))
            {
                return;
            }

            var shortcut = BuildShortcutText(virtualKey);
            if (shortcut == null)
            {
                _shortcutStatus.Text = "Shortcut must include Ctrl, Alt, Shift, or Win.";
                return;
            }

            _settings.ActivationShortcut.Value = shortcut;
            _shortcutButton.Content = shortcut;
            _shortcutStatus.Text = "Shortcut updated.";
            EndShortcutCapture();
        }

        private void StartShortcutCaptureHook()
        {
            if (_shortcutCaptureHook != null)
            {
                return;
            }

            try
            {
                _shortcutCaptureHook = new GlobalKeyboardHook();
                _shortcutCaptureHook.KeyboardPressed += ShortcutCaptureHook_KeyboardPressed;
            }
            catch (Exception ex)
            {
                _shortcutStatus.Text = $"Shortcut capture failed: {ex.Message}";
            }
        }

        private void ShortcutCaptureHook_KeyboardPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            if (!_capturingShortcut)
            {
                return;
            }

            e.Handled = true;
            var virtualKey = e.KeyboardData.VirtualCode;
            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyUp || e.KeyboardState == GlobalKeyboardHook.KeyboardState.SysKeyUp)
            {
                _shortcutPressedKeys.Remove(virtualKey);
                return;
            }

            if (e.KeyboardState != GlobalKeyboardHook.KeyboardState.KeyDown && e.KeyboardState != GlobalKeyboardHook.KeyboardState.SysKeyDown)
            {
                return;
            }

            if (virtualKey == KeyInterop.VirtualKeyFromKey(Key.Escape))
            {
                CancelShortcutCapture();
                return;
            }

            _shortcutPressedKeys.Add(virtualKey);
            if (IsModifierVirtualKey(virtualKey))
            {
                _shortcutStatus.Text = "Press one regular key to complete the shortcut.";
                return;
            }

            CompleteShortcutFromVirtualKey(virtualKey);
        }

        private string BuildShortcutText(int virtualKey)
        {
            var parts = new List<string>();
            if (IsControlDown())
            {
                parts.Add("Ctrl");
            }

            if (IsAltDown())
            {
                parts.Add("Alt");
            }

            if (IsShiftDown())
            {
                parts.Add("Shift");
            }

            if (IsWinDown())
            {
                parts.Add("Win");
            }

            if (parts.Count == 0)
            {
                return null;
            }

            parts.Add(VirtualKeyToDisplayString(virtualKey));
            return string.Join(" + ", parts);
        }

        private void EndShortcutCapture()
        {
            _capturingShortcut = false;
            _shortcutPressedKeys.Clear();
            _shortcutCancelButton.IsEnabled = false;
            DisposeShortcutCaptureHook();
        }

        private void DisposeShortcutCaptureHook()
        {
            if (_shortcutCaptureHook == null)
            {
                return;
            }

            _shortcutCaptureHook.KeyboardPressed -= ShortcutCaptureHook_KeyboardPressed;
            _shortcutCaptureHook.Dispose();
            _shortcutCaptureHook = null;
        }

        private bool IsControlDown()
            => _shortcutPressedKeys.Any(IsControlVirtualKey) || System.Windows.Input.Keyboard.IsKeyDown(Key.LeftCtrl) || System.Windows.Input.Keyboard.IsKeyDown(Key.RightCtrl);

        private bool IsAltDown()
            => _shortcutPressedKeys.Any(IsAltVirtualKey) || System.Windows.Input.Keyboard.IsKeyDown(Key.LeftAlt) || System.Windows.Input.Keyboard.IsKeyDown(Key.RightAlt);

        private bool IsShiftDown()
            => _shortcutPressedKeys.Any(IsShiftVirtualKey) || System.Windows.Input.Keyboard.IsKeyDown(Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(Key.RightShift);

        private bool IsWinDown()
            => _shortcutPressedKeys.Any(IsWinVirtualKey) || System.Windows.Input.Keyboard.IsKeyDown(Key.LWin) || System.Windows.Input.Keyboard.IsKeyDown(Key.RWin);

        private static string VirtualKeyToDisplayString(int virtualKey)
        {
            var key = KeyInterop.KeyFromVirtualKey(virtualKey);
            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString();
            }

            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return key.ToString();
        }

        private static bool IsModifierKey(Key key)
            => key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin;

        private static bool IsModifierVirtualKey(int virtualKey)
            => IsControlVirtualKey(virtualKey) || IsAltVirtualKey(virtualKey) || IsShiftVirtualKey(virtualKey) || IsWinVirtualKey(virtualKey);

        private static bool IsControlVirtualKey(int virtualKey)
            => virtualKey == 0x11 || virtualKey == 0xA2 || virtualKey == 0xA3;

        private static bool IsAltVirtualKey(int virtualKey)
            => virtualKey == 0x12 || virtualKey == 0xA4 || virtualKey == 0xA5;

        private static bool IsShiftVirtualKey(int virtualKey)
            => virtualKey == 0x10 || virtualKey == 0xA0 || virtualKey == 0xA1;

        private static bool IsWinVirtualKey(int virtualKey)
            => virtualKey == 0x5B || virtualKey == 0x5C;

        internal static string FormatPreview(string format)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            var color = System.Drawing.Color.FromArgb(255, 255, 228, 181);
            var text = ColorRepresentationHelper.ReplaceName(ColorFormatHelper.GetStringRepresentation(color, format), color);
            return HdrFormatHelper.ContainsHdrToken(text) ? HdrFormatHelper.ReplaceHdrTokens(text, HdrSampleCache.Current) : text;
        }

        private static Border CardBorder()
            => new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 44, 44)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                MinHeight = 66,
            };

        private static TextBlock Header(string text)
            => new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 18, 0, 8),
            };

        private static FrameworkElement Labeled(string label, FrameworkElement control)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = true };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center,
            });
            panel.Children.Add(control);
            return panel;
        }
    }

    internal sealed class Stage2FormatDialog : Window
    {
        private readonly TextBox _nameTextBox = new TextBox();
        private readonly TextBox _formatTextBox = new TextBox();
        private readonly TextBlock _previewText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

        public Stage2FormatDialog(Stage2ColorFormatSetting format)
        {
            Title = format == null ? "Add custom color format" : "Edit custom color format";
            Width = 540;
            Height = 720;
            MinWidth = 500;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _nameTextBox.Text = format?.Name ?? "My Format";
            _formatTextBox.Text = format?.Format ?? "new Color (R = %Re, G = %Gr, B = %Bl)";
            _nameTextBox.TextChanged += (_, __) => UpdatePreview();
            _formatTextBox.TextChanged += (_, __) => UpdatePreview();

            Content = BuildContent(format == null);
            UpdatePreview();
        }

        public string FormatName => _nameTextBox.Text.Trim();

        public string FormatString => _formatTextBox.Text;

        private UIElement BuildContent(bool isAdd)
        {
            var root = new DockPanel();

            var buttons = new Grid { Margin = new Thickness(18), Height = 44 };
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var save = new Button { Content = isAdd ? "Save" : "Update" };
            save.Click += (_, __) =>
            {
                DialogResult = true;
                Close();
            };
            buttons.Children.Add(save);

            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (_, __) =>
            {
                DialogResult = false;
                Close();
            };
            Grid.SetColumn(cancel, 2);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(22) };
            root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel });

            panel.Children.Add(new TextBlock { Text = Title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
            panel.Children.Add(new TextBlock { Text = "Name" });
            panel.Children.Add(_nameTextBox);
            panel.Children.Add(new TextBlock { Text = "Format", Margin = new Thickness(0, 12, 0, 0) });
            panel.Children.Add(_formatTextBox);
            panel.Children.Add(_previewText);
            panel.Children.Add(new TextBlock { Text = "The following parameters can be used:", Margin = new Thickness(0, 18, 0, 8) });
            panel.Children.Add(TokenGrid(new[]
            {
                ("%Re", "red"), ("%Gr", "green"), ("%Bl", "blue"),
                ("%Al", "alpha"), ("%Cy", "cyan"), ("%Ma", "magenta"),
                ("%Ye", "yellow"), ("%Bk", "black key"), ("%Hu", "hue"),
                ("%Si", "saturation (HSI)"), ("%Sl", "saturation (HSL)"), ("%Sb", "saturation (HSB)"),
                ("%Br", "brightness"), ("%In", "intensity"), ("%Hn", "hue (natural)"),
                ("%Ll", "lightness (nat)"), ("%Lc", "lightness (CIE)"), ("%Va", "value"),
                ("%Wh", "whiteness"), ("%Bn", "blackness"), ("%Ca", "chromaticityA"),
                ("%Cb", "chromaticityB"), ("%Xv", "X value"), ("%Yv", "Y value"),
                ("%Zv", "Z value"), ("%Dv", "decimal value (BGR)"), ("%Dr", "decimal value (RGB)"),
                ("%Na", "color name"),
            }));

            panel.Children.Add(new TextBlock { Text = "HDR parameters:", Margin = new Thickness(0, 16, 0, 8) });
            panel.Children.Add(TokenGrid(new[]
            {
                ("%Lr", "linear red"), ("%Lg", "linear green"), ("%Lb", "linear blue"),
                ("%Nr", "red nits"), ("%Ng", "green nits"), ("%Nb", "blue nits"),
                ("%Ny", "Y nits"), ("%Ii", "ICtCp I"), ("%Ic", "ICtCp I 10-bit"),
                ("%Ct", "ICtCp Ct"), ("%Cp", "ICtCp Cp"),
            }));

            panel.Children.Add(new TextBlock { Text = "The red, green, blue and alpha values can be formatted to the following formats:", Margin = new Thickness(0, 16, 0, 8) });
            panel.Children.Add(TokenGrid(new[]
            {
                ("b", "byte value (default)"), ("h", "hex lowercase one digit"), ("H", "hex uppercase one digit"),
                ("x", "hex lowercase two digits"), ("X", "hex uppercase two digits"), ("f", "float with leading zero"),
                ("F", "float without leading zero"),
            }));
            panel.Children.Add(new TextBlock { Text = "Example: %ReX means red value in hex uppercase two digits format.", Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });

            return root;
        }

        private static Grid TokenGrid((string Token, string Description)[] tokens)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < tokens.Length; i++)
            {
                var row = i / 3;
                while (grid.RowDefinitions.Count <= row)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 10, 2) };
                item.Children.Add(new TextBlock { Text = tokens[i].Token, Width = 42, FontWeight = FontWeights.SemiBold });
                item.Children.Add(new TextBlock { Text = tokens[i].Description, TextWrapping = TextWrapping.Wrap });
                Grid.SetRow(item, row);
                Grid.SetColumn(item, i % 3);
                grid.Children.Add(item);
            }

            return grid;
        }

        private void UpdatePreview()
        {
            _previewText.Text = Stage2SettingsWindow.FormatPreview(_formatTextBox.Text);
        }
    }
}
