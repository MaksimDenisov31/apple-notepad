using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AppleNotepad
{
    internal static class MenuVerification
    {
        private static void Check(bool ok, string text) { if (!ok) throw new Exception("Menu verification: " + text); }
        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate { frame.Continue = false; })); Dispatcher.PushFrame(frame);
        }
        private static void Click(MenuItem item)
        {
            Check(item.IsEnabled, "item enabled: " + item.Header);
            typeof(MenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, null); Pump();
        }
        private static Popup Open(MenuItem item)
        {
            item.ApplyTemplate(); Popup popup = (Popup)item.Template.FindName("PART_Popup", item); popup.PopupAnimation = PopupAnimation.None;
            item.IsSubmenuOpen = true; Pump(); ((FrameworkElement)popup.Child).UpdateLayout();
            Check(popup.IsOpen && ((FrameworkElement)popup.Child).ActualWidth > 200, "popup opens with usable size"); return popup;
        }
        internal static void Run(EditorWindow app)
        {
            TextBox editor = app.Current.Editor; string original = editor.Text; Menu main = app.Control<Menu>("MainMenu");
            MenuItem file = (MenuItem)main.Items[0];
            foreach (string theme in new string[] { "light", "dark" })
            {
                app.Settings.Theme = theme; app.ApplyPreferences();
                Popup popup = Open(file); app.Control<MenuItem>("FileSave").Focus(); Pump();
                Border border = (Border)popup.Child;
                Check(((SolidColorBrush)border.Background).Color == ((SolidColorBrush)app.Window.FindResource("MenuSurface")).Color, "popup tracks theme");
                Capture(border, "menu-" + theme + ".png");
                Popup recent = Open(app.Control<MenuItem>("RecentMenu")); Check(((FrameworkElement)recent.Child).ActualHeight > 0, "nested recent files submenu opens");
                app.Control<MenuItem>("RecentMenu").IsSubmenuOpen = false; file.IsSubmenuOpen = false; Pump();
            }
            app.Settings.Theme = "light"; app.ApplyPreferences();
            foreach (MenuItem header in main.Items) { Open(header); header.IsSubmenuOpen = false; Pump(); }
            int before = app.Tabs.Count; Open(file); Click(app.Control<MenuItem>("FileNew")); Check(app.Tabs.Count == before + 1, "file menu command creates tab"); app.CloseTab(app.Current);
            editor = app.Current.Editor; editor.Focus();
            bool wrap = app.Settings.Wrap; MenuItem format = (MenuItem)main.Items[2]; Open(format); Click(app.Control<MenuItem>("WordWrap")); Check(app.Settings.Wrap != wrap, "checkable item invokes command");
            Open(format); Click(app.Control<MenuItem>("WordWrap")); Check(app.Settings.Wrap == wrap, "checkable item toggles back");
            editor.IsUndoEnabled = false; editor.Text = "alpha"; editor.IsUndoEnabled = true; editor.CaretIndex = editor.Text.Length; editor.SelectedText = " beta";
            ContextMenu context = editor.ContextMenu; context.PlacementTarget = editor;
            context.IsOpen = true; Pump();
            MenuItem undo = Find(context, "Отменить"), redo = Find(context, "Повторить"), copy = Find(context, "Копировать"), all = Find(context, "Выделить всё");
            Click(undo); Check(editor.Text == "alpha", "context Undo uses native editor command");
            context.IsOpen = true; Pump(); Click(redo); Check(editor.Text == "alpha beta", "context Redo uses native editor command");
            editor.Select(0, 0); context.IsOpen = true; Pump(); Check(!copy.IsEnabled, "Copy disabled without selection"); context.IsOpen = false;
            editor.Select(0, 5); context.IsOpen = true; Pump(); Check(copy.IsEnabled, "Copy enabled with selection");
            foreach (string theme in new string[] { "light", "dark" })
            {
                app.Settings.Theme = theme; app.ApplyPreferences(); context.UpdateLayout(); Pump();
                Border surface = (Border)context.Template.FindName("ContextSurface", context);
                Check(((SolidColorBrush)surface.Background).Color == ((SolidColorBrush)app.Window.FindResource("MenuSurface")).Color, "context menu tracks theme");
                Capture(context, "context-" + theme + ".png");
            }
            Click(all); Check(editor.SelectionLength == editor.Text.Length, "context Select All works"); context.IsOpen = false;
            editor.Text = original; editor.Select(0, 0); app.Settings.Theme = "light"; app.ApplyPreferences();
            Check(app.Control<TextBox>("FindText").ContextMenu != null && app.Control<TextBox>("ReplaceText").ContextMenu != null, "search fields have styled context menus");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "menu-report.txt"), "PASS: all menu headers open; nested recent submenu; checkable command; File/New; native context Undo/Redo/Select All; Copy enablement; dropdown/context light-dark theme propagation; screenshots generated without reading clipboard contents.\r\n");
        }
        private static MenuItem Find(ContextMenu menu, string title)
        {
            foreach (object item in menu.Items) { MenuItem value = item as MenuItem; if (value != null && (string)value.Header == title) return value; }
            throw new Exception("Missing context item: " + title);
        }
        private static void Capture(FrameworkElement element, string name)
        {
            element.UpdateLayout(); int width = (int)Math.Ceiling(element.ActualWidth + element.Margin.Left + element.Margin.Right), height = (int)Math.Ceiling(element.ActualHeight + element.Margin.Top + element.Margin.Bottom);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(Math.Max(1,width), Math.Max(1,height), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
            PngBitmapEncoder png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", name))) png.Save(output);
        }
    }
}
