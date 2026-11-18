using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace AppleNotepad
{
    internal sealed class FileEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Modified { get; set; }
        public string Size { get; set; }
        public string Glyph { get; set; }
        public bool Directory { get; set; }
    }
    internal sealed class FilePicker
    {
        internal readonly Window Window;
        internal readonly TextBox Address, NameField;
        internal readonly ListBox Entries;
        internal readonly ComboBox Filter;
        internal readonly CheckBox Hidden;
        internal readonly Button Accept, Cancel, Up, Back;
        internal string[] Result;
        internal string Folder;
        private readonly bool save;
        private readonly TextBlock note;
        private readonly Stack<string> history = new Stack<string>();
        private bool updating, typedName;
        internal FilePicker(Window owner, bool save, string directory, string name)
        {
            this.save = save; Window = Dialogs.Create(owner, save ? "Сохранить документ" : "Открыть документы", 830, 570);
            Grid body = new Grid { Margin = new Thickness(22, 8, 22, 22) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition()); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DockPanel location = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            Back = Dialogs.Button("‹"); Back.MinWidth = 35; Back.Padding = new Thickness(8, 5, 8, 5); Back.ToolTip = "Назад"; Back.Margin = new Thickness(0, 0, 6, 0);
            Up = Dialogs.Button("↑"); Up.MinWidth = 35; Up.Padding = new Thickness(8, 5, 8, 5); Up.ToolTip = "Родительская папка — Alt+↑"; Up.Margin = new Thickness(0, 0, 9, 0);
            Address = new TextBox { ToolTip = "Путь к папке — Ctrl+L" }; MenuStyling.Attach(Window, Address);
            location.Children.Add(Back); location.Children.Add(Up); location.Children.Add(Address); body.Children.Add(location);
            Grid browser = new Grid(); browser.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(162) }); browser.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(browser, 1); body.Children.Add(browser);
            StackPanel places = new StackPanel { Margin = new Thickness(0, 4, 14, 0) }; places.Children.Add(new TextBlock { Text = "ИЗБРАННОЕ", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(10, 0, 0, 12) }); browser.Children.Add(places);
            AddPlace(places, "Рабочий стол", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); AddPlace(places, "Документы", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            string downloads = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); if (System.IO.Directory.Exists(downloads)) AddPlace(places, "Загрузки", downloads);
            foreach (DriveInfo drive in DriveInfo.GetDrives()) if (drive.IsReady) AddPlace(places, "Диск " + drive.Name.TrimEnd('\\'), drive.RootDirectory.FullName);
            Entries = new ListBox { SelectionMode = save ? SelectionMode.Single : SelectionMode.Extended };
            Entries.ItemTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='24'/><ColumnDefinition Width='*'/><ColumnDefinition Width='118'/><ColumnDefinition Width='58'/></Grid.ColumnDefinitions><TextBlock Text='{Binding Glyph}' FontSize='16' VerticalAlignment='Center'/><TextBlock Grid.Column='1' Text='{Binding Name}' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/><TextBlock Grid.Column='2' Text='{Binding Modified}' FontSize='11' Opacity='0.65' VerticalAlignment='Center'/><TextBlock Grid.Column='3' Text='{Binding Size}' FontSize='11' Opacity='0.65' HorizontalAlignment='Right' VerticalAlignment='Center'/></Grid></DataTemplate>");
            Grid.SetColumn(Entries, 1); browser.Children.Add(Entries);
            StackPanel footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) }; Grid.SetRow(footer, 2); body.Children.Add(footer);
            DockPanel filename = new DockPanel(); filename.Children.Add(new TextBlock { Text = "Имя файла", Width = 100, VerticalAlignment = VerticalAlignment.Center }); NameField = new TextBox { Text = name ?? "" }; MenuStyling.Attach(Window, NameField); filename.Children.Add(NameField); footer.Children.Add(filename);
            DockPanel options = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
            Filter = new ComboBox { Width = 225 }; Filter.Items.Add(save ? "Текстовый документ (*.txt)" : "Текстовые файлы"); if (save) Filter.Items.Add("Markdown (*.md)"); Filter.Items.Add("Все файлы");
            string extension = System.IO.Path.GetExtension(name ?? "").ToLowerInvariant(); Filter.SelectedIndex = save && extension == ".md" ? 1 : save && extension != "" && extension != ".txt" ? 2 : 0;
            Hidden = new CheckBox { Content = "Скрытые файлы", Margin = new Thickness(14, 0, 0, 0) }; options.Children.Add(Filter); options.Children.Add(Hidden); footer.Children.Add(options);
            DockPanel actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false }; StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(right, Dock.Right); actions.Children.Add(right);
            Button newFolder = Dialogs.Button("Новая папка"); newFolder.Click += delegate { string input = Dialogs.Prompt(Window, "Новая папка", "Название папки", "Новая папка"); if (input == null) return; try { if (String.IsNullOrWhiteSpace(input) || input.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || input == "." || input == "..") throw new ArgumentException("Укажите корректное имя папки."); System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Folder, input)); Refresh(); } catch (Exception error) { note.Text = error.Message; } }; actions.Children.Add(newFolder);
            Cancel = Dialogs.Button("Отмена"); Cancel.Margin = new Thickness(0, 0, 8, 0); Cancel.IsCancel = true; Cancel.Click += delegate { Window.Close(); }; Accept = Dialogs.Button(save ? "Сохранить" : "Открыть", true); Accept.IsDefault = true;
            right.Children.Add(Cancel); right.Children.Add(Accept); footer.Children.Add(actions); note = new TextBlock { FontSize = 11, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) }; footer.Children.Add(note);
            Dialogs.SetBody(Window, body);
            Address.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Navigate(Address.Text); e.Handled = true; } };
            NameField.TextChanged += delegate { if (!updating) typedName = true; };
            Entries.SelectionChanged += delegate
            {
                FileEntry selected = Entries.SelectedItem as FileEntry;
                if (selected != null && !selected.Directory) { updating = true; NameField.Text = selected.Name; updating = false; typedName = false; }
            };
            Entries.MouseDoubleClick += delegate { FileEntry selected = Entries.SelectedItem as FileEntry; if (selected == null) return; if (selected.Directory) Navigate(selected.Path); else Choose(); };
            Entries.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Choose(); e.Handled = true; } };
            Accept.Click += delegate { Choose(); }; Up.Click += delegate { if (Folder != null) { DirectoryInfo parent = System.IO.Directory.GetParent(Folder); if (parent != null) Navigate(parent.FullName); } };
            Back.Click += delegate { if (history.Count > 0) Navigate(history.Pop(), false); }; Filter.SelectionChanged += delegate { if (Folder != null) Refresh(); }; Hidden.Checked += delegate { Refresh(); }; Hidden.Unchecked += delegate { Refresh(); };
            Window.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L) { Address.Focus(); Address.SelectAll(); e.Handled = true; } else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Up) { Up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; } };
            if (String.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory)) directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!System.IO.Directory.Exists(directory)) directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Navigate(directory, false); Window.Loaded += delegate { NameField.Focus(); if (save) NameField.SelectAll(); };
        }
        private void AddPlace(Panel panel, string label, string path)
        {
            Button button = Dialogs.Button(label); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Margin = new Thickness(0, 1, 0, 1); button.Padding = new Thickness(9, 7, 9, 7); button.ToolTip = path; button.Click += delegate { Navigate(path); }; panel.Children.Add(button);
        }
        internal void Navigate(string path, bool remember = true)
        {
            try
            {
                string next = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)); if (!System.IO.Directory.Exists(next)) throw new DirectoryNotFoundException("Папка не найдена.");
                FileSystemInfo[] entries = new DirectoryInfo(next).GetFileSystemInfos();
                if (remember && Folder != null && !String.Equals(Folder, next, StringComparison.OrdinalIgnoreCase)) history.Push(Folder);
                Folder = next; Address.Text = next; Fill(entries); Back.IsEnabled = history.Count > 0; Up.IsEnabled = System.IO.Directory.GetParent(Folder) != null;
            }
            catch (Exception error) { note.Text = error.Message; }
        }
        private void Refresh() { if (Folder != null) Navigate(Folder, false); }
        private void Fill(IEnumerable<FileSystemInfo> entries)
        {
            List<FileEntry> values = new List<FileEntry>();
            foreach (FileSystemInfo entry in entries)
            {
                try
                {
                    if (Hidden.IsChecked != true && (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    bool directory = (entry.Attributes & FileAttributes.Directory) != 0;
                    if (!directory && !save && Filter.SelectedIndex == 0 && !KnownText(entry.Extension)) continue;
                    if (!directory && save && Filter.SelectedIndex < 2 && !String.Equals(entry.Extension, Filter.SelectedIndex == 1 ? ".md" : ".txt", StringComparison.OrdinalIgnoreCase)) continue;
                    values.Add(new FileEntry { Name = entry.Name, Path = entry.FullName, Directory = directory, Glyph = directory ? "▸" : "▱", Modified = entry.LastWriteTime.ToString("dd.MM.yyyy HH:mm"), Size = directory ? "" : FormatSize(((FileInfo)entry).Length) });
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            Entries.ItemsSource = values.OrderByDescending(delegate(FileEntry entry) { return entry.Directory; }).ThenBy(delegate(FileEntry entry) { return entry.Name; }, StringComparer.CurrentCultureIgnoreCase).ToList();
            note.Text = values.Count + " элементов" + (!save ? " · Ctrl / Shift — выбрать несколько файлов" : "");
        }
        private static bool KnownText(string extension) { return new string[] { ".txt", ".log", ".md", ".csv", ".json", ".xml", ".ini", ".cs", ".py", ".js", ".html", ".css", ".bat", ".ps1", ".yaml", ".yml" }.Contains(extension.ToLowerInvariant()); }
        private static string FormatSize(long value) { return value < 1024 ? value + " Б" : value < 1024 * 1024 ? (value / 1024.0).ToString("0.#") + " КБ" : (value / 1048576.0).ToString("0.#") + " МБ"; }
        internal void Choose()
        {
            try
            {
                FileEntry selected = Entries.SelectedItem as FileEntry;
                if (!typedName && selected != null && selected.Directory) { Navigate(selected.Path); return; }
                if (!save && !typedName && Entries.SelectedItems.Count > 0)
                {
                    string[] paths = Entries.SelectedItems.Cast<FileEntry>().Where(delegate(FileEntry entry) { return !entry.Directory; }).Select(delegate(FileEntry entry) { return entry.Path; }).ToArray();
                    if (paths.Length > 0) { Result = paths; Window.DialogResult = true; return; }
                }
                if (String.IsNullOrWhiteSpace(NameField.Text)) { note.Text = "Укажите имя файла."; return; }
                string path = System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(NameField.Text) ? NameField.Text : System.IO.Path.Combine(Folder, NameField.Text));
                if (System.IO.Directory.Exists(path)) { Navigate(path); return; }
                if (System.IO.Path.GetFileName(path).IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Недопустимое имя файла.");
                if (save)
                {
                    if (System.IO.Path.GetExtension(path).Length == 0 && !System.IO.Path.GetFileName(path).StartsWith(".") && Filter.SelectedIndex < 2) path += Filter.SelectedIndex == 1 ? ".md" : ".txt";
                    if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path))) throw new DirectoryNotFoundException("Сначала выберите или создайте папку.");
                    if (File.Exists(path) && Dialogs.Show(Window, "Файл уже существует. Заменить его?\n" + System.IO.Path.GetFileName(path), "Сохранение файла", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                }
                else if (!File.Exists(path)) throw new FileNotFoundException("Файл не найден.");
                Result = new string[] { path }; Window.DialogResult = true;
            }
            catch (Exception error) { note.Text = error.Message; }
        }
        internal static string[] Open(Window owner, string directory = null) { FilePicker dialog = new FilePicker(owner, false, directory, ""); return dialog.Window.ShowDialog() == true ? dialog.Result : null; }
        internal static string Save(Window owner, string path)
        {
            FilePicker dialog = new FilePicker(owner, true, String.IsNullOrEmpty(path) ? null : System.IO.Path.GetDirectoryName(path), String.IsNullOrEmpty(path) ? "Без названия.txt" : System.IO.Path.GetFileName(path));
            return dialog.Window.ShowDialog() == true ? dialog.Result[0] : null;
        }
    }
}
