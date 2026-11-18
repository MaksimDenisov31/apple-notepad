using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace AppleNotepad
{
    internal sealed class EditorTab
    {
        internal TextFile File;
        internal TextBox Editor;
        internal TabItem Tab;
        internal TextBlock Title;
        internal Canvas Numbers;
        internal Grid Host;
        internal FlowDocumentScrollViewer Preview;
        internal long IgnoredDiskTicks = -1, IgnoredDiskLength = -1;
    }
    internal sealed class EditorWindow
    {
        internal readonly Window Window = Program.ReadWindow();
        internal readonly Preferences Settings = Preferences.Load();
        internal readonly List<EditorTab> Tabs = new List<EditorTab>();
        internal readonly bool TestMode;
        private readonly SessionVault vault = new SessionVault();
        private readonly DispatcherTimer recovery = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private readonly Printing printing = new Printing();
        private bool syncing, recoveryDirty, checkingDisk, fullScreen;
        private WindowState previousState;
        internal T Control<T>(string name) where T : class { return Window.FindName(name) as T; }
        internal EditorTab Current { get { return Control<TabControl>("Documents").SelectedItem == null ? null : (EditorTab)((TabItem)Control<TabControl>("Documents").SelectedItem).Tag; } }
        internal EditorWindow(bool test, bool newWindow)
        {
            TestMode = test;
            WindowChrome.SetWindowChrome(Window, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(10) });
            Window.Width = Math.Max(720, Math.Min(Settings.Width, SystemParameters.WorkArea.Width));
            Window.Height = Math.Max(460, Math.Min(Settings.Height, SystemParameters.WorkArea.Height));
            Control<Grid>("TitleDrag").MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.OriginalSource is Button || AncestorButton(e.OriginalSource as DependencyObject)) return;
                if (e.ClickCount == 2) ToggleMaximize(); else Window.DragMove();
            };
            Bind("CloseWindow", Window.Close); Bind("MinimizeWindow", delegate { Window.WindowState = WindowState.Minimized; }); Bind("MaximizeWindow", ToggleMaximize);
            Bind("FileNew", delegate { NewTab(new TextFile()); }); Bind("AddTab", delegate { NewTab(new TextFile()); });
            Bind("FileWindow", delegate { Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--new-window") { UseShellExecute = true }); });
            Bind("FileOpen", PickOpen); Bind("FileSave", delegate { Save(Current, false); }); Bind("SaveButton", delegate { Save(Current, false); }); Bind("FileSaveAs", delegate { Save(Current, true); });
            Bind("FileSaveAll", delegate { foreach (EditorTab tab in Tabs) if (!Save(tab, false)) break; });
            Bind("CloseTab", delegate { CloseTab(Current); }); Bind("ExitApp", Window.Close);
            Bind("Undo", delegate { if (Current != null) Current.Editor.Undo(); }); Bind("Redo", delegate { if (Current != null) Current.Editor.Redo(); });
            Bind("Cut", delegate { if (Current != null) Current.Editor.Cut(); }); Bind("Copy", delegate { if (Current != null) Current.Editor.Copy(); }); Bind("Paste", delegate { if (Current != null) Current.Editor.Paste(); });
            Bind("Delete", delegate { if (Current != null && Current.Editor.SelectionLength > 0) Current.Editor.SelectedText = ""; });
            Bind("SelectAll", delegate { if (Current != null) { Current.Editor.Focus(); Current.Editor.SelectAll(); } });
            Bind("Find", delegate { ShowFind(false); }); Bind("SearchToggle", delegate { ShowFind(false); }); Bind("Replace", delegate { ShowFind(true); });
            Bind("FindClose", HideFind); Bind("FindNext", delegate { Find(false); }); Bind("FindPrevious", delegate { Find(true); });
            Bind("FindNextButton", delegate { Find(false); }); Bind("FindPrevButton", delegate { Find(true); });
            Bind("ReplaceButton", ReplaceOne); Bind("ReplaceAllButton", ReplaceAll); Bind("GoTo", GoTo); Bind("DateTime", InsertDate);
            Bind("WordWrap", delegate { Settings.Wrap = !Settings.Wrap; ApplyPreferences(); });
            Bind("SpellcheckMenu", delegate { Settings.SpellCheck = !Settings.SpellCheck; ApplyPreferences(); });
            Bind("LineNumbers", delegate { Settings.LineNumbers = !Settings.LineNumbers; ApplyPreferences(); });
            Bind("StatusBarMenu", delegate { Settings.StatusBar = !Settings.StatusBar; ApplyPreferences(); });
            Bind("FontMenu", PickFont); Bind("ZoomIn", delegate { Zoom(10); }); Bind("ZoomOut", delegate { Zoom(-10); }); Bind("ZoomReset", delegate { Settings.Zoom = 100; ApplyPreferences(); });
            Bind("ZoomPlus", delegate { Zoom(10); }); Bind("ZoomMinus", delegate { Zoom(-10); }); Bind("Fullscreen", ToggleFullScreen);
            Bind("LightTheme", delegate { Settings.Theme = "light"; ApplyPreferences(); }); Bind("DarkTheme", delegate { Settings.Theme = "dark"; ApplyPreferences(); });
            Bind("SettingsButton", ShowSettings); Bind("PageSetup", delegate { printing.Setup(Window); });
            Bind("PrintFile", delegate { if (Current != null) printing.Print(Window, Current.File, Settings); });
            Bind("MarkdownPreview", TogglePreview); Bind("BoldMarkdown", delegate { WrapSelection("**", "**"); }); Bind("ItalicMarkdown", delegate { WrapSelection("*", "*"); });
            Bind("HeadingMarkdown", delegate { InsertLinePrefix("## "); }); Bind("ListMarkdown", delegate { InsertLinePrefix("- "); });
            Bind("LinkMarkdown", delegate { string url = Dialogs.Prompt(Window, "Добавить ссылку", "Адрес ссылки", "https://"); if (url != null) WrapSelection("[", "](" + url + ")"); });
            Bind("AboutMenu", delegate { Dialogs.Show(Window, "Apple Notepad 1.1.0\n\nТекстовый редактор для Windows с интерфейсом в стиле macOS.\nРаботает локально, без аккаунта и интернета.\n\nНезависимый проект, не связан с Apple или Microsoft.", "О программе", MessageBoxButton.OK, MessageBoxImage.Information); });
            Bind("HelpMenu", delegate { Dialogs.Show(Window, "Ctrl+N — новая вкладка\nCtrl+O — открыть\nCtrl+S — сохранить\nCtrl+Shift+S — сохранить как\nCtrl+W — закрыть вкладку\nCtrl+F / Ctrl+H — поиск / замена\nF3 / Shift+F3 — далее / ранее\nCtrl+G — перейти к строке\nF5 — время и дата\nCtrl+P — печать\nCtrl+колесо — масштаб\nF11 — полный экран\nCtrl+Tab — следующая вкладка\n\nПолная инструкция — README.md рядом с программой.", "Горячие клавиши"); });
            Control<TextBox>("FindText").KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Find(Keyboard.Modifiers == ModifierKeys.Shift); e.Handled = true; } };
            MenuStyling.Attach(Window, Control<TextBox>("FindText")); MenuStyling.Attach(Window, Control<TextBox>("ReplaceText"));
            Control<TabControl>("Documents").SelectionChanged += delegate(object sender, SelectionChangedEventArgs e) { if (e.Source == Control<TabControl>("Documents")) { RefreshStatus(); recoveryDirty = true; } };
            Control<ComboBox>("EncodingChoice").SelectionChanged += delegate { if (!syncing && Current != null) { Current.File.EncodingName = (string)((ComboBoxItem)Control<ComboBox>("EncodingChoice").SelectedItem).Tag; MetadataChanged(); } };
            Control<ComboBox>("NewlineChoice").SelectionChanged += delegate { if (!syncing && Current != null) { Current.File.Newline = (string)((ComboBoxItem)Control<ComboBox>("NewlineChoice").SelectedItem).Tag; MetadataChanged(); } };
            Window.PreviewKeyDown += KeyDown;
            Window.PreviewMouseWheel += delegate(object sender, MouseWheelEventArgs e) { if (Keyboard.Modifiers == ModifierKeys.Control) { Zoom(e.Delta > 0 ? 10 : -10); e.Handled = true; } };
            Window.AllowDrop = true; Window.PreviewDragOver += delegate(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            Window.Drop += delegate(object sender, DragEventArgs e) { string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[]; if (paths != null) foreach (string path in paths) Safe(delegate { OpenPath(path); }); e.Handled = true; };
            recovery.Tick += delegate { if (recoveryDirty && Settings.RestoreSession) Safe(SaveSession); }; recovery.Start();
            Window.Closing += Closing;
            Window.Closed += delegate { recovery.Stop(); vault.Dispose(); };
            Window.Activated += delegate { if (!TestMode) Safe(CheckDisk); };
            Window.SizeChanged += delegate { RefreshStatus(); };
            if (Settings.RestoreSession && !newWindow && !test) Restore();
            if (Tabs.Count == 0) NewTab(new TextFile());
            ApplyPreferences(); RefreshRecent();
            Window.Loaded += delegate { if (Current != null) Current.Editor.Focus(); };
        }
        private static bool AncestorButton(DependencyObject node)
        {
            while (node != null) { if (node is Button) return true; node = VisualTreeHelper.GetParent(node); } return false;
        }
        internal void Safe(Action action) { try { action(); } catch (Exception error) { Program.Log(error); if (TestMode) throw; Dialogs.Show(Window, error.Message, "Apple Notepad", MessageBoxButton.OK, MessageBoxImage.Warning); } }
        private void Bind(string name, Action action)
        {
            object control = Window.FindName(name); MenuItem menu = control as MenuItem; ButtonBase button = control as ButtonBase;
            if (menu != null) menu.Click += delegate { Safe(action); }; else if (button != null) button.Click += delegate { Safe(action); };
        }
        internal EditorTab NewTab(TextFile file)
        {
            EditorTab item = new EditorTab { File = file, Title = new TextBlock { MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center } };
            StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
            Button close = new Button { Content = "×", Padding = new Thickness(3, 0, 3, 0), Margin = new Thickness(13, 0, 0, 0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 14, ToolTip = "Закрыть вкладку" };
            header.Children.Add(item.Title); header.Children.Add(close);
            item.Editor = new TextBox { Text = file.Text, AcceptsReturn = true, AcceptsTab = true, VerticalContentAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(0), Padding = new Thickness(28, 20, 28, 30), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, UndoLimit = 1000, IsUndoEnabled = true };
            item.Editor.Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
            MenuStyling.Attach(Window, item.Editor, delegate { ShowFind(false); });
            item.Editor.SetResourceReference(TextBox.BackgroundProperty, "Paper"); item.Editor.SetResourceReference(TextBox.ForegroundProperty, "Ink"); item.Editor.SetResourceReference(TextBox.CaretBrushProperty, "Ink");
            item.Numbers = new Canvas { Width = 52, IsHitTestVisible = false, ClipToBounds = true };
            item.Host = new Grid(); item.Host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); item.Host.ColumnDefinitions.Add(new ColumnDefinition());
            item.Host.Children.Add(item.Numbers); item.Host.Children.Add(item.Editor); Grid.SetColumn(item.Editor, 1);
            item.Preview = new FlowDocumentScrollViewer { Visibility = Visibility.Collapsed, IsToolBarVisible = false }; Grid.SetColumnSpan(item.Preview, 2); item.Host.Children.Add(item.Preview);
            item.Tab = new TabItem { Header = header, Content = item.Host, Tag = item };
            close.Click += delegate { Safe(delegate { CloseTab(item); }); };
            item.Tab.MouseDown += delegate(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Middle) { Safe(delegate { CloseTab(item); }); e.Handled = true; } };
            item.Editor.TextChanged += delegate { item.File.Text = TextFile.Normalize(item.Editor.Text); RefreshTab(item); if (Current == item) RefreshStatus(); UpdateNumbers(item); recoveryDirty = true; };
            item.Editor.SelectionChanged += delegate { if (Current == item) RefreshStatus(); };
            item.Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(delegate { UpdateNumbers(item); }));
            item.Editor.SizeChanged += delegate { UpdateNumbers(item); };
            Tabs.Add(item); Control<TabControl>("Documents").Items.Add(item.Tab); Control<TabControl>("Documents").SelectedItem = item.Tab;
            ApplyTabPreferences(item); RefreshTab(item); RefreshStatus(); item.Editor.Focus(); recoveryDirty = true; return item;
        }
        internal void OpenPath(string path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path)) throw new IOException("Выберите файл, а не папку.");
            if (!File.Exists(path))
            {
                if (Dialogs.Show(Window, "Файл не найден. Создать новый документ с этим именем?\n" + path, "Apple Notepad", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    NewTab(new TextFile { Path = path, MetadataDirty = true });
                return;
            }
            foreach (EditorTab opened in Tabs) if (String.Equals(opened.File.Path, path, StringComparison.OrdinalIgnoreCase)) { Control<TabControl>("Documents").SelectedItem = opened.Tab; return; }
            TextFile file = TextFile.Open(path);
            EditorTab empty = Tabs.Count == 1 && !Tabs[0].File.Dirty && Tabs[0].File.Path == "" && Tabs[0].Editor.Text == "" ? Tabs[0] : null;
            EditorTab item = NewTab(file);
            if (empty != null) { Tabs.Remove(empty); Control<TabControl>("Documents").Items.Remove(empty.Tab); }
            if (file.Text.StartsWith(".LOG", StringComparison.Ordinal)) { item.Editor.AppendText(Environment.NewLine + DateStamp() + Environment.NewLine); item.Editor.CaretIndex = item.Editor.Text.Length; }
            Remember(path);
        }
        private void PickOpen()
        {
            string directory = Current == null || String.IsNullOrEmpty(Current.File.Path) ? null : Path.GetDirectoryName(Current.File.Path);
            string[] paths = FilePicker.Open(Window, directory);
            if (paths != null) foreach (string path in paths) Safe(delegate { OpenPath(path); });
        }        internal bool Save(EditorTab tab, bool saveAs)
        {
            if (tab == null) return false;
            tab.File.Text = TextFile.Normalize(tab.Editor.Text); string path = tab.File.Path;
            if (saveAs || String.IsNullOrEmpty(path))
            {
                string selected = FilePicker.Save(Window, path);
                if (selected == null) return false; path = selected;
            }
            else if (tab.File.ChangedOnDisk() && File.Exists(path) && Dialogs.Show(Window, "Файл изменён другой программой. Перезаписать его вашей версией?", "Конфликт изменений", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return false;
            foreach (EditorTab other in Tabs) if (other != tab && String.Equals(other.File.Path, path, StringComparison.OrdinalIgnoreCase)) { Dialogs.Show(Window, "Этот файл уже открыт в другой вкладке. Сохраните под другим именем.", "Apple Notepad"); return false; }
            tab.File.Save(path); RefreshTab(tab); Remember(path); RefreshStatus(); recoveryDirty = true;
            if (Settings.RestoreSession) SaveSession(); return true;
        }
        internal bool CloseTab(EditorTab tab)
        {
            if (tab == null) return true;
            if (!ConfirmClose(tab)) return false;
            Tabs.Remove(tab); Control<TabControl>("Documents").Items.Remove(tab.Tab);
            if (Tabs.Count == 0) NewTab(new TextFile()); RefreshStatus(); recoveryDirty = true; if (Settings.RestoreSession) SaveSession(); return true;
        }
        private bool ConfirmClose(EditorTab tab)
        {
            if (!tab.File.Dirty) return true;
            MessageBoxResult answer = Dialogs.Show(Window, "Сохранить изменения в «" + Name(tab) + "»?", "Закрытие документа", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return answer == MessageBoxResult.No || answer == MessageBoxResult.Yes && Save(tab, false);
        }
        private static string Name(EditorTab tab) { return String.IsNullOrEmpty(tab.File.Path) ? "Без названия" : Path.GetFileName(tab.File.Path); }
        private void RefreshTab(EditorTab tab) { tab.Title.Text = (tab.File.Dirty ? "●  " : "") + Name(tab); tab.Tab.ToolTip = String.IsNullOrEmpty(tab.File.Path) ? "Несохранённый документ" : tab.File.Path; }
        internal void RefreshStatus()
        {
            if (Current == null) return;
            EditorTab tab = Current; string prefix = tab.Editor.Text.Substring(0, tab.Editor.SelectionStart); string normalized = TextFile.Normalize(prefix);
            Control<TextBlock>("PositionStatus").MaxWidth = Math.Max(180, Window.ActualWidth - 440);
            Control<TextBlock>("PositionStatus").TextTrimming = TextTrimming.CharacterEllipsis;
            Control<TextBlock>("SavedIndicator").Visibility = Window.ActualWidth < 850 ? Visibility.Collapsed : Visibility.Visible;
            int line = 1; foreach (char c in normalized) if (c == '\n') line++;
            int column = normalized.Length - normalized.LastIndexOf('\n');
            Control<TextBlock>("PositionStatus").Text = "Строка " + line + ", столбец " + column + "     ·     " + tab.File.Text.Length.ToString("N0") + " символов" + (tab.Editor.SelectionLength > 0 ? "  ·  выделено " + tab.Editor.SelectionLength : "");
            Control<TextBlock>("SavedIndicator").Text = tab.File.Dirty ? "Изменения в документе" : "Все изменения сохранены";
            Window.Title = Name(tab) + (tab.File.Dirty ? " •" : "") + " — Apple Notepad";
            syncing = true;
            foreach (ComboBoxItem item in Control<ComboBox>("EncodingChoice").Items) if ((string)item.Tag == tab.File.EncodingName) Control<ComboBox>("EncodingChoice").SelectedItem = item;
            foreach (ComboBoxItem item in Control<ComboBox>("NewlineChoice").Items) if ((string)item.Tag == tab.File.Newline) Control<ComboBox>("NewlineChoice").SelectedItem = item;
            Control<MenuItem>("MarkdownPreview").IsChecked = tab.Preview.Visibility == Visibility.Visible;
            syncing = false;
        }
        private void MetadataChanged() { Current.File.MetadataDirty = true; RefreshTab(Current); recoveryDirty = true; }
        private void Remember(string path)
        {
            Settings.Recent.RemoveAll(delegate(string item) { return String.Equals(item, path, StringComparison.OrdinalIgnoreCase); }); Settings.Recent.Insert(0, path);
            if (Settings.Recent.Count > 12) Settings.Recent.RemoveRange(12, Settings.Recent.Count - 12); Settings.Save(); RefreshRecent();
        }
        private void RefreshRecent()
        {
            MenuItem menu = Control<MenuItem>("RecentMenu"); menu.Items.Clear(); menu.IsEnabled = Settings.Recent.Count > 0;
            foreach (string path in Settings.Recent) { string captured = path; MenuItem item = new MenuItem { Header = Path.GetFileName(path).Replace("_", "__"), ToolTip = path }; item.Click += delegate { Safe(delegate { OpenPath(captured); }); }; menu.Items.Add(item); }
        }
        internal void ShowFind(bool replace)
        {
            Control<Border>("FindBar").Visibility = Visibility.Visible; Control<DockPanel>("ReplaceRow").Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
            if (Current != null && Current.Editor.SelectionLength > 0 && !Current.Editor.SelectedText.Contains("\n")) Control<TextBox>("FindText").Text = Current.Editor.SelectedText;
            Control<TextBox>("FindText").Focus(); Control<TextBox>("FindText").SelectAll();
        }
        private void HideFind() { Control<Border>("FindBar").Visibility = Visibility.Collapsed; if (Current != null) Current.Editor.Focus(); }
        internal void Find(bool reverse)
        {
            if (Current == null) return;
            string term = Control<TextBox>("FindText").Text;
            if (term.Length == 0) { ShowFind(false); return; }
            TextBox editor = Current.Editor;
            int start = reverse ? editor.SelectionStart - 1 : editor.SelectionStart + editor.SelectionLength;
            int index = Search.Find(editor.Text, term, start, reverse, Control<CheckBox>("MatchCase").IsChecked == true, Control<CheckBox>("WholeWord").IsChecked == true, Control<CheckBox>("WrapSearch").IsChecked == true);
            Control<TextBlock>("SearchResult").Text = index < 0 ? "Совпадений больше нет" : "Найдено";
            if (index >= 0) { editor.Select(index, term.Length); editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(index)); editor.Focus(); }
        }
        private void ReplaceOne()
        {
            if (Current == null) return;
            TextBox editor = Current.Editor; string term = Control<TextBox>("FindText").Text;
            if (editor.SelectionLength == term.Length && Search.At(editor.Text, editor.SelectionStart, term, Control<CheckBox>("MatchCase").IsChecked == true, Control<CheckBox>("WholeWord").IsChecked == true)) editor.SelectedText = Control<TextBox>("ReplaceText").Text;
            Find(false);
        }
        internal void ReplaceAll()
        {
            if (Current == null) return;
            int count; string result = Search.ReplaceAll(Current.Editor.Text, Control<TextBox>("FindText").Text, Control<TextBox>("ReplaceText").Text, Control<CheckBox>("MatchCase").IsChecked == true, Control<CheckBox>("WholeWord").IsChecked == true, out count);
            if (count > 0) { Current.Editor.BeginChange(); Current.Editor.SelectAll(); Current.Editor.SelectedText = result; Current.Editor.EndChange(); }
            Control<TextBlock>("SearchResult").Text = "Заменено: " + count;
        }
        private void GoTo()
        {
            if (Current == null) return; string value = Dialogs.Prompt(Window, "Перейти к строке", "Номер строки", "1"); if (value == null) return;
            int target; if (!Int32.TryParse(value, out target) || target < 1) throw new ArgumentException("Введите положительный номер строки.");
            string text = Current.Editor.Text; int index = 0, line = 1;
            while (line < target && index < text.Length) { int next = text.IndexOf('\n', index); if (next < 0) break; index = next + 1; line++; }
            if (line != target) throw new ArgumentException("В документе нет такой строки.");
            Current.Editor.Focus(); Current.Editor.Select(index, 0); Current.Editor.ScrollToLine(Current.Editor.GetLineIndexFromCharacterIndex(index));
        }
        private static string DateStamp() { return DateTime.Now.ToShortTimeString() + " " + DateTime.Now.ToShortDateString(); }
        private void InsertDate() { if (Current != null) { Current.Editor.SelectedText = DateStamp(); Current.Editor.Focus(); } }
        private void WrapSelection(string before, string after)
        {
            if (Current == null) return; TextBox editor = Current.Editor; int start = editor.SelectionStart; string text = editor.SelectedText;
            editor.SelectedText = before + text + after; editor.Select(start + before.Length, text.Length); editor.Focus();
        }
        private void InsertLinePrefix(string prefix)
        {
            if (Current == null) return; TextBox editor = Current.Editor; int line = editor.SelectionStart == 0 ? -1 : editor.Text.LastIndexOf('\n', editor.SelectionStart - 1);
            editor.Select(line + 1, 0); editor.SelectedText = prefix; editor.Focus();
        }
        private void TogglePreview()
        {
            if (Current == null) return; EditorTab tab = Current; bool show = tab.Preview.Visibility != Visibility.Visible;
            if (show) tab.Preview.Document = Markdown.Create(tab.File.Text, Settings.Theme == "dark");
            tab.Preview.Visibility = show ? Visibility.Visible : Visibility.Collapsed; tab.Editor.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            RefreshStatus(); if (!show) tab.Editor.Focus();
        }
        private void Zoom(int delta) { Settings.Zoom = Math.Max(20, Math.Min(500, Settings.Zoom + delta)); ApplyPreferences(); }
        internal void ApplyPreferences()
        {
            bool dark = Settings.Theme == "dark";
            SetBrush("Chrome", dark ? "#202126" : "#F5F5F7"); SetBrush("Paper", dark ? "#292B31" : "#FFFFFF"); SetBrush("Ink", dark ? "#E6E7ED" : "#25262B"); SetBrush("Muted", dark ? "#A0A3AF" : "#83858D"); SetBrush("Line", dark ? "#3B3D46" : "#E2E3E8");
            SetBrush("MenuSurface", dark ? "#303238" : "#FAFAFC"); SetBrush("MenuEdge", dark ? "#4B4D55" : "#DCDDE2"); SetBrush("MenuHover", dark ? "#45474F" : "#E7E8ED");
            SetBrush("ScrollThumb", dark ? "#666872" : "#B6B7BD"); SetBrush("ScrollHover", dark ? "#8F929E" : "#8D8F97");
            foreach (EditorTab tab in Tabs) ApplyTabPreferences(tab);
            Control<MenuItem>("WordWrap").IsChecked = Settings.Wrap; Control<MenuItem>("LineNumbers").IsChecked = Settings.LineNumbers; Control<MenuItem>("SpellcheckMenu").IsChecked = Settings.SpellCheck;
            Control<MenuItem>("StatusBarMenu").IsChecked = Settings.StatusBar; Control<MenuItem>("LightTheme").IsChecked = !dark; Control<MenuItem>("DarkTheme").IsChecked = dark;
            Control<Border>("StatusArea").Visibility = Settings.StatusBar ? Visibility.Visible : Visibility.Collapsed;
            Control<TextBlock>("ZoomStatus").Text = Settings.Zoom + "%"; Settings.Save();
        }
        private void SetBrush(string key, string color) { Window.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        private void ApplyTabPreferences(EditorTab tab)
        {
            tab.Editor.FontFamily = new FontFamily(Settings.Font); tab.Editor.FontSize = Settings.FontSize * Settings.Zoom / 100.0;
            tab.Editor.FontWeight = Settings.Bold ? FontWeights.Bold : FontWeights.Normal; tab.Editor.FontStyle = Settings.Italic ? FontStyles.Italic : FontStyles.Normal;
            TextBlock.SetLineHeight(tab.Editor, tab.Editor.FontSize * 1.6); tab.Editor.TextWrapping = Settings.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            tab.Editor.HorizontalScrollBarVisibility = Settings.Wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            SpellCheck.SetIsEnabled(tab.Editor, Settings.SpellCheck);
            tab.Numbers.Visibility = Settings.LineNumbers ? Visibility.Visible : Visibility.Collapsed;
            if (tab.Preview.Visibility == Visibility.Visible) tab.Preview.Document = Markdown.Create(tab.File.Text, Settings.Theme == "dark"); UpdateNumbers(tab);
        }
        private void UpdateNumbers(EditorTab tab)
        {
            if (!Settings.LineNumbers || tab.Editor.ActualHeight == 0) return;
            TextBox editor = tab.Editor; string text = editor.Text; tab.Numbers.Children.Clear();
            int visible = Math.Max(0, editor.GetCharacterIndexFromPoint(new Point(2, 2), true));
            int start = visible == 0 ? 0 : text.LastIndexOf('\n', Math.Min(visible - 1, text.Length - 1)) + 1;
            int line = 1; for (int i = 0; i < start; i++) if (text[i] == '\n') line++;
            int stop = Math.Max(start, editor.GetCharacterIndexFromPoint(new Point(editor.ActualWidth - 2, editor.ActualHeight - 2), true));
            for (int index = start, shown = 0; index <= text.Length && shown < 250; shown++)
            {
                Rect position = editor.GetRectFromCharacterIndex(index);
                if (!position.IsEmpty && position.Y >= -editor.FontSize && position.Y < editor.ActualHeight)
                {
                    TextBlock number = new TextBlock { Text = line.ToString(), FontFamily = editor.FontFamily, FontSize = editor.FontSize, Width = 40, TextAlignment = TextAlignment.Right };
                    number.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Canvas.SetTop(number, position.Y); tab.Numbers.Children.Add(number);
                }
                int next = text.IndexOf('\n', index); if (next < 0 || next >= stop) break; index = next + 1; line++;
            }
        }
        private void PickFont()
        {
            FontSelection value = FontPicker.Pick(Window, Settings);
            if (value == null) return;
            Settings.Font = value.Family; Settings.FontSize = value.Size; Settings.Bold = value.Bold; Settings.Italic = value.Italic; ApplyPreferences();
        }        private void ShowSettings()
        {
            Window dialog = Dialogs.Create(Window, "Настройки", 460, 410); StackPanel panel = Dialogs.Body(dialog);
            TextBlock heading = new TextBlock { Text = "Ваше рабочее пространство", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 20) }; panel.Children.Add(heading);
            CheckBox restore = new CheckBox { Content = "Восстанавливать вкладки и несохранённый текст", IsChecked = Settings.RestoreSession, Margin = new Thickness(0, 0, 0, 15) };
            CheckBox spell = new CheckBox { Content = "Проверять орфографию", IsChecked = Settings.SpellCheck, Margin = new Thickness(0, 0, 0, 15) };
            CheckBox wrap = new CheckBox { Content = "Переносить длинные строки", IsChecked = Settings.Wrap, Margin = new Thickness(0, 0, 0, 15) };
            CheckBox numbers = new CheckBox { Content = "Показывать номера строк", IsChecked = Settings.LineNumbers, Margin = new Thickness(0, 0, 0, 18) };
            panel.Children.Add(restore); panel.Children.Add(spell); panel.Children.Add(wrap); panel.Children.Add(numbers);
            panel.Children.Add(new TextBlock { Text = "Восстановление хранится только на этом компьютере.\nОригинальные файлы меняются после «Сохранить».", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0, 0, 0, 20) });
            Button save = Dialogs.Button("Готово", true); save.IsDefault = true; save.HorizontalAlignment = HorizontalAlignment.Right; panel.Children.Add(save);
            save.Click += delegate { Settings.RestoreSession = restore.IsChecked == true; Settings.SpellCheck = spell.IsChecked == true; Settings.Wrap = wrap.IsChecked == true; Settings.LineNumbers = numbers.IsChecked == true; ApplyPreferences(); if (!Settings.RestoreSession) vault.Forget(); dialog.Close(); };
            dialog.ShowDialog();
        }
        private void ToggleMaximize() { Window.WindowState = Window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void ToggleFullScreen()
        {
            if (!fullScreen) { previousState = Window.WindowState; Window.WindowState = WindowState.Normal; Window.ResizeMode = ResizeMode.NoResize; Window.WindowState = WindowState.Maximized; }
            else { Window.ResizeMode = ResizeMode.CanResize; Window.WindowState = previousState; } fullScreen = !fullScreen;
        }
        private void CheckDisk()
        {
            if (checkingDisk || Current == null || !Current.File.ChangedOnDisk()) return;
            checkingDisk = true;
            try
            {
                EditorTab tab = Current;
                if (!File.Exists(tab.File.Path)) return;
                FileInfo disk = new FileInfo(tab.File.Path);
                if (disk.LastWriteTimeUtc.Ticks == tab.IgnoredDiskTicks && disk.Length == tab.IgnoredDiskLength) return;
                if (Dialogs.Show(Window, "Файл «" + Name(tab) + "» изменён другой программой. Загрузить новую версию?" + (tab.File.Dirty ? "\nВаши несохранённые изменения будут заменены." : ""), "Файл изменился", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                { tab.File = TextFile.Open(tab.File.Path); tab.Editor.IsUndoEnabled = false; tab.Editor.Text = tab.File.Text; tab.Editor.IsUndoEnabled = true; RefreshTab(tab); RefreshStatus(); }
                else { tab.IgnoredDiskTicks = disk.LastWriteTimeUtc.Ticks; tab.IgnoredDiskLength = disk.Length; }
            }
            finally { checkingDisk = false; }
        }
        private void Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (!Settings.RestoreSession) foreach (EditorTab tab in Tabs) if (!ConfirmClose(tab)) { e.Cancel = true; return; }
                if (Window.WindowState == WindowState.Normal) { Settings.Width = Window.Width; Settings.Height = Window.Height; }
                Settings.Save(); if (Settings.RestoreSession) SaveSession(); else vault.Forget();
            }
            catch (Exception error) { e.Cancel = true; Program.Log(error); Dialogs.Show(Window, "Не удалось сохранить состояние. Окно оставлено открытым, чтобы не потерять текст.\n" + error.Message, "Apple Notepad"); }
        }
        internal void SaveSession()
        {
            Session session = new Session { Selected = Control<TabControl>("Documents").SelectedIndex };
            foreach (EditorTab tab in Tabs) session.Tabs.Add(new SessionTab { Path = tab.File.Path, Text = TextFile.Normalize(tab.Editor.Text), SavedText = tab.File.SavedText, Encoding = tab.File.EncodingName, Newline = tab.File.Newline, Caret = tab.Editor.CaretIndex, DiskTicks = tab.File.DiskTicks, DiskLength = tab.File.DiskLength, MetadataDirty = tab.File.MetadataDirty });
            vault.Save(session); recoveryDirty = false;
        }
        private void Restore()
        {
            foreach (Session session in vault.Recover())
            {
                if (session.Tabs == null) continue;
                foreach (SessionTab saved in session.Tabs)
                {
                    EditorTab tab = NewTab(new TextFile { Path = saved.Path ?? "", Text = saved.Text ?? "", SavedText = saved.SavedText ?? "", EncodingName = saved.Encoding ?? "utf8", Newline = saved.Newline ?? "CRLF", DiskTicks = saved.DiskTicks, DiskLength = saved.DiskLength, MetadataDirty = saved.MetadataDirty });
                    tab.Editor.CaretIndex = Math.Max(0, Math.Min(tab.Editor.Text.Length, saved.Caret));
                }
                if (session.Selected >= 0 && session.Selected < Tabs.Count) Control<TabControl>("Documents").SelectedIndex = session.Selected;
            }
        }
        private void KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0; Action action = null;
            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.N: action = shift ? (Action)delegate { Process.Start(Process.GetCurrentProcess().MainModule.FileName, "--new-window"); } : delegate { NewTab(new TextFile()); }; break;
                    case Key.O: action = PickOpen; break;
                    case Key.S: action = delegate { Save(Current, shift); }; break;
                    case Key.W: action = delegate { CloseTab(Current); }; break;
                    case Key.F: action = delegate { ShowFind(false); }; break;
                    case Key.H: action = delegate { ShowFind(true); }; break;
                    case Key.G: action = GoTo; break;
                    case Key.P: action = delegate { if (Current != null) printing.Print(Window, Current.File, Settings); }; break;
                    case Key.B: action = delegate { WrapSelection("**", "**"); }; break;
                    case Key.I: action = delegate { WrapSelection("*", "*"); }; break;
                    case Key.Add: case Key.OemPlus: action = delegate { Zoom(10); }; break;
                    case Key.Subtract: case Key.OemMinus: action = delegate { Zoom(-10); }; break;
                    case Key.D0: case Key.NumPad0: action = delegate { Settings.Zoom = 100; ApplyPreferences(); }; break;
                    case Key.Tab: action = delegate { TabControl tabs = Control<TabControl>("Documents"); tabs.SelectedIndex = (tabs.SelectedIndex + (shift ? Tabs.Count - 1 : 1)) % Tabs.Count; }; break;
                }
            }
            else if (e.Key == Key.Escape) action = HideFind;
            else if (e.Key == Key.F3) action = delegate { Find(shift); };
            else if (e.Key == Key.F5) action = InsertDate;
            else if (e.Key == Key.F11) action = ToggleFullScreen;
            if (action != null) { e.Handled = true; Safe(action); }
        }
    }
}
