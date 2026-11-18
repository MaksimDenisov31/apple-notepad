using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AppleNotepad
{
    internal static class InterfaceVerification
    {
        private static void Check(bool value, string message) { if (!value) throw new Exception("Interface check: " + message); }
        private static void Pump() { DispatcherFrame frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { frame.Continue = false; })); Dispatcher.PushFrame(frame); }
        private static void Show(Window dialog, Action verify)
        {
            Exception failure = null;
            dialog.Loaded += delegate { dialog.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { try { verify(); } catch (Exception error) { failure = error; } finally { if (dialog.IsVisible) dialog.Close(); } })); };
            dialog.ShowDialog(); if (failure != null) throw failure;
        }
        private static T Child<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { DependencyObject child = VisualTreeHelper.GetChild(parent, i); T result = child as T ?? Child<T>(child); if (result != null) return result; } return null;
        }
        private static Button FindButton(DependencyObject parent, string caption)
        {
            Button button = parent as Button; if (button != null && Convert.ToString(button.Content) == caption) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { Button found = FindButton(VisualTreeHelper.GetChild(parent, i), caption); if (found != null) return found; } return null;
        }
        private static void ModalAction(Window owner, string title, Action<Window> inspect, Action invoke)
        {
            bool handled = false; Exception failure = null;
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            timer.Tick += delegate
            {
                foreach (Window child in owner.OwnedWindows)
                    if (child.IsVisible && child.Title == title)
                    {
                        timer.Stop(); handled = true;
                        try { inspect(child); } catch (Exception error) { failure = error; child.Close(); }
                        break;
                    }
            };
            timer.Start(); try { invoke(); } finally { timer.Stop(); }
            if (failure != null) throw failure; Check(handled, "expected modal dialog: " + title);
        }
        internal static void Run(EditorWindow app)
        {
            TextBox editor = app.Current.Editor; string original = editor.Text; bool wrap = app.Settings.Wrap;
            app.Settings.Wrap = false; app.ApplyPreferences();
            editor.Text = String.Join("\n", Enumerable.Range(1, 180).Select(delegate(int line) { return "Идеи для нового проекта. Сохраняем важные мысли и продолжаем работу — " + String.Join(" ", Enumerable.Repeat("всё начинается с простого текста", 5)); }));
            app.Window.UpdateLayout(); Pump();
            ScrollViewer host = Child<ScrollViewer>(editor); Check(host != null, "editor scroll host");
            ScrollBar vertical = (ScrollBar)host.Template.FindName("PART_VerticalScrollBar", host), horizontal = (ScrollBar)host.Template.FindName("PART_HorizontalScrollBar", host);
            Check(vertical.IsVisible && horizontal.IsVisible, "both styled scrollbars visible");
            Track vt = (Track)vertical.Template.FindName("PART_Track", vertical); vt.Thumb.RaiseEvent(new DragDeltaEventArgs(0, 60)); Pump(); Check(editor.VerticalOffset > 0, "vertical thumb scrolling works");
            Track ht = (Track)horizontal.Template.FindName("PART_Track", horizontal); ht.Thumb.RaiseEvent(new DragDeltaEventArgs(60, 0)); Pump(); Check(editor.HorizontalOffset > 0, "horizontal thumb scrolling works");
            editor.ScrollToVerticalOffset(0); editor.ScrollToHorizontalOffset(0); Pump();
            foreach (string theme in new string[] { "light", "dark" }) { app.Settings.Theme = theme; app.ApplyPreferences(); app.Window.UpdateLayout(); Capture(app.Window, "controls-" + theme + ".png"); }
            editor.Text = original; app.Settings.Wrap = wrap; app.Settings.Theme = "light"; app.ApplyPreferences();
            ComboBox encoding = app.Control<ComboBox>("EncodingChoice"); string oldEncoding = app.Current.File.EncodingName;
            encoding.IsDropDownOpen = true; Pump();
            Popup comboPopup = (Popup)encoding.Template.FindName("PART_Popup", encoding); Check(comboPopup.IsOpen, "styled encoding list opens"); Capture((FrameworkElement)comboPopup.Child, "encoding-menu.png");
            foreach (ComboBoxItem item in encoding.Items) if ((string)item.Tag == "utf16") encoding.SelectedItem = item;
            encoding.IsDropDownOpen = false; Check(app.Current.File.EncodingName == "utf16", "encoding selection reaches document");
            ComboBox newline = app.Control<ComboBox>("NewlineChoice"); foreach (ComboBoxItem item in newline.Items) if ((string)item.Tag == "LF") newline.SelectedItem = item;
            Check(app.Save(app.Current, false), "save with selected format"); TextFile saved = TextFile.Open(app.Current.File.Path); Check(saved.EncodingName == "utf16" && saved.Newline == "LF" && saved.Text == TextFile.Normalize(original), "styled choices affect actual bytes");
            foreach (ComboBoxItem item in encoding.Items) if ((string)item.Tag == oldEncoding) encoding.SelectedItem = item;
            foreach (ComboBoxItem item in newline.Items) if ((string)item.Tag == "CRLF") newline.SelectedItem = item;
            app.Save(app.Current, false);
            FontPicker font = new FontPicker(app.Window, app.Settings); string originalFamily = app.Settings.Font; double originalSize = app.Settings.FontSize;
            Show(font.Window, delegate
            {
                Check(font.Window.WindowStyle == WindowStyle.None && font.Window.AllowsTransparency, "font dialog has custom chrome");
                font.Query.Text = "Consolas"; Check(font.Families.Items.Count > 0, "font search filters families"); font.Families.SelectedIndex = 0;
                font.SizeField.Text = "0"; Check(!font.Apply.IsEnabled, "invalid font size rejected"); font.SizeField.Text = "NaN"; Check(!font.Apply.IsEnabled, "non-finite font size rejected"); font.SizeField.Text = "18"; font.Face.SelectedIndex = 3;
                Check(font.Preview.FontSize == 24 && font.Preview.FontWeight == FontWeights.Bold && font.Preview.FontStyle == FontStyles.Italic, "font preview updates");
                Check(app.Settings.Font == originalFamily && app.Settings.FontSize == originalSize, "font preview does not modify settings");
                Capture(font.Window, "font-light.png"); app.Settings.Theme = "dark"; app.ApplyPreferences(); Pump(); Check(((SolidColorBrush)Child<TextBlock>(font.Face).Foreground).Color == ((SolidColorBrush)font.Face.Foreground).Color, "selected font face has readable dark-theme text"); Capture(font.Window, "font-dark.png");
                font.Apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Check(font.Result != null && font.Result.Family == "Consolas" && font.Result.Size == 24 && font.Result.Bold && font.Result.Italic, "font dialog returns confirmed choice");
            FontPicker cancelled = new FontPicker(app.Window, app.Settings); Show(cancelled.Window, delegate { cancelled.SizeField.Text = "20"; cancelled.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }); Check(cancelled.Result == null, "font cancellation returns no change");
            app.Settings.Theme = "light"; app.ApplyPreferences();
            ModalAction(app.Window, "Перейти к строке", delegate(Window dialog) { TextBox input = Child<TextBox>(dialog); input.Text = "3"; Capture(dialog, "prompt.png"); FindButton(dialog, "Готово").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, delegate { Check(Dialogs.Prompt(app.Window, "Перейти к строке", "Номер строки", "1") == "3", "styled prompt returns input"); });
            ModalAction(app.Window, "Закрытие документа", delegate(Window dialog) { Capture(dialog, "save-confirmation.png"); FindButton(dialog, "Отмена").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, delegate { Check(Dialogs.Show(app.Window, "Сохранить изменения в «Документ.txt»?", "Закрытие документа", MessageBoxButton.YesNoCancel) == MessageBoxResult.Cancel, "styled save cancellation preserved"); });
            ModalAction(app.Window, "Настройки", delegate(Window dialog) { Capture(dialog, "settings.png"); dialog.Close(); }, delegate { app.Control<Button>("SettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
            ModalAction(app.Window, "Параметры страницы", delegate(Window dialog) { Capture(dialog, "page-setup.png"); dialog.Close(); }, delegate { app.Control<MenuItem>("PageSetup").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); });
            string root = Path.Combine(Preferences.DataDirectory, "picker-fixtures"); Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root, "Папка"));
            string one = Path.Combine(root, "Первый.txt"), two = Path.Combine(root, "Второй.txt"); File.WriteAllText(one, "one"); File.WriteAllText(two, "two"); File.WriteAllText(Path.Combine(root, "image.png"), "fixture, not an actual image");
            FilePicker open = new FilePicker(app.Window, false, root, "");
            Show(open.Window, delegate
            {
                Check(!open.Entries.Items.Cast<FileEntry>().Any(delegate(FileEntry entry) { return entry.Name == "image.png"; }), "text filter hides unrelated files");
                open.Filter.SelectedIndex = open.Filter.Items.Count - 1; Check(open.Entries.Items.Cast<FileEntry>().Any(delegate(FileEntry entry) { return entry.Name == "image.png"; }), "all-files filter works");
                open.Navigate(Path.Combine(root, "Папка")); open.Back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(open.Folder == root, "folder back navigation");
                foreach (FileEntry entry in open.Entries.Items) if (entry.Path == one || entry.Path == two) open.Entries.SelectedItems.Add(entry);
                Capture(open.Window, "open-file.png"); open.Choose();
            });
            Check(open.Result != null && open.Result.Length == 2 && open.Result.Contains(one) && open.Result.Contains(two), "multiple file selection returned");
            FilePicker save = new FilePicker(app.Window, true, root, "Новый документ"); Show(save.Window, delegate { save.NameField.Text = "Новая заметка"; Capture(save.Window, "save-file.png"); save.Choose(); });
            Check(save.Result != null && save.Result[0] == Path.Combine(root, "Новая заметка.txt") && !File.Exists(save.Result[0]), "save picker adds extension without modifying file");
            new TextFile { Text = "Кириллица\n", EncodingName = "utf8", Newline = "LF" }.Save(save.Result[0]); Check(TextFile.Open(save.Result[0]).Text == "Кириллица\n", "save result supports actual Unicode write");
            FilePicker conflict = new FilePicker(app.Window, true, root, "Первый.txt");
            Show(conflict.Window, delegate
            {
                ModalAction(conflict.Window, "Сохранение файла", delegate(Window dialog) { FindButton(dialog, "Нет").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, conflict.Choose);
                Check(conflict.Result == null && File.ReadAllText(one) == "one", "overwrite rejection preserves existing file"); conflict.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            using (PrinterPicker printer = new PrinterPicker(app.Window, "Документ.txt", false, delegate(Window owner) { return false; }))
            {
                Show(printer.Window, delegate
                {
                    printer.Printers.SelectedIndex = -1; printer.Copies.Text = "0"; printer.Choose(); Check(printer.Result == null, "invalid copy count cannot submit print job");
                    printer.Copies.Text = "1"; printer.Range.Text = "5-2"; printer.Choose(); Check(printer.Result == null, "invalid page range rejected");
                    Capture(printer.Window, "print.png"); printer.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
            }
            int first, last; Check(PrinterPicker.TryRange("2-5", out first, out last) && first == 1 && last == 4, "page range parsed"); Check(!PrinterPicker.TryRange("0", out first, out last), "zero page rejected");
            app.Settings.Theme = "light"; app.ApplyPreferences();
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "interface-report.txt"), "PASS: vertical/horizontal thumb scrolling; encoding/newline controls affect saved bytes; custom font search/style/size/preview/apply/cancel; styled prompts/save cancellation/settings/page setup; file navigation/filter/multi-select/save extension/Unicode/overwrite refusal; print validation without submitting a job; light/dark screenshots. User documents and clipboard were not used.\r\n");
        }
        private static void Capture(FrameworkElement element, string name)
        {
            element.UpdateLayout(); Pump(); RenderTargetBitmap bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
            PngBitmapEncoder output = new PngBitmapEncoder(); output.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", name))) output.Save(stream);
        }
    }
}
