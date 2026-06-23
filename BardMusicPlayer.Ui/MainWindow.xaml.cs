/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license.
 * See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE
 * for full license information.
 */

using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Ui.Classic;
using BardMusicPlayer.Ui.Functions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; 
using System.Windows.Media;
using System.Windows.Shell;

namespace BardMusicPlayer.Ui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double ClassicTitleBarHeight = 30.0;

        // The Classic layout is designed around this logical client size.
        // MainViewScaleHost scales the full nested UI uniformly from here.
        private const double ClassicDesignWidth = 1100.0;
        private const double ClassicDesignHeight = 620.0;

        private bool _classicScaleEnabled;

        private static readonly string[] TitleBarResourceKeys =
        {
            "TitleBarWindowBackgroundBrush",
            "TitleBarBackgroundBrush",
            "TitleBarBorderBrush",
            "TitleBarTextBrush",
            "TitleBarButtonHoverBrush",
            "TitleBarButtonPressedBrush",
            "TitleBarCloseHoverBrush",
            "TitleBarClosePressedBrush"
        };

        private readonly Dictionary<string, Brush> _defaultTitleBarBrushes =
            new Dictionary<string, Brush>(StringComparer.Ordinal);

        public MainWindow()
        {
            EnsureDefaultThemeSelected();
            InitializeComponent();

            CaptureDefaultTitleBarPalette();
            Globals.Globals.OnConfigReload += Globals_OnConfigReload;

            Title =
                "LightAmpRedux Ver:" +
                Assembly.GetExecutingAssembly().GetName().Version +
                " - Jacked";

            if (!BmpPigeonhole.Instance.LastSkin.EndsWith(
                    ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                SwitchClassicStyle();
            }
            else
            {
                SwitchSkinnedStyle();
            }
        }

        private static void EnsureDefaultThemeSelected()
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            try
            {
                // The Default theme is always the portable theme beside the
                // released executable, regardless of a custom theme-browser
                // folder selected in Settings.
                string candidate =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Themes",
                        "BlackGlassNeon.xaml");

                candidate =
                    Path.GetFullPath(candidate);

                if (!File.Exists(candidate))
                {
                    // Keep LightAmp's built-in resources as a safe fallback.
                    return;
                }

                string currentTheme =
                    settings.LastSkin
                    ??
                    string.Empty;

                bool noThemeSelected =
                    string.IsNullOrWhiteSpace(
                        currentTheme);

                bool usingLegacyBlackGlassName =
                    !noThemeSelected
                    &&
                    string.Equals(
                        Path.GetFileName(currentTheme),
                        "BlackGlassNeonTheme_FeaturePack.xaml",
                        StringComparison.OrdinalIgnoreCase);

                bool usingPortableBlackGlassName =
                    !noThemeSelected
                    &&
                    string.Equals(
                        Path.GetFileName(currentTheme),
                        "BlackGlassNeon.xaml",
                        StringComparison.OrdinalIgnoreCase);

                if (noThemeSelected
                    ||
                    usingLegacyBlackGlassName
                    ||
                    usingPortableBlackGlassName)
                {
                    settings.LastSkin =
                        candidate;
                }

                // A different explicit user selection remains untouched.
                settings.DefaultThemeInitialized =
                    true;
            }
            catch
            {
                // A missing or invalid optional theme must never stop LightAmp
                // from starting with its built-in resources.
            }
        }

        /// <summary>
        /// Loads LightAmp's Classic WPF interface with a custom dark title bar.
        /// </summary>
        public void SwitchClassicStyle()
        {
            DataContext = new Classic_MainView();

            ApplySelectedThemeToTitleBar();

            AllowsTransparency = false;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;

            // WindowChrome preserves resize borders around the frameless window.
            WindowChrome.SetWindowChrome(
                this,
                new WindowChrome
                {
                    CaptionHeight = ClassicTitleBarHeight,
                    ResizeBorderThickness = new Thickness(6),
                    CornerRadius = new CornerRadius(0),
                    GlassFrameThickness = new Thickness(0),
                    UseAeroCaptionButtons = false
                });

            CustomTitleBar.Visibility = Visibility.Visible;
            CustomTitleBarRow.Height = new GridLength(ClassicTitleBarHeight);

            ConfigureClassicScaleHost();

            MinWidth = 520;
            MinHeight = 430;

            // Only set a practical default size when the window is still using
            // LightAmp's original small startup dimensions.
            if (Width < 900)
                Width = 1100;

            if (Height < 600)
                Height = 650;

            UpdateMaximizeRestoreButton();
        }

        /// <summary>
        /// Loads an old DLL/WinAmp skin without the custom Classic title bar.
        /// </summary>
        public void SwitchSkinnedStyle()
        {
            try
            {
                CustomTitleBar.Visibility = Visibility.Collapsed;
                CustomTitleBarRow.Height = new GridLength(0);

                ConfigureSkinnedScaleHost();

                // DLL skins already provide their own frame and drag behavior.
                WindowChrome.SetWindowChrome(this, null);

                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Height = 174;
                Width = 412;
                MinHeight = 0;
                MinWidth = 0;
                ResizeMode = ResizeMode.NoResize;

                var dll = Assembly.LoadFile(BmpPigeonhole.Instance.LastSkin);

                if (!LoadSkinDllDependencies())
                {
                    SwitchClassicStyle();
                    return;
                }

                var type = dll.GetType("Skin.Ui.Skin_MainView");

                if (type == null)
                    throw new TypeLoadException(
                        "Skin.Ui.Skin_MainView was not found in the selected skin.");

                var runnable = Activator.CreateInstance(type);
                DataContext = runnable;
                Application.Current.MainWindow = this;
            }
            catch (FileNotFoundException)
            {
                var result = MessageBox.Show(
                    "Skin not found.\r\nUsing default UI.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly);

                if (result == MessageBoxResult.OK)
                    SwitchClassicStyle();
            }
            catch (TargetInvocationException exception)
            {
                var result = MessageBox.Show(
                    "Skin error:\r\n" +
                    exception.Message +
                    "\r\n" +
                    exception.InnerException +
                    "\r\n" +
                    exception.HelpLink +
                    "\r\n",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly);

                if (result == MessageBoxResult.OK)
                    SwitchClassicStyle();
            }
            catch (Exception exception)
            {
                var result = MessageBox.Show(
                    "Skin error:\r\n" + exception.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly);

                if (result == MessageBoxResult.OK)
                    SwitchClassicStyle();
            }
        }

        private void Globals_OnConfigReload(
            object sender,
            EventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                RefreshWindowPresentation();
                return;
            }

            Dispatcher.BeginInvoke(
                new Action(RefreshWindowPresentation));
        }

        private void RefreshWindowPresentation()
        {
            ApplySelectedThemeToTitleBar();
            UpdateClassicScaleSurface();
        }

        private void ConfigureClassicScaleHost()
        {
            _classicScaleEnabled = true;

            // Preserve proportions. The design surface is resized to match
            // the available client aspect ratio, so Uniform fills the window
            // without horizontally stretching controls.
            MainViewScaleHost.Stretch =
                Stretch.Uniform;

            MainViewScaleHost.StretchDirection =
                StretchDirection.Both;

            MainViewScaleHost.HorizontalAlignment =
                HorizontalAlignment.Stretch;

            MainViewScaleHost.VerticalAlignment =
                VerticalAlignment.Stretch;

            MainViewContent.HorizontalContentAlignment =
                HorizontalAlignment.Stretch;

            MainViewContent.VerticalContentAlignment =
                VerticalAlignment.Stretch;

            UpdateClassicScaleSurface();
        }

        private void ConfigureSkinnedScaleHost()
        {
            _classicScaleEnabled = false;

            // Legacy skin DLLs already use their own exact 412 x 174 layout.
            MainViewScaleHost.Stretch =
                Stretch.None;

            MainViewScaleHost.StretchDirection =
                StretchDirection.DownOnly;

            MainViewScaleHost.HorizontalAlignment =
                HorizontalAlignment.Left;

            MainViewScaleHost.VerticalAlignment =
                VerticalAlignment.Top;

            MainViewContent.Width = 412;
            MainViewContent.Height = 174;
        }

        private void UpdateClassicScaleSurface()
        {
            if (!_classicScaleEnabled ||
                MainViewContent == null ||
                MainViewScaleContainer == null)
            {
                return;
            }

            double availableWidth =
                MainViewScaleContainer.ActualWidth;

            double availableHeight =
                MainViewScaleContainer.ActualHeight;

            if (availableWidth <= 1 ||
                availableHeight <= 1)
            {
                availableWidth = ClassicDesignWidth;
                availableHeight = ClassicDesignHeight;
            }

            double userScale =
                Math.Max(
                    0.65,
                    Math.Min(
                        1.75,
                        BmpPigeonhole.Instance.ThemeUiScale));

            double targetAspect =
                availableWidth / availableHeight;

            double baseAspect =
                ClassicDesignWidth / ClassicDesignHeight;

            double logicalWidth;
            double logicalHeight;

            if (targetAspect >= baseAspect)
            {
                // Wider window: keep the logical height stable and give the
                // responsive layout more horizontal room.
                logicalHeight =
                    ClassicDesignHeight / userScale;

                logicalWidth =
                    logicalHeight * targetAspect;
            }
            else
            {
                // Taller or narrower window: keep the logical width stable and
                // give the responsive layout more vertical room.
                logicalWidth =
                    ClassicDesignWidth / userScale;

                logicalHeight =
                    logicalWidth / targetAspect;
            }

            MainViewContent.Width =
                Math.Max(720, logicalWidth);

            MainViewContent.Height =
                Math.Max(420, logicalHeight);
        }

        private void MainViewScaleContainer_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            UpdateClassicScaleSurface();
        }

        /// <summary>
        /// Reads colors from the selected external XAML theme and applies
        /// them only to MainWindow's custom title bar.
        ///
        /// Dedicated TitleBar* brush keys are optional. When a theme does
        /// not define them, familiar LightAmp theme keys are used instead.
        /// </summary>
        private void ApplySelectedThemeToTitleBar()
        {
            ResetTitleBarPalette();

            string selectedTheme = BmpPigeonhole.Instance.LastSkin;

            if (string.IsNullOrWhiteSpace(selectedTheme) ||
                !selectedTheme.EndsWith(
                    ".xaml",
                    StringComparison.OrdinalIgnoreCase))
            {
                UiCustomizationManager.ApplyTitleBarOverrides(this);
                return;
            }

            string themePath = ResolveThemePath(selectedTheme);

            if (string.IsNullOrWhiteSpace(themePath) ||
                !File.Exists(themePath))
            {
                UiCustomizationManager.ApplyTitleBarOverrides(this);
                return;
            }

            try
            {
                var themeDictionary = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };

                SetThemeBrush(
                    "TitleBarWindowBackgroundBrush",
                    themeDictionary,
                    "TitleBarWindowBackgroundBrush",
                    "BackgroundBrush",
                    "PanelBackgroundBrush");

                SetThemeBrush(
                    "TitleBarBackgroundBrush",
                    themeDictionary,
                    "TitleBarBackgroundBrush",
                    "PanelBackgroundBrush",
                    "BackgroundBrush");

                SetThemeBrush(
                    "TitleBarBorderBrush",
                    themeDictionary,
                    "TitleBarBorderBrush",
                    "ButtonBorderBrush",
                    "BorderBrush",
                    "AccentOrangeSelectedBrush");

                SetThemeBrush(
                    "TitleBarTextBrush",
                    themeDictionary,
                    "TitleBarTextBrush",
                    "TextMainBrush",
                    "TextBrush");

                SetThemeBrush(
                    "TitleBarButtonHoverBrush",
                    themeDictionary,
                    "TitleBarButtonHoverBrush",
                    "AccentOrangeSelectedBrush",
                    "AccentOrangeBrush",
                    "ControlElementBrush");

                SetThemeBrush(
                    "TitleBarButtonPressedBrush",
                    themeDictionary,
                    "TitleBarButtonPressedBrush",
                    "AccentOrangeHoverBrush",
                    "AccentOrangeSelectedBrush",
                    "ControlElementBrush");

                SetThemeBrush(
                    "TitleBarCloseHoverBrush",
                    themeDictionary,
                    "TitleBarCloseHoverBrush",
                    "AccentRedBrush",
                    "AccentOrangeBrush");

                SetThemeBrush(
                    "TitleBarClosePressedBrush",
                    themeDictionary,
                    "TitleBarClosePressedBrush",
                    "AccentRedBrush",
                    "AccentOrangeSelectedBrush");
            }
            catch
            {
                // A broken optional title-bar palette must never stop the
                // main LightAmp window from opening. Startup defaults remain.
                ResetTitleBarPalette();
            }

            UiCustomizationManager.ApplyTitleBarOverrides(this);
        }

        private static string ResolveThemePath(string configuredPath)
        {
            try
            {
                string resolved = configuredPath;

                if (!Path.IsPathRooted(resolved))
                {
                    resolved = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        resolved);
                }

                return Path.GetFullPath(resolved);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void CaptureDefaultTitleBarPalette()
        {
            foreach (string key in TitleBarResourceKeys)
            {
                var brush = Resources[key] as Brush;
                if (brush == null)
                    continue;

                _defaultTitleBarBrushes[key] =
                    CloneBrush(brush);
            }
        }

        private void ResetTitleBarPalette()
        {
            foreach (KeyValuePair<string, Brush> entry
                     in _defaultTitleBarBrushes)
            {
                Resources[entry.Key] =
                    CloneBrush(entry.Value);
            }
        }

        private void SetThemeBrush(
            string targetKey,
            ResourceDictionary dictionary,
            params string[] candidateKeys)
        {
            Brush selectedBrush = null;

            foreach (string candidateKey in candidateKeys)
            {
                selectedBrush =
                    TryGetBrush(dictionary, candidateKey);

                if (selectedBrush != null)
                    break;
            }

            if (selectedBrush == null)
                return;

            Resources[targetKey] =
                CloneBrush(selectedBrush);
        }

        private static Brush TryGetBrush(
            ResourceDictionary dictionary,
            string key)
        {
            try
            {
                if (!dictionary.Contains(key))
                    return null;

                return dictionary[key] as Brush;
            }
            catch
            {
                return null;
            }
        }

        private static Brush CloneBrush(Brush brush)
        {
            if (brush == null)
                return null;

            try
            {
                return brush.CloneCurrentValue();
            }
            catch
            {
                return brush;
            }
        }

        private void CustomTitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                // Restore before dragging so the window follows the pointer
                // naturally when pulled down from a maximized state.
                Point mousePosition = e.GetPosition(this);
                double horizontalRatio =
                    ActualWidth <= 0
                        ? 0.5
                        : mousePosition.X / ActualWidth;

                WindowState = WindowState.Normal;

                Left = mousePosition.X -
                       (RestoreBounds.Width * horizontalRatio);

                Top = 0;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if the mouse button is released between
                // the event and the native drag operation.
            }
        }

        private void MinimizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(
            object sender,
            EventArgs e)
        {
            UpdateMaximizeRestoreButton();

            // Avoid the custom frame touching the monitor edge when maximized.
            WindowFrame.BorderThickness =
                WindowState == WindowState.Maximized
                    ? new Thickness(0)
                    : new Thickness(1);
        }

        private void ToggleMaximizeRestore()
        {
            if (ResizeMode == ResizeMode.NoResize)
                return;

            WindowState =
                WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
        }

        private void UpdateMaximizeRestoreButton()
        {
            if (MaximizeRestoreButton == null)
                return;

            bool isMaximized =
                WindowState == WindowState.Maximized;

            MaximizeRestoreButton.Content =
                isMaximized ? "❐" : "□";

            MaximizeRestoreButton.ToolTip =
                isMaximized ? "Restore" : "Maximize";
        }

        protected override void OnClosing(
            System.ComponentModel.CancelEventArgs e)
        {
            Globals.Globals.OnConfigReload -= Globals_OnConfigReload;
            for (int index = App.Current.Windows.Count - 1;
                 index >= 0;
                 index--)
            {
                try
                {
                    App.Current.Windows[index].Close();
                }
                catch
                {
                    // Preserve the original best-effort shutdown behavior.
                }
            }

            base.OnClosing(e);
        }

        private bool LoadSkinDllDependencies()
        {
            string loadDirectory =
                Path.GetDirectoryName(BmpPigeonhole.Instance.LastSkin);

            if (string.IsNullOrWhiteSpace(loadDirectory) ||
                !Directory.Exists(loadDirectory))
            {
                return false;
            }

            var existingDependencies =
                Assembly.GetExecutingAssembly()
                    .GetReferencedAssemblies()
                    .Select(assembly => assembly.Name);

            foreach (string candidate in Directory.GetFiles(loadDirectory))
            {
                if (!candidate.EndsWith(
                        ".dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fileName =
                    Path.GetFileNameWithoutExtension(candidate);

                if (string.Equals(
                        fileName,
                        "Skin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (existingDependencies.Contains(fileName))
                    continue;

                try
                {
                    Assembly.LoadFile(candidate);
                }
                catch (BadImageFormatException)
                {
                    // Ignore native or otherwise incompatible DLLs.
                }
                catch (Exception exception)
                {
                    var result = MessageBox.Show(
                        "Error loading skin dependency DLL:\r\n" +
                        exception.Message,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error,
                        MessageBoxResult.Yes,
                        MessageBoxOptions.DefaultDesktopOnly);

                    if (result == MessageBoxResult.OK)
                        return false;
                }
            }

            return true;
        }
    }
}
