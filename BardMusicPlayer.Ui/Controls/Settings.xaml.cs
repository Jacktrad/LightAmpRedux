/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using BardMusicPlayer.Maestro;
using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Seer;
using BardMusicPlayer.Ui.Functions;
using BardMusicPlayer.Ui.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


namespace BardMusicPlayer.Ui.Controls
{
    /// <summary>
    /// Interaktionslogik für ConfigView.xaml
    /// </summary>
    public partial class Settings : UserControl
    {
        private bool _loadingThemeList;
        private bool _loadingPreferenceControls;
        private bool _loadingObsSettings;
        private readonly DispatcherTimer _preferenceReloadTimer =
            new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };

        public sealed class ThemeChoice
        {
            public ThemeChoice(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
            }

            public string Name { get; }
            public string FullPath { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        public Settings()
        {
            // Sliders and ComboBoxes may raise change events while XAML is
            // still constructing the control. Suppress persistence until
            // InitializeComponent has completed.
            _loadingPreferenceControls = true;
            _loadingObsSettings = true;
            InitializeComponent();
            _preferenceReloadTimer.Tick += PreferenceReloadTimer_Tick;
            _loadingPreferenceControls = false;
            _loadingObsSettings = false;

            if (!BmpPigeonhole.Instance.UsePluginForKeyOutput)
                this.KeyDown += Classic_MainView_KeyDown;
        }

        /// <summary>
        /// load the settings
        /// </summary>
        public void LoadConfig(bool reload = false)
        {
            //Orchestra
            LocalOrchestraBox.IsChecked = BmpPigeonhole.Instance.LocalOrchestra;
            KeepTrackSettingsBox.IsChecked = BmpPigeonhole.Instance.EnsembleKeepTrackSetting;
            IgnoreProgchangeBox.IsChecked = BmpPigeonhole.Instance.IgnoreProgChange;
            Autostart_source.SelectedIndex = BmpPigeonhole.Instance.AutostartMethod;
            AutoEquipBox.IsChecked = BmpPigeonhole.Instance.AutoEquipBards;
            AutoselectHostBox.IsChecked = BmpPigeonhole.Instance.AutoselectHost;
            AutoLoadPerformersBox.IsChecked = BmpPigeonhole.Instance.AutoLoadPerformers;
            LyricsLatencyBox.IsChecked = BmpPigeonhole.Instance.UseLyricsOffset;
            StartBardIndividuallyBox.IsChecked = BmpPigeonhole.Instance.EnsembleStartIndividual;

            //Playback
            HoldNotesBox.IsChecked = BmpPigeonhole.Instance.HoldNotes;
            ForcePlaybackBox.IsChecked = BmpPigeonhole.Instance.ForcePlayback;
            if (!reload)
            {
                MIDI_Input_DeviceBox.Items.Clear();
                MIDI_Input_DeviceBox.ItemsSource = Maestro.Utils.MidiInput.ReloadMidiInputDevices();
                MIDI_Input_DeviceBox.SelectedIndex = BmpPigeonhole.Instance.MidiInputDev + 1;
            }
            LiveMidiDelay.IsChecked = BmpPigeonhole.Instance.LiveMidiPlayDelay;
            NoteOffsetBox.IsChecked = BmpPigeonhole.Instance.UseNoteOffset;
            ChannelToProg.IsChecked = BmpPigeonhole.Instance.ChannelToProgram;

            //Misc
            AMPInFrontBox.IsChecked = BmpPigeonhole.Instance.BringBMPtoFront;
            MultiBox_Box.IsChecked = BmpPigeonhole.Instance.EnableMultibox;
            AutoequipDalamud.IsChecked = BmpPigeonhole.Instance.UsePluginForInstrumentOpen;
            AutoAcceptInvite.IsChecked = BmpPigeonhole.Instance.AutoAcceptPartyInvite;
            EnableSynthLimit.IsChecked = BmpPigeonhole.Instance.EnableSynthVoiceLimiter;

            if (BmpPigeonhole.Instance.BMPApiKey != "")
            {
                ApiBtnToggleShow.IsChecked = true;
                ApiBtnToggleShow_Checked(null, null);
            }

            MidiBardComp.IsChecked = BmpPigeonhole.Instance.MidiBardCompatMode;
            SongHistoryBox.IsChecked = BmpPigeonhole.Instance.EnableSongHistory;

            LoadThemeChoices();
            LoadVisualizerAndThemePreferences();
            LoadObsSettings();

            if (BmpPigeonhole.Instance.UsePluginForKeyOutput)
            {
                Sp_DalamudKeyOut.Visibility = Visibility.Visible;
                Sp_DalamudKeyOut.IsChecked = BmpPigeonhole.Instance.UsePluginForKeyOutput;
            }
        }

        #region Orchestra
        private void LocalOrchestraBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.LocalOrchestra = LocalOrchestraBox.IsChecked ?? false;
        }

        private void KeepTrackSettingsBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.EnsembleKeepTrackSetting = KeepTrackSettingsBox.IsChecked ?? false;
        }

        private void IgnoreProgchangeBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.IgnoreProgChange = IgnoreProgchangeBox.IsChecked ?? false;
        }

        private void Autostart_source_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int d = Autostart_source.SelectedIndex;
            BmpPigeonhole.Instance.AutostartMethod = (int)d;
        }

        private void AutoEquipBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.AutoEquipBards = AutoEquipBox.IsChecked ?? false;
            Globals.Globals.ReloadConfig();
        }

        private void AutoselectHost_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.AutoselectHost = AutoselectHostBox.IsChecked ?? false;
            Globals.Globals.ReloadConfig();
        }

        private void AutoLoadPerformers_Checked(object sender, RoutedEventArgs e)
        {
            if (!(bool)AutoLoadPerformersBox.IsChecked)
                BmpPigeonhole.Instance.LastLoadedPerformerProfile = "";
            BmpPigeonhole.Instance.AutoLoadPerformers = AutoLoadPerformersBox.IsChecked ?? false;
        }

        private void LyricsLatency_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.UseLyricsOffset = LyricsLatencyBox.IsChecked ?? false;
        }

        private void StartBardIndividually_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.EnsembleStartIndividual = StartBardIndividuallyBox.IsChecked ?? false;
        }
        #endregion

        #region Playback
        private void Hold_Notes_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.HoldNotes = HoldNotesBox.IsChecked ?? false;
        }

        private void Force_Playback_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.ForcePlayback = ForcePlaybackBox.IsChecked ?? false;
        }

        private void MIDI_Input_Device_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var d = (KeyValuePair<int, string>)MIDI_Input_DeviceBox.SelectedItem;
            BmpPigeonhole.Instance.MidiInputDev = d.Key;
            if (d.Key == -1)
            {
                BmpMaestro.Instance.CloseInputDevice();
                return;
            }

            BmpMaestro.Instance.OpenInputDevice(d.Key);
        }

        private void LiveMidiDelay_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.LiveMidiPlayDelay = (LiveMidiDelay.IsChecked ?? false);
        }

        private void NoteOffsetBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.UseNoteOffset = (NoteOffsetBox.IsChecked ?? false);
            Globals.Globals.ReloadConfig();
        }
        #endregion

        #region Misc
        private void AMPInFrontBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.BringBMPtoFront = AMPInFrontBox.IsChecked ?? false;
        }

        private void MultiBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!BmpPigeonhole.Instance.EnableMultibox)
            {
                Task.Run(() =>
                {
                    foreach (var game in BmpSeer.Instance.Games.Values)
                        game.KillMutant(true);
                });
            }
            BmpPigeonhole.Instance.EnableMultibox = MultiBox_Box.IsChecked ?? false;
        }

        private void AutoequipDalamud_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.UsePluginForInstrumentOpen = AutoequipDalamud.IsChecked ?? false;
        }

        private void AutoAcceptInvite_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.AutoAcceptPartyInvite = AutoAcceptInvite.IsChecked ?? false;
        }

        private void EnableSynthLimit_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.EnableSynthVoiceLimiter = EnableSynthLimit.IsChecked ?? false;
        }

        private void MidiBard_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.MidiBardCompatMode = MidiBardComp.IsChecked ?? false;
        }

        private void SongHistoryBox_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.EnableSongHistory = SongHistoryBox.IsChecked ?? false;
        }

        private void LoadThemeChoices(bool selectFirstThemeWhenMissing = false)
        {
            _loadingThemeList = true;

            try
            {
                string folder = GetThemeFolder();
                ThemeFolderBox.Text = folder;
                ThemeFolderBox.ToolTip = folder;

                ThemeComboBox.Items.Clear();

                string releaseDefaultTheme =
                    GetReleaseDefaultThemePath();

                ThemeComboBox.Items.Add(
                    new ThemeChoice(
                        "Default theme",
                        File.Exists(releaseDefaultTheme)
                            ? releaseDefaultTheme
                            : string.Empty));

                if (Directory.Exists(folder))
                {
                    bool releaseDefaultExists =
                        File.Exists(
                            releaseDefaultTheme);

                    foreach (string file in Directory
                        .GetFiles(folder, "*.xaml", SearchOption.TopDirectoryOnly)
                        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                    {
                        string fileName =
                            Path.GetFileName(file);

                        // The first item already represents BlackGlassNeon.xaml.
                        if (releaseDefaultExists
                            &&
                            string.Equals(
                                Path.GetFullPath(file),
                                releaseDefaultTheme,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Hide the old duplicate filename when the new portable
                        // default exists. This also cleans up patch-only installs.
                        if (releaseDefaultExists
                            &&
                            string.Equals(
                                fileName,
                                "BlackGlassNeonTheme_FeaturePack.xaml",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ThemeComboBox.Items.Add(
                            new ThemeChoice(
                                Path.GetFileNameWithoutExtension(file),
                                file));
                    }
                }

                int selectedIndex = 0;
                string currentTheme = BmpPigeonhole.Instance.LastSkin ?? string.Empty;

                for (int index = 0; index < ThemeComboBox.Items.Count; index++)
                {
                    var choice = ThemeComboBox.Items[index] as ThemeChoice;
                    if (choice != null && string.Equals(
                        choice.FullPath,
                        currentTheme,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }

                if (selectFirstThemeWhenMissing &&
                    selectedIndex == 0 &&
                    ThemeComboBox.Items.Count > 1)
                {
                    selectedIndex = 1;
                }

                ThemeComboBox.SelectedIndex = selectedIndex;
                UpdateThemeToolTip();
            }
            finally
            {
                _loadingThemeList = false;
            }

            if (selectFirstThemeWhenMissing)
                ApplySelectedTheme();
        }

        private const string DefaultThemeDirectory = "Themes";
        private const string ReleaseDefaultThemeFile = "BlackGlassNeon.xaml";

        private static string GetReleaseDefaultThemePath()
        {
            return Path.GetFullPath(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    DefaultThemeDirectory,
                    ReleaseDefaultThemeFile));
        }

        private string GetThemeFolder()
        {
            string configuredFolder = BmpPigeonhole.Instance.ThemeDirectory;

            // An empty value means the portable folder beside LightAmp.exe:
            // .\Themes\
            if (string.IsNullOrWhiteSpace(configuredFolder))
            {
                configuredFolder = DefaultThemeDirectory;
                BmpPigeonhole.Instance.ThemeDirectory = configuredFolder;
            }

            string resolvedFolder = configuredFolder;

            if (!Path.IsPathRooted(resolvedFolder))
            {
                resolvedFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    resolvedFolder);
            }

            resolvedFolder = Path.GetFullPath(resolvedFolder);

            if (!Directory.Exists(resolvedFolder))
            {
                try
                {
                    Directory.CreateDirectory(resolvedFolder);
                }
                catch
                {
                    // Continue showing the folder even when it cannot be created.
                }
            }

            return resolvedFolder;
        }

        private void ThemeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingThemeList)
                return;

            ApplySelectedTheme();
        }

        private void ApplySelectedTheme()
        {
            var choice = ThemeComboBox.SelectedItem as ThemeChoice;
            if (choice == null)
                return;

            string selectedTheme = choice.FullPath ?? string.Empty;
            string currentTheme = BmpPigeonhole.Instance.LastSkin ?? string.Empty;

            UpdateThemeToolTip();

            if (string.Equals(
                selectedTheme,
                currentTheme,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BmpPigeonhole.Instance.LastSkin = selectedTheme;
            Globals.Globals.ReloadConfig();
        }

        private void ThemeFolderBrowseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            // OpenFileDialog is used in folder-selection mode so this remains
            // dependency-free on .NET Framework WPF.
            var dialog = new OpenFileDialog
            {
                Title = "Choose the folder containing LightAmp XAML themes",
                Filter = "Folder selection|*.folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select this folder"
            };

            string currentFolder = GetThemeFolder();
            if (Directory.Exists(currentFolder))
                dialog.InitialDirectory = currentFolder;

            if (dialog.ShowDialog() != true)
                return;

            string selectedFolder = Path.GetDirectoryName(dialog.FileName);
            if (string.IsNullOrWhiteSpace(selectedFolder) ||
                !Directory.Exists(selectedFolder))
            {
                return;
            }

            BmpPigeonhole.Instance.ThemeDirectory = selectedFolder;
            LoadThemeChoices(selectFirstThemeWhenMissing: true);

            if (ThemeComboBox.Items.Count <= 1)
            {
                MessageBox.Show(
                    "No .xaml theme files were found in that folder.",
                    "LightAmp Themes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ThemeRefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadThemeChoices();
        }

        private void UpdateThemeToolTip()
        {
            var choice = ThemeComboBox.SelectedItem as ThemeChoice;
            ThemeComboBox.ToolTip = choice == null
                ? "Use the release-folder BlackGlassNeon default theme"
                : string.IsNullOrWhiteSpace(choice.FullPath)
                    ? "BlackGlassNeon.xaml was not found in the release Themes folder"
                    : choice.FullPath;
        }
        #endregion


        private void LoadVisualizerAndThemePreferences()
        {
            _loadingPreferenceControls = true;

            try
            {
                ShowPianoRollBox.IsChecked =
                    BmpPigeonhole.Instance.VisualizerShowPianoRoll;
                ShowKeyboardBox.IsChecked =
                    BmpPigeonhole.Instance.VisualizerShowKeyboard;
                PianoRollHeightSlider.Value =
                    BmpPigeonhole.Instance.VisualizerPianoRollHeight;
                NoteThicknessSlider.Value =
                    BmpPigeonhole.Instance.VisualizerNoteThickness;
                ShowNoteNamesBox.IsChecked =
                    BmpPigeonhole.Instance.VisualizerShowNoteNames;
                ShowBarLinesBox.IsChecked =
                    BmpPigeonhole.Instance.VisualizerShowBarLines;
                ShowBeatLinesBox.IsChecked =
                    BmpPigeonhole.Instance.VisualizerShowBeatLines;
                VisualizerTrackModeComboBox.SelectedIndex =
                    BmpPigeonhole.Instance.VisualizerAllTracks ? 0 : 1;

                GlowStrengthSlider.Value =
                    BmpPigeonhole.Instance.ThemeGlowStrength;
                ThemeAnimationsBox.IsChecked =
                    BmpPigeonhole.Instance.ThemeAnimationsEnabled;
                CornerRadiusSlider.Value =
                    BmpPigeonhole.Instance.ThemeCornerRadius;
                UiScaleSlider.Value =
                    BmpPigeonhole.Instance.ThemeUiScale;
                DarkenBackgroundSlider.Value =
                    BmpPigeonhole.Instance.ThemeDarkenAmount;

                SelectFontSize(BmpPigeonhole.Instance.ThemeFontSize);
                UpdateAccentPreview();
                UpdatePreferenceValueLabels();
            }
            finally
            {
                _loadingPreferenceControls = false;
            }
        }

        private void SelectFontSize(double size)
        {
            for (int index = 0;
                 index < ThemeFontSizeComboBox.Items.Count;
                 index++)
            {
                ComboBoxItem item =
                    ThemeFontSizeComboBox.Items[index] as ComboBoxItem;

                if (item == null)
                    continue;

                double parsed;
                if (!double.TryParse(
                        Convert.ToString(item.Content),
                        out parsed))
                {
                    continue;
                }

                if (Math.Abs(parsed - size) < 0.01)
                {
                    ThemeFontSizeComboBox.SelectedIndex = index;
                    return;
                }
            }

            ThemeFontSizeComboBox.SelectedIndex = 2;
        }

        private void UpdatePreferenceValueLabels()
        {
            if (PianoRollHeightValueText != null)
                PianoRollHeightValueText.Text =
                    Math.Round(PianoRollHeightSlider.Value) + " px";

            if (NoteThicknessValueText != null)
                NoteThicknessValueText.Text =
                    NoteThicknessSlider.Value.ToString("0.0") + " px";

            if (GlowStrengthValueText != null)
                GlowStrengthValueText.Text =
                    GlowStrengthSlider.Value.ToString("0.0") + "×";

            if (CornerRadiusValueText != null)
                CornerRadiusValueText.Text =
                    Math.Round(CornerRadiusSlider.Value) + " px";

            if (UiScaleValueText != null)
                UiScaleValueText.Text =
                    Math.Round(UiScaleSlider.Value * 100) + "%";

            if (DarkenBackgroundValueText != null)
                DarkenBackgroundValueText.Text =
                    Math.Round(DarkenBackgroundSlider.Value * 100) + "%";
        }

        private void UpdateAccentPreview()
        {
            string value =
                BmpPigeonhole.Instance.ThemeAccentColor ?? string.Empty;

            AccentColorTextBox.Text =
                string.IsNullOrWhiteSpace(value)
                    ? "Theme default"
                    : value;

            try
            {
                AccentColorPreview.Background =
                    string.IsNullOrWhiteSpace(value)
                        ? TryFindResource("AccentOrangeBrush") as Brush
                        : new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(value));
            }
            catch
            {
                AccentColorPreview.Background =
                    TryFindResource("AccentOrangeBrush") as Brush;
            }
        }

        private void VisualizerToggle_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingPreferenceControls)
                return;

            SaveVisualizerSettings();
        }

        private void VisualizerSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePreferenceValueLabels();

            if (_loadingPreferenceControls)
                return;

            SaveVisualizerSettings();
        }

        private void VisualizerTrackMode_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingPreferenceControls)
                return;

            SaveVisualizerSettings();
        }

        private void SaveVisualizerSettings()
        {
            BmpPigeonhole.Instance.VisualizerShowPianoRoll =
                ShowPianoRollBox.IsChecked ?? true;
            BmpPigeonhole.Instance.VisualizerShowKeyboard =
                ShowKeyboardBox.IsChecked ?? true;
            BmpPigeonhole.Instance.VisualizerPianoRollHeight =
                PianoRollHeightSlider.Value;
            BmpPigeonhole.Instance.VisualizerNoteThickness =
                NoteThicknessSlider.Value;
            BmpPigeonhole.Instance.VisualizerShowNoteNames =
                ShowNoteNamesBox.IsChecked ?? false;
            BmpPigeonhole.Instance.VisualizerShowBarLines =
                ShowBarLinesBox.IsChecked ?? true;
            BmpPigeonhole.Instance.VisualizerShowBeatLines =
                ShowBeatLinesBox.IsChecked ?? true;
            BmpPigeonhole.Instance.VisualizerAllTracks =
                VisualizerTrackModeComboBox.SelectedIndex != 1;

            SchedulePreferenceReload();
        }

        private void ResetVisualizerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.VisualizerShowPianoRoll = true;
            BmpPigeonhole.Instance.VisualizerShowKeyboard = true;
            BmpPigeonhole.Instance.VisualizerPianoRollHeight = 150.0;
            BmpPigeonhole.Instance.VisualizerNoteThickness = 5.0;
            BmpPigeonhole.Instance.VisualizerShowNoteNames = false;
            BmpPigeonhole.Instance.VisualizerShowBarLines = true;
            BmpPigeonhole.Instance.VisualizerShowBeatLines = true;
            BmpPigeonhole.Instance.VisualizerAllTracks = true;

            LoadVisualizerAndThemePreferences();
            Globals.Globals.ReloadConfig();
        }

        private void ThemeOverrideSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            UpdatePreferenceValueLabels();

            if (_loadingPreferenceControls)
                return;

            SaveThemeOverrides();
        }

        private void ThemeAnimationsBox_Checked(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingPreferenceControls)
                return;

            SaveThemeOverrides();
        }

        private void ThemeFontSizeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingPreferenceControls)
                return;

            SaveThemeOverrides();
        }

        private void SaveThemeOverrides()
        {
            BmpPigeonhole.Instance.ThemeGlowStrength =
                GlowStrengthSlider.Value;
            BmpPigeonhole.Instance.ThemeAnimationsEnabled =
                ThemeAnimationsBox.IsChecked ?? true;
            BmpPigeonhole.Instance.ThemeCornerRadius =
                CornerRadiusSlider.Value;
            BmpPigeonhole.Instance.ThemeUiScale =
                UiScaleSlider.Value;
            BmpPigeonhole.Instance.ThemeDarkenAmount =
                DarkenBackgroundSlider.Value;

            ComboBoxItem fontItem =
                ThemeFontSizeComboBox.SelectedItem as ComboBoxItem;

            double fontSize;
            if (fontItem != null &&
                double.TryParse(
                    Convert.ToString(fontItem.Content),
                    out fontSize))
            {
                BmpPigeonhole.Instance.ThemeFontSize = fontSize;
            }

            SchedulePreferenceReload();
        }

        private void AccentColorButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new AccentColorPickerDialog(
                BmpPigeonhole.Instance.ThemeAccentColor)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
                return;

            BmpPigeonhole.Instance.ThemeAccentColor =
                dialog.SelectedHex ?? string.Empty;

            UpdateAccentPreview();
            Globals.Globals.ReloadConfig();
        }

        private void ResetThemeOverrides_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.ThemeAccentColor = "";
            BmpPigeonhole.Instance.ThemeGlowStrength = 1.0;
            BmpPigeonhole.Instance.ThemeAnimationsEnabled = true;
            BmpPigeonhole.Instance.ThemeCornerRadius = 10.0;
            BmpPigeonhole.Instance.ThemeUiScale = 1.0;
            BmpPigeonhole.Instance.ThemeFontSize = 12.0;
            BmpPigeonhole.Instance.ThemeDarkenAmount = 0.0;

            LoadVisualizerAndThemePreferences();
            Globals.Globals.ReloadConfig();
        }

        private void SchedulePreferenceReload()
        {
            _preferenceReloadTimer.Stop();
            _preferenceReloadTimer.Start();
        }

        private void PreferenceReloadTimer_Tick(
            object sender,
            EventArgs e)
        {
            _preferenceReloadTimer.Stop();
            Globals.Globals.ReloadConfig();
        }

        private void LoadObsSettings()
        {
            _loadingObsSettings = true;

            try
            {
                ObsOutputEnabledBox.IsChecked =
                    BmpPigeonhole.Instance.ObsOutputEnabled;

                ObsOutputFolderBox.Text =
                    ObsOutputService.GetOutputDirectory();

                ObsOutputFolderBox.ToolTip =
                    ObsOutputService.GetOutputDirectory();

                ObsNowPlayingTemplateBox.Text =
                    BmpPigeonhole.Instance.ObsNowPlayingTemplate
                    ?? "{song}";

                ObsHistoryTemplateBox.Text =
                    BmpPigeonhole.Instance.ObsHistoryTemplate
                    ?? "{song}";

                ObsHistoryTimestampBox.IsChecked =
                    BmpPigeonhole.Instance.ObsHistoryShowTimestamp;

                ObsClearNowPlayingOnStopBox.IsChecked =
                    BmpPigeonhole.Instance.ObsClearNowPlayingOnStop;

                ObsHistoryLengthSlider.Value =
                    Math.Max(
                        1,
                        Math.Min(
                            20,
                            BmpPigeonhole.Instance.ObsHistoryLength));

                UpdateObsValueText();
                UpdateObsControlState();
            }
            finally
            {
                _loadingObsSettings = false;
            }
        }

        private void UpdateObsValueText()
        {
            if (ObsHistoryLengthValueText != null)
            {
                ObsHistoryLengthValueText.Text =
                    Math.Round(
                        ObsHistoryLengthSlider.Value)
                    .ToString();
            }

            if (ObsFileNamesText != null)
            {
                ObsFileNamesText.Text =
                    "Files: "
                    + ObsOutputService.NowPlayingFileName
                    + " and "
                    + ObsOutputService.HistoryFileName;
            }
        }

        private void UpdateObsControlState()
        {
            bool enabled =
                ObsOutputEnabledBox.IsChecked == true;

            ObsNowPlayingTemplateBox.IsEnabled = enabled;
            ObsHistoryTemplateBox.IsEnabled = enabled;
            ObsHistoryTimestampBox.IsEnabled = enabled;
            ObsClearNowPlayingOnStopBox.IsEnabled = enabled;
            ObsHistoryLengthSlider.IsEnabled = enabled;
        }

        private void ObsOutputEnabled_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingObsSettings)
                return;

            BmpPigeonhole.Instance.ObsOutputEnabled =
                ObsOutputEnabledBox.IsChecked == true;

            UpdateObsControlState();
            ObsOutputService.ApplySettings();
            Globals.Globals.ReloadConfig();
        }

        private void ObsOutputFolderBrowse_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose the folder for LightAmp OBS text files",
                Filter = "Folder selection|*.folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select this folder"
            };

            string currentFolder =
                ObsOutputService.GetOutputDirectory();

            if (Directory.Exists(currentFolder))
                dialog.InitialDirectory = currentFolder;

            if (dialog.ShowDialog() != true)
                return;

            string selectedFolder =
                Path.GetDirectoryName(dialog.FileName);

            if (string.IsNullOrWhiteSpace(selectedFolder))
                return;

            BmpPigeonhole.Instance.ObsOutputDirectory =
                selectedFolder;

            LoadObsSettings();
            ObsOutputService.ApplySettings();
            Globals.Globals.ReloadConfig();
        }

        private void ObsOutputFolderOpen_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string folder =
                    ObsOutputService.GetOutputDirectory();

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Could not open the OBS output folder.\r\n\r\n"
                    + exception.Message,
                    "LightAmp OBS Output",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void ObsTemplate_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_loadingObsSettings)
                return;

            BmpPigeonhole.Instance.ObsNowPlayingTemplate =
                string.IsNullOrWhiteSpace(
                    ObsNowPlayingTemplateBox.Text)
                    ? "{song}"
                    : ObsNowPlayingTemplateBox.Text;

            BmpPigeonhole.Instance.ObsHistoryTemplate =
                string.IsNullOrWhiteSpace(
                    ObsHistoryTemplateBox.Text)
                    ? "{song}"
                    : ObsHistoryTemplateBox.Text;

            SchedulePreferenceReload();
        }

        private void ObsHistoryLength_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateObsValueText();

            if (_loadingObsSettings)
                return;

            BmpPigeonhole.Instance.ObsHistoryLength =
                Math.Max(
                    1,
                    Math.Min(
                        20,
                        (int)Math.Round(
                            ObsHistoryLengthSlider.Value)));

            ObsOutputService.RewriteHistoryFile();
        }

        private void ObsOption_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingObsSettings)
                return;

            BmpPigeonhole.Instance.ObsHistoryShowTimestamp =
                ObsHistoryTimestampBox.IsChecked == true;

            BmpPigeonhole.Instance.ObsClearNowPlayingOnStop =
                ObsClearNowPlayingOnStopBox.IsChecked == true;

            SchedulePreferenceReload();
        }

        private void ObsRefreshFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            ObsOutputService.ApplySettings();
            Globals.Globals.ReloadConfig();

            MessageBox.Show(
                "The OBS text files were refreshed.",
                "LightAmp OBS Output",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ObsWriteTestFiles_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ObsOutputEnabledBox.IsChecked != true)
            {
                MessageBox.Show(
                    "Enable OBS output first.",
                    "LightAmp OBS Output",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            ObsOutputService.WritePreviewFiles();

            MessageBox.Show(
                "Example text was written to:\r\n\r\n"
                + ObsOutputService.GetOutputDirectory(),
                "LightAmp OBS Output",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ObsClearHistory_Click(
            object sender,
            RoutedEventArgs e)
        {
            ObsOutputService.ClearHistory();
        }

        private void ObsResetDefaults_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.ObsOutputDirectory = "OBS";
            BmpPigeonhole.Instance.ObsNowPlayingTemplate = "{song}";
            BmpPigeonhole.Instance.ObsHistoryTemplate = "{song}";
            BmpPigeonhole.Instance.ObsHistoryLength = 5;
            BmpPigeonhole.Instance.ObsHistoryShowTimestamp = false;
            BmpPigeonhole.Instance.ObsClearNowPlayingOnStop = false;

            LoadObsSettings();
            ObsOutputService.ApplySettings();
            Globals.Globals.ReloadConfig();
        }

        private void Info_Button_Click(
            object sender,
            RoutedEventArgs e)
        {
            var infoBox = new InfoBox
            {
                Owner = Window.GetWindow(this)
            };

            infoBox.Show();
        }

        private void Script_Button_Click(
            object sender,
            RoutedEventArgs e)
        {
            var macroLaunchpad = new MacroLaunchpad();

            macroLaunchpad.Visibility =
                Visibility.Visible;
        }

        private void Sp_DalamudKeyOut_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.UsePluginForKeyOutput = (Sp_DalamudKeyOut.IsChecked ?? true);
        }

        private void ChannelToProg_Checked(object sender, RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.ChannelToProgram = (ChannelToProg.IsChecked ?? true);
        }

        private static readonly Key[] KonamiCode = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right, Key.B, Key.A };
        private readonly Queue<Key> _inputKeys = new Queue<Key>();
        private void Classic_MainView_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsCompletedBy(e.Key))
            {
                Sp_DalamudKeyOut.Visibility = Visibility.Visible;
                this.KeyDown -= Classic_MainView_KeyDown;
            }
        }
        public bool IsCompletedBy(Key inputKey)
        {
            _inputKeys.Enqueue(inputKey);

            while (_inputKeys.Count > KonamiCode.Length)
                _inputKeys.Dequeue();

            return _inputKeys.SequenceEqual(KonamiCode);
        }

        private void BMPApiKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = sender as TextBox; 
            if (box == null) return;
            if (box.IsFocused)
                BmpPigeonhole.Instance.BMPApiKey = box.Text;
        }

        private void ApiBtnToggleShow_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)ApiBtnToggleShow.IsChecked)
                BMPApiKey.Text = new string('*', BmpPigeonhole.Instance.BMPApiKey.Length);
            else
                BMPApiKey.Text = BmpPigeonhole.Instance.BMPApiKey;
        }
    }
}
