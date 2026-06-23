using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BardMusicPlayer.Ui.Windows
{
    public sealed class AccentColorPickerDialog : Window
    {
        private readonly Slider _redSlider;
        private readonly Slider _greenSlider;
        private readonly Slider _blueSlider;
        private readonly TextBox _hexBox;
        private readonly Border _preview;
        private bool _updating;

        public string SelectedHex { get; private set; }

        public AccentColorPickerDialog(string initialHex)
        {
            Title = "Choose Accent Color";
            Width = 390;
            Height = 330;
            MinWidth = 390;
            MinHeight = 330;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(9, 12, 19));
            Foreground = Brushes.White;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _preview = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 95, 120)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(_preview, 0);
            root.Children.Add(_preview);

            _redSlider = AddChannelRow(root, 1, "Red");
            _greenSlider = AddChannelRow(root, 2, "Green");
            _blueSlider = AddChannelRow(root, 3, "Blue");

            var hexGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            hexGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            hexGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            hexGrid.Children.Add(new TextBlock
            {
                Text = "Hex",
                VerticalAlignment = VerticalAlignment.Center
            });

            _hexBox = new TextBox
            {
                Margin = new Thickness(6, 0, 0, 0),
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _hexBox.KeyDown += HexBox_KeyDown;
            _hexBox.LostFocus += HexBox_LostFocus;
            Grid.SetColumn(_hexBox, 1);
            hexGrid.Children.Add(_hexBox);
            Grid.SetRow(hexGrid, 4);
            root.Children.Add(hexGrid);

            var presets = new WrapPanel
            {
                Margin = new Thickness(0, 12, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            string[] values =
            {
                "#00F5FF", "#00B7FF", "#7A5CFF", "#C85CFF",
                "#FF4FC8", "#FF5D73", "#FFB84D", "#56F09A"
            };

            foreach (string preset in values)
            {
                var button = new Button
                {
                    Width = 34,
                    Height = 28,
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush(ParseColor(preset)),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(1),
                    Tag = preset,
                    ToolTip = preset
                };
                button.Click += PresetButton_Click;
                presets.Children.Add(button);
            }

            Grid.SetRow(presets, 5);
            root.Children.Add(presets);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var defaultButton = new Button
            {
                Content = "Use Theme Default",
                MinWidth = 125,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            defaultButton.Click += delegate
            {
                SelectedHex = string.Empty;
                DialogResult = true;
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 76,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };

            var okButton = new Button
            {
                Content = "OK",
                MinWidth = 76,
                Height = 32,
                IsDefault = true
            };
            okButton.Click += OkButton_Click;

            buttons.Children.Add(defaultButton);
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(okButton);
            Grid.SetRow(buttons, 6);
            root.Children.Add(buttons);

            Content = root;

            SetColor(ParseColor(
                string.IsNullOrWhiteSpace(initialHex)
                    ? "#00F5FF"
                    : initialHex));
        }

        private Slider AddChannelRow(Grid root, int row, string title)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

            grid.Children.Add(new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center
            });

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(6, 0, 8, 0)
            };
            slider.ValueChanged += ChannelSlider_ValueChanged;
            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);

            var value = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            value.SetBinding(
                TextBlock.TextProperty,
                new System.Windows.Data.Binding("Value")
                {
                    Source = slider,
                    StringFormat = "0"
                });
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            Grid.SetRow(grid, row);
            root.Children.Add(grid);
            return slider;
        }

        private void ChannelSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating || _redSlider == null)
                return;

            UpdateFromSliders();
        }

        private void UpdateFromSliders()
        {
            Color color = Color.FromRgb(
                (byte)Math.Round(_redSlider.Value),
                (byte)Math.Round(_greenSlider.Value),
                (byte)Math.Round(_blueSlider.Value));

            _preview.Background = new SolidColorBrush(color);
            _hexBox.Text = ToHex(color);
        }

        private void SetColor(Color color)
        {
            _updating = true;
            try
            {
                _redSlider.Value = color.R;
                _greenSlider.Value = color.G;
                _blueSlider.Value = color.B;
                _preview.Background = new SolidColorBrush(color);
                _hexBox.Text = ToHex(color);
            }
            finally
            {
                _updating = false;
            }
        }

        private void HexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            ApplyHexBox();
            e.Handled = true;
        }

        private void HexBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyHexBox();
        }

        private void ApplyHexBox()
        {
            try
            {
                SetColor(ParseColor(_hexBox.Text));
            }
            catch
            {
                _hexBox.Text = ToHex(
                    ((SolidColorBrush)_preview.Background).Color);
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
                SetColor(ParseColor(Convert.ToString(button.Tag)));
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyHexBox();
            SelectedHex = _hexBox.Text;
            DialogResult = true;
        }

        private static Color ParseColor(string value)
        {
            object parsed = ColorConverter.ConvertFromString(value);
            return parsed is Color ? (Color)parsed : Colors.Cyan;
        }

        private static string ToHex(Color color)
        {
            return string.Format(
                "#{0:X2}{1:X2}{2:X2}",
                color.R,
                color.G,
                color.B);
        }
    }
}
