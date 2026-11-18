using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace AppleNotepad
{
    internal static class Dialogs
    {
        internal static Window Create(Window owner, string title, double width, double height)
        {
            ResourceDictionary resources = owner == null ? Program.ReadWindow().Resources : owner.Resources;
            Window dialog = new Window { Title = title, Width = Math.Min(width + 24, SystemParameters.WorkArea.Width - 16), Height = Math.Min(height + 40, SystemParameters.WorkArea.Height - 16), WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, ShowInTaskbar = false, Resources = resources };
            if (owner != null && owner.IsVisible) dialog.Owner = owner;
            dialog.SetResourceReference(Window.ForegroundProperty, "Ink");
            Border frame = new Border { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1), Margin = new Thickness(12), Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .2 } };
            frame.SetResourceReference(Border.BackgroundProperty, "Chrome"); frame.SetResourceReference(Border.BorderBrushProperty, "Line");
            Grid layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) }); layout.RowDefinitions.Add(new RowDefinition());
            Grid titlebar = new Grid { Background = Brushes.Transparent }; titlebar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if (!(e.OriginalSource is Button)) dialog.DragMove(); };
            Button close = new Button { Content = "×", Width = 14, Height = 14, Margin = new Thickness(16, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Background = new SolidColorBrush(Color.FromRgb(255, 96, 88)), Style = (Style)resources["Traffic"], ToolTip = "Закрыть" };
            close.Click += delegate { dialog.Close(); };
            close.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; dialog.Close(); };
            titlebar.Children.Add(new TextBlock { Text = title, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, IsHitTestVisible = false }); titlebar.Children.Add(close);
            ContentControl host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            Grid.SetRow(host, 1); layout.Children.Add(titlebar); layout.Children.Add(host); frame.Child = layout; dialog.Content = frame; dialog.Tag = host;
            dialog.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { dialog.Close(); e.Handled = true; } };
            return dialog;
        }
        internal static StackPanel Body(Window dialog)
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(26, 14, 26, 24) };
            SetBody(dialog, new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); return panel;
        }
        internal static void SetBody(Window dialog, UIElement body) { ((ContentControl)dialog.Tag).Content = body; }
        internal static Button Button(string text, bool primary = false)
        {
            Button button = new Button { Content = text, MinWidth = 96, Padding = new Thickness(18, 8, 18, 8) };
            if (primary) { button.SetResourceReference(Control.BackgroundProperty, "Accent"); button.Foreground = Brushes.White; button.SetResourceReference(Control.BorderBrushProperty, "Accent"); }
            return button;
        }
        internal static MessageBoxResult Show(Window owner, string message, string title = "Apple Notepad", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            Window dialog = Create(owner, title, 460, Math.Min(540, 200 + message.Split('\n').Length * 17)); StackPanel panel = Body(dialog);
            panel.Children.Add(new ScrollViewer { MaxHeight = 330, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, LineHeight = 21, Margin = new Thickness(0, 4, 0, 24) } });
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(actions);
            MessageBoxResult result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No : buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel;
            Action<string, MessageBoxResult, bool> add = delegate(string label, MessageBoxResult value, bool primary)
            {
                Button button = Button(label, primary); button.Margin = new Thickness(8, 0, 0, 0); button.IsDefault = primary;
                button.Click += delegate { result = value; dialog.DialogResult = true; }; actions.Children.Add(button);
            };
            if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
            {
                bool save = message.StartsWith("Сохранить изменения", StringComparison.Ordinal);
                if (buttons == MessageBoxButton.YesNoCancel) add("Отмена", MessageBoxResult.Cancel, false);
                add(save ? "Не сохранять" : "Нет", MessageBoxResult.No, false); add(save ? "Сохранить" : "Да", MessageBoxResult.Yes, true);
            }
            else { if (buttons == MessageBoxButton.OKCancel) add("Отмена", MessageBoxResult.Cancel, false); add("Готово", MessageBoxResult.OK, true); }
            dialog.ShowDialog(); return result;
        }
        internal static MessageBoxResult Show(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        { return Show(Application.Current == null ? null : Application.Current.MainWindow, message, title, buttons, icon); }
        internal static string Prompt(Window owner, string title, string label, string initial)
        {
            Window dialog = Create(owner, title, 440, 215); StackPanel panel = Body(dialog);
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 10) });
            TextBox input = new TextBox { Text = initial, Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 18) }; panel.Children.Add(input);
            MenuStyling.Attach(dialog, input);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
            Button ok = Button("Готово", true); ok.IsDefault = true;
            actions.Children.Add(cancel); actions.Children.Add(ok); panel.Children.Add(actions);
            ok.Click += delegate { dialog.DialogResult = true; }; dialog.Loaded += delegate { input.Focus(); input.SelectAll(); };
            return dialog.ShowDialog() == true ? input.Text : null;
        }
    }
}
