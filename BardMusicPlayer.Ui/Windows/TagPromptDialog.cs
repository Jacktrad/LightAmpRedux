using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BardMusicPlayer.Ui.Windows
{
    /// <summary>
    /// Small dependency-free dialog for creating a reusable song tag.
    /// </summary>
    public sealed class TagPromptDialog : Window
    {
        private readonly TextBox _tagBox;
        private readonly TextBlock _validationText;

        public string TagName { get; private set; }

        public TagPromptDialog()
        {
            Title = "Add New Tag";
            Width = 390;
            Height = 205;
            MinWidth = 390;
            MinHeight = 205;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(9, 12, 19));
            Foreground = Brushes.White;

            var root = new Grid
            {
                Margin = new Thickness(18)
            };

            root.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            root.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            root.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Create a reusable tag",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };

            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var description = new TextBlock
            {
                Text = "Examples: Pop Punk, Duet, Holiday, Needs Review",
                Foreground = new SolidColorBrush(
                    Color.FromRgb(165, 177, 196)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            Grid.SetRow(description, 1);
            root.Children.Add(description);

            _tagBox = new TextBox
            {
                Height = 32,
                MaxLength = 40,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            _tagBox.KeyDown += TagBox_KeyDown;
            _tagBox.TextChanged += TagBox_TextChanged;

            Grid.SetRow(_tagBox, 2);
            root.Children.Add(_tagBox);

            _validationText = new TextBlock
            {
                Foreground = new SolidColorBrush(
                    Color.FromRgb(255, 93, 115)),
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };

            Grid.SetRow(_validationText, 3);
            root.Children.Add(_validationText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 78,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };

            var addButton = new Button
            {
                Content = "Add Tag",
                MinWidth = 88,
                Height = 32,
                IsDefault = true
            };

            addButton.Click += AddButton_Click;

            buttons.Children.Add(cancelButton);
            buttons.Children.Add(addButton);

            Grid.SetRow(buttons, 4);
            root.Children.Add(buttons);

            Content = root;

            Loaded += delegate
            {
                _tagBox.Focus();
                Keyboard.Focus(_tagBox);
            };
        }

        private void TagBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            TryAccept();
            e.Handled = true;
        }

        private void TagBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            _validationText.Visibility =
                Visibility.Collapsed;
        }

        private void AddButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            TryAccept();
        }

        private void TryAccept()
        {
            string value =
                (_tagBox.Text ?? string.Empty)
                    .Replace(";", string.Empty)
                    .Trim();

            if (value.Length == 0)
            {
                ShowValidation("Enter a tag name.");
                return;
            }

            if (value.Length > 40)
            {
                ShowValidation(
                    "Keep the tag to 40 characters or fewer.");
                return;
            }

            TagName = value;
            DialogResult = true;
        }

        private void ShowValidation(string message)
        {
            _validationText.Text = message;
            _validationText.Visibility = Visibility.Visible;
            _tagBox.Focus();
            _tagBox.SelectAll();
        }
    }
}
