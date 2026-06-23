using BardMusicPlayer.Pigeonhole;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BardMusicPlayer.Ui.Functions
{
    public static class UiCustomizationManager
    {
        private static readonly string[] LocalOverrideKeys =
        {
            "AccentOrangeBrush", "AccentOrangeHoverBrush",
            "AccentOrangeSelectedBrush", "AccentOrangeBorderHover",
            "MockupDividerBrush", "ProgressTrackBorderBrush",
            "KeyboardGlowWhiteKeyBrush", "KeyboardGlowBlackKeyBrush",
            "BackgroundBrush", "PanelBackgroundBrush", "DarkGridBrush",
            "ControlElementBrush", "MenuBackgroundBrush", "ChatBoxBackground",
            "MockupShellBrush", "MockupSidebarBrush", "MockupCardBrush",
            "MockupCardHoverBrush", "NeonSoftGlowEffect", "NeonGlowEffect",
            "MockupPrimaryGlowEffect", "ProgressBarGlowEffect",
            "UiCornerRadius", "UiSmallCornerRadius", "UiLargeCornerRadius",
            "UiPillCornerRadius", "UiTopCornerRadius",
            "UiAnimationsEnabled", "UiAnimationDuration"
        };

        private static readonly string[] DarkenableBrushKeys =
        {
            "BackgroundBrush", "PanelBackgroundBrush", "DarkGridBrush",
            "ControlElementBrush", "MenuBackgroundBrush", "ChatBoxBackground",
            "MockupShellBrush", "MockupSidebarBrush",
            "MockupCardBrush", "MockupCardHoverBrush"
        };

        public static void Apply(FrameworkElement root)
        {
            if (root == null)
                return;

            ClearLocalOverrides(root);
            BmpPigeonhole settings = BmpPigeonhole.Instance;

            // FrameworkElement itself has no FontSize property.
            // TextElement.FontSizeProperty is inheritable, so setting it on
            // the root applies the chosen base font size throughout the UI.
            root.SetValue(
                System.Windows.Documents.TextElement.FontSizeProperty,
                Clamp(settings.ThemeFontSize, 9.0, 24.0));

            // MainWindow's Viewbox now handles automatic window scaling and
            // combines it with ThemeUiScale. Keep the Classic root untransformed
            // here to avoid applying the scale twice.
            root.LayoutTransform = Transform.Identity;

            double radius = Clamp(settings.ThemeCornerRadius, 0.0, 32.0);
            root.Resources["UiCornerRadius"] = new CornerRadius(radius);
            root.Resources["UiSmallCornerRadius"] =
                new CornerRadius(Math.Max(0, radius * 0.75));
            root.Resources["UiLargeCornerRadius"] =
                new CornerRadius(Math.Max(0, radius * 1.25));
            root.Resources["UiPillCornerRadius"] =
                new CornerRadius(Math.Max(0, radius * 1.6));
            root.Resources["UiTopCornerRadius"] =
                new CornerRadius(radius, radius, 0, 0);

            root.Resources["UiAnimationsEnabled"] =
                settings.ThemeAnimationsEnabled;
            root.Resources["UiAnimationDuration"] =
                new Duration(settings.ThemeAnimationsEnabled
                    ? TimeSpan.FromMilliseconds(200)
                    : TimeSpan.Zero);

            ApplyAccent(root, settings.ThemeAccentColor);
            ApplyDarkening(root, settings.ThemeDarkenAmount);
            ApplyGlow(root, settings.ThemeGlowStrength);
        }

        public static void ApplyTitleBarOverrides(FrameworkElement window)
        {
            if (window == null)
                return;

            BmpPigeonhole settings = BmpPigeonhole.Instance;
            Color accent;

            if (TryParseColor(settings.ThemeAccentColor, out accent))
            {
                window.Resources["TitleBarBorderBrush"] =
                    new SolidColorBrush(accent);
                window.Resources["TitleBarButtonHoverBrush"] =
                    CreateAccentGradient(accent, 0.18);
                window.Resources["TitleBarButtonPressedBrush"] =
                    CreateAccentGradient(accent, -0.10);
            }

            double amount = Clamp(settings.ThemeDarkenAmount, 0.0, 0.85);
            if (amount <= 0.001)
                return;

            string[] keys =
            {
                "TitleBarWindowBackgroundBrush",
                "TitleBarBackgroundBrush"
            };

            foreach (string key in keys)
            {
                Brush source = window.TryFindResource(key) as Brush;
                if (source != null)
                    window.Resources[key] = DarkenBrush(source, amount);
            }
        }

        private static void ClearLocalOverrides(FrameworkElement root)
        {
            foreach (string key in LocalOverrideKeys)
            {
                if (root.Resources.Contains(key))
                    root.Resources.Remove(key);
            }
        }

        private static void ApplyAccent(FrameworkElement root, string hex)
        {
            Color accent;
            if (!TryParseColor(hex, out accent))
                return;

            root.Resources["AccentOrangeBrush"] =
                CreateAccentGradient(accent, 0.0);
            root.Resources["AccentOrangeHoverBrush"] =
                CreateAccentGradient(accent, 0.16);
            root.Resources["AccentOrangeSelectedBrush"] =
                CreateAccentGradient(accent, -0.18);
            root.Resources["AccentOrangeBorderHover"] =
                new SolidColorBrush(Lighten(accent, 0.32));
            root.Resources["MockupDividerBrush"] =
                CreateTransparentAccentGradient(accent);
            root.Resources["ProgressTrackBorderBrush"] =
                new SolidColorBrush(
                    Color.FromArgb(150, accent.R, accent.G, accent.B));
            root.Resources["KeyboardGlowWhiteKeyBrush"] =
                CreateAccentGradient(accent, 0.10);
            root.Resources["KeyboardGlowBlackKeyBrush"] =
                CreateAccentGradient(ShiftHueLike(accent), 0.05);
        }

        private static void ApplyDarkening(FrameworkElement root, double value)
        {
            double amount = Clamp(value, 0.0, 0.85);
            if (amount <= 0.001)
                return;

            foreach (string key in DarkenableBrushKeys)
            {
                Brush source = root.TryFindResource(key) as Brush;
                if (source != null)
                    root.Resources[key] = DarkenBrush(source, amount);
            }
        }

        private static void ApplyGlow(FrameworkElement root, double value)
        {
            double strength = Clamp(value, 0.0, 2.5);
            ApplyGlowEffect(root, "NeonSoftGlowEffect", 12, 0.72, strength);
            ApplyGlowEffect(root, "NeonGlowEffect", 20, 0.95, strength);
            ApplyGlowEffect(root, "MockupPrimaryGlowEffect", 25, 0.82, strength);
            ApplyGlowEffect(root, "ProgressBarGlowEffect", 11, 0.78, strength);
        }

        private static void ApplyGlowEffect(
            FrameworkElement root,
            string key,
            double baseBlur,
            double baseOpacity,
            double strength)
        {
            DropShadowEffect source =
                root.TryFindResource(key) as DropShadowEffect;
            if (source == null)
                return;

            DropShadowEffect result;
            try
            {
                result = source.CloneCurrentValue();
            }
            catch
            {
                result = new DropShadowEffect
                {
                    Color = source.Color,
                    ShadowDepth = source.ShadowDepth,
                    Direction = source.Direction
                };
            }

            result.BlurRadius =
                Math.Max(0, baseBlur * (0.35 + strength * 0.65));
            result.Opacity =
                Math.Min(1.0, baseOpacity * strength);
            root.Resources[key] = result;
        }

        private static Brush DarkenBrush(Brush source, double amount)
        {
            SolidColorBrush solid = source as SolidColorBrush;
            if (solid != null)
                return new SolidColorBrush(Darken(solid.Color, amount));

            GradientBrush gradient = source as GradientBrush;
            if (gradient != null)
            {
                GradientBrush clone;
                try
                {
                    clone = (GradientBrush)gradient.CloneCurrentValue();
                }
                catch
                {
                    return source;
                }

                foreach (GradientStop stop in clone.GradientStops)
                    stop.Color = Darken(stop.Color, amount);

                return clone;
            }

            return source;
        }

        private static LinearGradientBrush CreateAccentGradient(
            Color accent,
            double adjustment)
        {
            Color middle = adjustment >= 0
                ? Lighten(accent, adjustment)
                : Darken(accent, -adjustment);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(
                new GradientStop(Lighten(middle, 0.20), 0.0));
            brush.GradientStops.Add(new GradientStop(middle, 0.55));
            brush.GradientStops.Add(
                new GradientStop(Darken(middle, 0.22), 1.0));
            return brush;
        }

        private static LinearGradientBrush
            CreateTransparentAccentGradient(Color accent)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            Color shifted = ShiftHueLike(accent);

            brush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(0, accent.R, accent.G, accent.B), 0.0));
            brush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(190, accent.R, accent.G, accent.B), 0.30));
            brush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(190, shifted.R, shifted.G, shifted.B), 0.72));
            brush.GradientStops.Add(
                new GradientStop(
                    Color.FromArgb(0, shifted.R, shifted.G, shifted.B), 1.0));
            return brush;
        }

        private static Color ShiftHueLike(Color color)
        {
            return Color.FromArgb(color.A, color.B, color.R, color.G);
        }

        private static bool TryParseColor(string value, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            try
            {
                object converted = ColorConverter.ConvertFromString(value);
                if (converted is Color)
                {
                    color = (Color)converted;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static Color Lighten(Color color, double amount)
        {
            amount = Clamp(amount, 0, 1);
            return Color.FromArgb(
                color.A,
                (byte)(color.R + (255 - color.R) * amount),
                (byte)(color.G + (255 - color.G) * amount),
                (byte)(color.B + (255 - color.B) * amount));
        }

        private static Color Darken(Color color, double amount)
        {
            amount = Clamp(amount, 0, 1);
            return Color.FromArgb(
                color.A,
                (byte)(color.R * (1 - amount)),
                (byte)(color.G * (1 - amount)),
                (byte)(color.B * (1 - amount)));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
