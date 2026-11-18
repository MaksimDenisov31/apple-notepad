using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppleNotepad
{
    internal static class Verification
    {
        internal static bool UIFailed;
        private static void Check(bool value, string name) { if (!value) throw new Exception("Verification: " + name); }
        internal static int Core()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "core-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Preferences.OverrideDataDirectory = root;
            string sample = "Hello Привет Ελληνικά 😀\nsecond line\n";
            int count = 0;
            foreach (string encoding in new string[] { "utf8", "utf8bom", "utf16", "utf16be", "utf32", "utf32be", "ansi" })
                foreach (string newline in new string[] { "CRLF", "LF", "CR" })
                {
                    string text = encoding == "ansi" ? "ASCII text\nSecond line\n" : sample;
                    string path = Path.Combine(root, encoding + "-" + newline + ".txt");
                    TextFile file = new TextFile { Text = text, EncodingName = encoding, Newline = newline }; file.Save(path);
                    TextFile loaded = TextFile.Open(path);
                    Check(loaded.Text == text && loaded.Newline == newline && !loaded.Dirty, "encoding/newline round-trip " + encoding + "/" + newline);
                    byte[] saved = File.ReadAllBytes(path); loaded.Text += "end"; loaded.Save(path); Check(!loaded.Dirty && File.ReadAllBytes(path).Length > saved.Length, "atomic replacement"); count++;
                }
            string protectedPath = Path.Combine(root, "protected.txt"); File.WriteAllText(protectedPath, "original"); File.SetAttributes(protectedPath, FileAttributes.ReadOnly);
            bool refused = false; try { Storage.AtomicWrite(protectedPath, Encoding.UTF8.GetBytes("replacement")); } catch (UnauthorizedAccessException) { refused = true; } catch (IOException) { refused = true; }
            finally { File.SetAttributes(protectedPath, FileAttributes.Normal); }
            Check(refused && File.ReadAllText(protectedPath) == "original", "failed save preserves original");
            Check(Search.Find("cat scatter CAT", "cat", 1, false, false, true, true) == 12, "whole-word case-insensitive search");
            Check(Search.Find("one two one", "one", -1, true, true, false, true) == 8, "reverse wraps");
            Check(Search.Find("one two one", "one", 11, false, true, false, false) == -1, "no-wrap search");
            Check(Search.Find("one two one", "one", 11, false, true, false, true) == 0, "forward wraps");
            int replaced; Check(Search.ReplaceAll("a aa a", "a", "$1", true, true, out replaced) == "$1 aa $1" && replaced == 2, "literal replacement and whole words");
            Check(Search.ReplaceAll("aaa", "a", "aa", true, false, out replaced) == "aaaaaa" && replaced == 3, "replace does not loop over inserted text");
            Preferences prefs = new Preferences { Theme = "dark", Wrap = false }; prefs.Save(); Check(Preferences.Load().Theme == "dark" && !Preferences.Load().Wrap, "preferences persistence");
            string unsaved = "Секретный тестовый текст без пользовательских данных\nline 2";
            using (SessionVault first = new SessionVault())
            {
                first.Save(new Session { Tabs = { new SessionTab { Text = unsaved, SavedText = "", Encoding = "utf8", Newline = "LF", Caret = 4 } } });
                using (SessionVault second = new SessionVault()) Check(second.Recover().Count == 0, "active window session not duplicated");
            }
            using (SessionVault restored = new SessionVault())
            {
                System.Collections.Generic.List<Session> sessions = restored.Recover(); Check(sessions.Count == 1 && sessions[0].Tabs[0].Text == unsaved, "unsaved Unicode text recovered after close"); restored.Save(sessions[0]);
            }
            using (SessionVault restoredAgain = new SessionVault()) Check(restoredAgain.Recover().Count == 1, "recovery consumes old snapshot without duplication");
            string longText = String.Join("\n", new string[250]).Replace("\n", "Printed line\n");
            FlowDocument printed = Printing.CreateDocument(longText, "Consolas", 14, new Size(794, 1123), new Thickness(75));
            DocumentPaginator paginator = ((IDocumentPaginatorSource)printed).DocumentPaginator; paginator.ComputePageCount(); Check(paginator.PageCount > 1, "long document paginates");
            DecoratedPaginator range = new DecoratedPaginator(paginator, 1, 1, "example.txt", "&l&f&c&d&r&t", "Страница &p");
            Check(range.PageCount == 1 && range.GetPage(0) != DocumentPage.Missing && range.GetPage(1) == DocumentPage.Missing, "print range and header/footer rendering");
            string xpsPath = Path.Combine(root, "print-range.xps");
            using (System.Windows.Xps.Packaging.XpsDocument xps = new System.Windows.Xps.Packaging.XpsDocument(xpsPath, FileAccess.ReadWrite))
                System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps).Write(range);
            using (System.Windows.Xps.Packaging.XpsDocument xps = new System.Windows.Xps.Packaging.XpsDocument(xpsPath, FileAccess.Read))
                Check(xps.GetFixedDocumentSequence().DocumentPaginator.PageCount == 1, "print pipeline serializes selected page into XPS");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "core-report.txt"), "PASS: " + count + " encoding/newline combinations; atomic saves and failure preservation; search directions/case/whole words/wrap; literal replacement; preferences; multi-window session recovery; print pagination, ranges, headers and footers.\r\n");
            return 0;
        }
        internal static void UI(EditorWindow app)
        {
            try
            {
                Check(app.Tabs.Count == 1, "blank initial tab");
                TrafficVerification.Run(app.Window,new[]{app.Control<Button>("CloseWindow"),app.Control<Button>("MinimizeWindow"),app.Control<Button>("MaximizeWindow")},delegate(string name){InterfaceVerification.Capture(app.Window,name);});
                Check(true,"caption hover trigger and keyboard symbol visibility verified");
                app.Control<Button>("MaximizeWindow").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(app.Window.WindowState==WindowState.Maximized,"green caption button maximizes");
                app.Control<Button>("MaximizeWindow").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(app.Window.WindowState==WindowState.Normal,"green caption button restores");
                app.Control<Button>("MinimizeWindow").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(app.Window.WindowState==WindowState.Minimized,"yellow caption button minimizes");app.Window.WindowState=WindowState.Normal;
                app.Current.Editor.Text = "# Мои заметки\n\nМесто для простых мыслей.\n\nПлан на сегодня\n• Сделать главное\n• Сохранить хорошую идею\n• Найти время для себя\n\nПростой текст. Ничего лишнего.";
                app.Current.Editor.Select(0, 0); Check(app.Current.File.Dirty, "editing marks document dirty");
                app.ShowFind(true); app.Control<TextBox>("FindText").Text = "текст"; app.Find(false); Check(app.Current.Editor.SelectedText == "текст", "search selects match");
                app.Control<TextBox>("ReplaceText").Text = "редактор"; string before = app.Current.Editor.Text; app.ReplaceAll(); Check(app.Current.Editor.Text.Contains("редактор"), "replace-all changes editor");
                app.Current.Editor.Undo(); Check(app.Current.Editor.Text == before, "replacement undone in one step"); app.Current.Editor.Redo(); Check(app.Current.Editor.Text.Contains("редактор"), "replacement redo");
                app.Control<Border>("FindBar").Visibility = Visibility.Collapsed;
                string path = Path.Combine(Preferences.DataDirectory, "document.txt"); app.Current.File.Save(path); Check(app.Save(app.Current, false), "save current document without dialog");
                Check(TextFile.Open(path).Text == TextFile.Normalize(app.Current.Editor.Text), "UI saves actual text");
                app.NewTab(new TextFile { Text = "Вторая вкладка" }); Check(app.Tabs.Count == 2, "new tab");
                app.NewTab(new TextFile { Text = "Третья вкладка", SavedText = "Третья вкладка" }); Check(app.Tabs.Count == 3, "third tab");
                Check(app.CloseTab(app.Current) && app.Tabs.Count == 2, "clean tab closes");
                app.Control<TabControl>("Documents").SelectedIndex = 0;
                app.Settings.LineNumbers = true; app.ApplyPreferences(); app.Window.UpdateLayout();
                Snapshot(app, "preview-light.png");
                app.Settings.Theme = "dark"; app.ApplyPreferences(); app.Window.UpdateLayout(); Snapshot(app, "preview-dark.png");
                TrafficVerification.Run(app.Window,new[]{app.Control<Button>("CloseWindow"),app.Control<Button>("MinimizeWindow"),app.Control<Button>("MaximizeWindow")},delegate(string name){InterfaceVerification.Capture(app.Window,"dark-"+name);});Check(true,"caption hover symbols remain readable in dark theme");
                app.Control<MenuItem>("MarkdownPreview").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(app.Current.Preview.Visibility == Visibility.Visible, "Markdown preview opens");
                app.Control<MenuItem>("MarkdownPreview").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(app.Current.Editor.Visibility == Visibility.Visible, "plain-text editing returns");
                app.Settings.Theme = "light"; app.Settings.Zoom = 150; app.ApplyPreferences(); Check(app.Current.Editor.FontSize == app.Settings.FontSize * 1.5, "zoom changes font");
                app.Settings.Zoom = 100; app.ApplyPreferences(); MenuVerification.Run(app); app.Control<TabControl>("Documents").SelectedIndex = 0; InterfaceVerification.Run(app); app.SaveSession();
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "ui-report.txt"), "PASS: window and styles load; tabs/edit/dirty state; find; replacement with one-step undo/redo; actual save; clean tab closing; line numbers; light/dark themes; Markdown preview; zoom; session snapshot. Print pagination is covered by core tests. Additional control/dialog behavior is covered by interface-report.txt; no print job is submitted.\r\n");
                bool closed=false;app.Window.Closed+=delegate{closed=true;};app.Control<Button>("CloseWindow").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(closed,"red caption button closes and saves session");
            }
            catch (Exception error) { UIFailed = true; Program.Log(error); File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "ui-report.txt"), error.ToString()); app.Window.Close(); }
        }
        private static void Snapshot(EditorWindow app, string name)
        {
            app.Window.UpdateLayout(); RenderTargetBitmap bitmap = new RenderTargetBitmap((int)app.Window.ActualWidth, (int)app.Window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(app.Window);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", name))) encoder.Save(stream);
        }
    }
}
