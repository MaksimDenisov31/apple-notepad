using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AppleNotepad
{
    internal sealed class FontSelection { internal string Family; internal double Size; internal bool Bold, Italic; }
    internal sealed class FontPicker
    {
        internal readonly Window Window;
        internal readonly TextBox Query, SizeField;
        internal readonly ListBox Families;
        internal readonly ComboBox Face;
        internal readonly TextBlock Preview;
        internal readonly Button Apply, Cancel;
        internal FontSelection Result;
        private readonly List<FontFamily> fonts;
        internal FontPicker(Window owner, Preferences settings)
        {
            Window = Dialogs.Create(owner, "Шрифт", 650, 510);
            fonts = Fonts.SystemFontFamilies.OrderBy(delegate(FontFamily font) { return font.Source; }, StringComparer.CurrentCultureIgnoreCase).ToList();
            Grid body = new Grid { Margin = new Thickness(24, 12, 24, 24) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(158) }); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid fields = new Grid { Margin = new Thickness(0, 0, 0, 12) }; fields.ColumnDefinitions.Add(new ColumnDefinition()); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            Query = new TextBox { ToolTip = "Найти шрифт", Margin = new Thickness(0, 0, 12, 0) }; MenuStyling.Attach(Window, Query);
            Face = new ComboBox { Margin = new Thickness(0, 0, 12, 0) }; foreach (string label in new string[] { "Обычный", "Полужирный", "Курсив", "Полужирный курсив" }) Face.Items.Add(label);
            Face.SelectedIndex = settings.Bold ? (settings.Italic ? 3 : 1) : (settings.Italic ? 2 : 0);
            SizeField = new TextBox { Text = (settings.FontSize * 72 / 96).ToString("0.##", CultureInfo.CurrentCulture), ToolTip = "Размер в пунктах: 6–72" }; MenuStyling.Attach(Window, SizeField);
            fields.Children.Add(Query); fields.Children.Add(Face); Grid.SetColumn(Face, 1); fields.Children.Add(SizeField); Grid.SetColumn(SizeField, 2); body.Children.Add(fields);
            Families = new ListBox { ItemsSource = fonts, DisplayMemberPath = "Source" }; Grid.SetRow(Families, 1); body.Children.Add(Families);
            FontFamily selected = fonts.Find(delegate(FontFamily family) { return String.Equals(family.Source, settings.Font, StringComparison.OrdinalIgnoreCase); }); Families.SelectedItem = selected ?? fonts.FirstOrDefault();
            Border previewCard = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(18), Margin = new Thickness(0, 14, 0, 12) };
            previewCard.SetResourceReference(Border.BackgroundProperty, "Paper"); previewCard.SetResourceReference(Border.BorderBrushProperty, "Line");
            Preview = new TextBlock { Text = "Съешь ещё этих мягких булок.\nThe quick brown fox jumps.\n0123456789", TextWrapping = TextWrapping.Wrap }; previewCard.Child = new ScrollViewer { Content = Preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(previewCard, 2); body.Children.Add(previewCard);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Cancel = Dialogs.Button("Отмена"); Cancel.Margin = new Thickness(0, 0, 10, 0); Cancel.IsCancel = true; Cancel.Click += delegate { Window.Close(); };
            Apply = Dialogs.Button("Применить", true); Apply.IsDefault = true; actions.Children.Add(Cancel); actions.Children.Add(Apply); Grid.SetRow(actions, 3); body.Children.Add(actions);
            Dialogs.SetBody(Window, body);
            Query.TextChanged += delegate { FontFamily old = Families.SelectedItem as FontFamily; Families.ItemsSource = fonts.Where(delegate(FontFamily family) { return family.Source.IndexOf(Query.Text, StringComparison.CurrentCultureIgnoreCase) >= 0; }).ToList(); Families.SelectedItem = old; if (Families.SelectedItem == null && Families.Items.Count > 0) Families.SelectedIndex = 0; };
            Families.SelectionChanged += delegate { UpdatePreview(); }; Face.SelectionChanged += delegate { UpdatePreview(); }; SizeField.TextChanged += delegate { UpdatePreview(); };
            Apply.Click += delegate { FontSelection value = Selection(); if (value == null) return; Result = value; Window.DialogResult = true; };
            Window.Loaded += delegate { if (Families.SelectedItem != null) Families.ScrollIntoView(Families.SelectedItem); Query.Focus(); UpdatePreview(); };
            UpdatePreview();
        }
        private FontSelection Selection()
        {
            double points; FontFamily family = Families.SelectedItem as FontFamily;
            if (family == null || !(Double.TryParse(SizeField.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out points) || Double.TryParse(SizeField.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out points)) || Double.IsNaN(points) || Double.IsInfinity(points) || points < 6 || points > 72) return null;
            return new FontSelection { Family = family.Source, Size = points * 96 / 72, Bold = Face.SelectedIndex == 1 || Face.SelectedIndex == 3, Italic = Face.SelectedIndex >= 2 };
        }
        private void UpdatePreview()
        {
            FontSelection value = Selection(); Apply.IsEnabled = value != null;
            if (value == null) return;
            Preview.FontFamily = new FontFamily(value.Family); Preview.FontSize = value.Size; Preview.FontWeight = value.Bold ? FontWeights.Bold : FontWeights.Normal; Preview.FontStyle = value.Italic ? FontStyles.Italic : FontStyles.Normal;
        }
        internal static FontSelection Pick(Window owner, Preferences settings)
        { FontPicker picker = new FontPicker(owner, settings); return picker.Window.ShowDialog() == true ? picker.Result : null; }
    }
}
