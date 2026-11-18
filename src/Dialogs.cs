using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AppleNotepad
{
    internal static class Dialogs
    {
        internal static Window Create(Window owner, string title, double width, double height)
        {
            Window dialog = new Window { Owner = owner, Title = title, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, ShowInTaskbar = false, Resources = owner.Resources };
            dialog.SetResourceReference(Window.BackgroundProperty, "Chrome"); dialog.SetResourceReference(Window.ForegroundProperty, "Ink"); return dialog;
        }
        internal static StackPanel Body(Window dialog)
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(26) }; dialog.Content = panel; return panel;
        }
        internal static string Prompt(Window owner, string title, string label, string initial)
        {
            Window dialog = Create(owner, title, 440, 215); StackPanel panel = Body(dialog);
            panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 10) });
            TextBox input = new TextBox { Text = initial, Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 18) }; panel.Children.Add(input);
            MenuStyling.Attach(dialog, input);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
            Button ok = new Button { Content = "Готово", IsDefault = true, MinWidth = 90, Padding = new Thickness(12, 6, 12, 6) };
            actions.Children.Add(cancel); actions.Children.Add(ok); panel.Children.Add(actions);
            ok.Click += delegate { dialog.DialogResult = true; }; dialog.Loaded += delegate { input.Focus(); input.SelectAll(); };
            return dialog.ShowDialog() == true ? input.Text : null;
        }
    }
}
