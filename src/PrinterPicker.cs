using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AppleNotepad
{
    internal sealed class PrintSelection
    {
        internal PrintQueue Queue;
        internal PrintTicket Ticket;
        internal int First, Last;
    }
    internal sealed class PaperChoice
    {
        internal PageMediaSize Media;
        public string Label { get; set; }
        public override string ToString() { return Label; }
    }
    internal sealed class PrinterPicker : IDisposable
    {
        internal readonly Window Window;
        internal readonly ComboBox Printers, Paper, Orientation, Duplex, Color;
        internal readonly TextBox Copies, Range;
        internal readonly Button Print, Cancel;
        internal PrintSelection Result;
        private LocalPrintServer server;
        private readonly List<PrintQueue> queues = new List<PrintQueue>();
        private readonly TextBlock note;
        internal PrinterPicker(Window owner, string documentName, bool landscape, Func<Window, bool> setup)
        {
            Window = Dialogs.Create(owner, "Печать", 555, 540); StackPanel body = Dialogs.Body(Window);
            body.Children.Add(new TextBlock { Text = documentName, FontSize = 21, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 22) });
            Printers = new ComboBox { DisplayMemberPath = "FullName" }; Paper = new ComboBox { DisplayMemberPath = "Label" }; Orientation = new ComboBox(); Orientation.Items.Add("Книжная"); Orientation.Items.Add("Альбомная"); Orientation.SelectedIndex = landscape ? 1 : 0;
            Copies = new TextBox { Text = "1" }; Range = new TextBox { ToolTip = "Пусто — все страницы; например, 2-5 или 3" }; MenuStyling.Attach(Window, Copies); MenuStyling.Attach(Window, Range);
            Duplex = new ComboBox(); Color = new ComboBox();
            Row(body, "Принтер", Printers); Row(body, "Бумага", Paper); Row(body, "Ориентация", Orientation); Row(body, "Копии", Copies); Row(body, "Страницы", Range); Row(body, "Стороны", Duplex); Row(body, "Цвет", Color);
            body.Children.Add(new TextBlock { Text = "Все страницы — оставьте поле пустым.\nПоля и колонтитулы доступны в параметрах страницы.", FontSize = 12, Foreground = Brushes.Gray, Margin = new Thickness(0, 3, 0, 16) });
            DockPanel actions = new DockPanel { LastChildFill = false }; Button page = Dialogs.Button("Параметры страницы"); page.Click += delegate { Orientation.SelectedIndex = setup(Window) ? 1 : 0; }; actions.Children.Add(page);
            StackPanel right = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal }; DockPanel.SetDock(right, Dock.Right); actions.Children.Add(right);
            Cancel = Dialogs.Button("Отмена"); Cancel.IsCancel = true; Cancel.Margin = new Thickness(0, 0, 9, 0); Cancel.Click += delegate { Window.Close(); };
            Print = Dialogs.Button("Печатать", true); Print.IsDefault = true; right.Children.Add(Cancel); right.Children.Add(Print); body.Children.Add(actions);
            note = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 13, 0, 0) }; body.Children.Add(note);
            Printers.SelectionChanged += delegate { PrinterChanged(); }; Print.Click += delegate { Choose(); };
            try
            {
                server = new LocalPrintServer(); foreach (PrintQueue queue in server.GetPrintQueues(new EnumeratedPrintQueueTypes[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })) queues.Add(queue);
                Printers.ItemsSource = queues; PrintQueue defaultQueue = null; try { defaultQueue = LocalPrintServer.GetDefaultPrintQueue(); } catch (PrintSystemException) { }
                Printers.SelectedItem = queues.Find(delegate(PrintQueue queue) { return defaultQueue != null && queue.FullName == defaultQueue.FullName; }) ?? queues.FirstOrDefault();
                if (defaultQueue != null) defaultQueue.Dispose();
                if (queues.Count == 0) { note.Text = "В Windows пока нет доступных принтеров."; Print.IsEnabled = false; }
            }
            catch (Exception error) { note.Text = error.Message; Print.IsEnabled = false; }
        }
        private static void Row(Panel panel, string label, Control field)
        {
            DockPanel row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; row.Children.Add(new TextBlock { Text = label, Width = 100, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(field); panel.Children.Add(row);
        }
        private void PrinterChanged()
        {
            try
            {
                PrintQueue queue = Printers.SelectedItem as PrintQueue; if (queue == null) { Print.IsEnabled = false; return; }
                PrintTicket ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket; PrintCapabilities caps = queue.GetPrintCapabilities(ticket);
                Paper.Items.Clear(); foreach (PageMediaSize media in caps.PageMediaSizeCapability) Paper.Items.Add(new PaperChoice { Media = media, Label = (media.PageMediaSizeName.HasValue ? media.PageMediaSizeName.ToString().Replace("ISO", "") : "Бумага") + (media.Width.HasValue && media.Height.HasValue ? " · " + (media.Width.Value * 25.4 / 96).ToString("0") + " × " + (media.Height.Value * 25.4 / 96).ToString("0") + " мм" : "") });
                Paper.SelectedIndex = Paper.Items.Count > 0 ? 0 : -1;
                foreach (PaperChoice item in Paper.Items) if (ticket.PageMediaSize != null && item.Media.PageMediaSizeName == ticket.PageMediaSize.PageMediaSizeName) Paper.SelectedItem = item;
                Duplex.Items.Clear(); foreach (Duplexing option in caps.DuplexingCapability) Duplex.Items.Add(new ComboBoxItem { Content = option == Duplexing.OneSided ? "Односторонняя" : option == Duplexing.TwoSidedLongEdge ? "Двусторонняя — длинный край" : "Двусторонняя — короткий край", Tag = option });
                Duplex.SelectedIndex = Duplex.Items.Count > 0 ? 0 : -1; Duplex.IsEnabled = Duplex.Items.Count > 1;
                Color.Items.Clear(); foreach (OutputColor option in caps.OutputColorCapability) Color.Items.Add(new ComboBoxItem { Content = option == OutputColor.Color ? "Цветная" : option == OutputColor.Grayscale ? "Оттенки серого" : "Чёрно-белая", Tag = option });
                Color.SelectedIndex = Color.Items.Count > 0 ? 0 : -1; Color.IsEnabled = Color.Items.Count > 1;
                foreach (ComboBoxItem item in Duplex.Items) if (ticket.Duplexing.HasValue && (Duplexing)item.Tag == ticket.Duplexing.Value) Duplex.SelectedItem = item;
                foreach (ComboBoxItem item in Color.Items) if (ticket.OutputColor.HasValue && (OutputColor)item.Tag == ticket.OutputColor.Value) Color.SelectedItem = item;
                Copies.Text = (ticket.CopyCount ?? 1).ToString(CultureInfo.CurrentCulture);
                note.Text = ""; Print.IsEnabled = true;
            }
            catch (Exception error) { note.Text = error.Message; Print.IsEnabled = false; }
        }
        internal static bool TryRange(string text, out int first, out int last)
        {
            first = 0; last = Int32.MaxValue; if (String.IsNullOrWhiteSpace(text)) return true;
            string[] parts = text.Replace('–', '-').Split('-'); int from, to;
            if (parts.Length > 2 || !Int32.TryParse(parts[0].Trim(), out from) || from < 1) return false;
            if (parts.Length == 1) to = from; else if (!Int32.TryParse(parts[1].Trim(), out to) || to < from) return false;
            first = from - 1; last = to - 1; return true;
        }
        internal void Choose()
        {
            int copies, first, last;
            if (!Int32.TryParse(Copies.Text, out copies) || copies < 1 || copies > 999) { note.Text = "Число копий: от 1 до 999."; return; }
            if (!TryRange(Range.Text, out first, out last)) { note.Text = "Укажите страницу или диапазон, например 2-5."; return; }
            PrintQueue queue = Printers.SelectedItem as PrintQueue; if (queue == null) return;
            try
            {
                PrintTicket ticket = (queue.UserPrintTicket ?? queue.DefaultPrintTicket).Clone(); ticket.CopyCount = copies; ticket.PageOrientation = Orientation.SelectedIndex == 1 ? PageOrientation.Landscape : PageOrientation.Portrait;
                PaperChoice paper = Paper.SelectedItem as PaperChoice; if (paper != null) ticket.PageMediaSize = paper.Media;
                if (Duplex.SelectedItem != null) ticket.Duplexing = (Duplexing)((ComboBoxItem)Duplex.SelectedItem).Tag;
                if (Color.SelectedItem != null) ticket.OutputColor = (OutputColor)((ComboBoxItem)Color.SelectedItem).Tag;
                ticket = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, ticket).ValidatedPrintTicket;
                Result = new PrintSelection { Queue = queue, Ticket = ticket, First = first, Last = last }; Window.DialogResult = true;
            }
            catch (Exception error) { note.Text = error.Message; }
        }
        public void Dispose() { foreach (PrintQueue queue in queues) queue.Dispose(); if (server != null) server.Dispose(); }
    }
}
