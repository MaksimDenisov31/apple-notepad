using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AppleNotepad
{
    internal sealed class Printing
    {
        private double left = 20, right = 20, top = 20, bottom = 20;
        private bool landscape;
        private string header = "&f", footer = "Страница &p";
        internal void Setup(Window owner)
        {
            Window dialog = Dialogs.Create(owner, "Параметры страницы", 440, 535); StackPanel panel = Dialogs.Body(dialog);
            panel.Children.Add(new TextBlock { Text = "Поля в миллиметрах", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 15) });
            string[] names = { "Слева", "Справа", "Сверху", "Снизу" }; double[] values = { left, right, top, bottom }; TextBox[] inputs = new TextBox[4];
            for (int i = 0; i < names.Length; i++)
            {
                DockPanel row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) }; row.Children.Add(new TextBlock { Text = names[i], Width = 100, VerticalAlignment = VerticalAlignment.Center });
                inputs[i] = new TextBox { Text = values[i].ToString(CultureInfo.CurrentCulture), Padding = new Thickness(5) }; row.Children.Add(inputs[i]); panel.Children.Add(row);
                MenuStyling.Attach(dialog, inputs[i]);
            }
            CheckBox orientation = new CheckBox { Content = "Альбомная ориентация", IsChecked = landscape, Margin = new Thickness(0, 10, 0, 15) }; panel.Children.Add(orientation);
            panel.Children.Add(new TextBlock { Text = "Верхний колонтитул" }); TextBox head = new TextBox { Text = header, Padding = new Thickness(6), Margin = new Thickness(0, 5, 0, 10) }; panel.Children.Add(head);
            panel.Children.Add(new TextBlock { Text = "Нижний колонтитул" }); TextBox foot = new TextBox { Text = footer, Padding = new Thickness(6), Margin = new Thickness(0, 5, 0, 10) }; panel.Children.Add(foot);
            MenuStyling.Attach(dialog, head); MenuStyling.Attach(dialog, foot);
            panel.Children.Add(new TextBlock { Text = "&f — файл · &p — страница · &d — дата · &t — время\n&l / &c / &r — слева / по центру / справа", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 15) });
            Button ok = new Button { Content = "Сохранить", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(18, 7, 18, 7) }; panel.Children.Add(ok);
            ok.Click += delegate
            {
                double[] parsed = new double[4];
                for (int i = 0; i < 4; i++) if (!Double.TryParse(inputs[i].Text, out parsed[i]) || parsed[i] < 8 || parsed[i] > 65) { MessageBox.Show(dialog, "Поля должны быть от 8 до 65 мм."); return; }
                left = parsed[0]; right = parsed[1]; top = parsed[2]; bottom = parsed[3]; landscape = orientation.IsChecked == true; header = head.Text; footer = foot.Text; dialog.Close();
            };
            dialog.ShowDialog();
        }
        internal void Print(Window owner, TextFile file, Preferences settings)
        {
            PrintDialog dialog = new PrintDialog { UserPageRangeEnabled = true };
            if (dialog.PrintTicket != null) dialog.PrintTicket.PageOrientation = landscape ? System.Printing.PageOrientation.Landscape : System.Printing.PageOrientation.Portrait;
            if (dialog.ShowDialog() != true) return;
            double width = dialog.PrintableAreaWidth, height = dialog.PrintableAreaHeight;
            FlowDocument document = CreateDocument(file.Text, settings.Font, settings.FontSize, new Size(width, height), new Thickness(left * 96 / 25.4, top * 96 / 25.4, right * 96 / 25.4, bottom * 96 / 25.4));
            document.FontWeight = settings.Bold ? FontWeights.Bold : FontWeights.Normal; document.FontStyle = settings.Italic ? FontStyles.Italic : FontStyles.Normal;
            DocumentPaginator source = ((IDocumentPaginatorSource)document).DocumentPaginator; source.ComputePageCount();
            int first = dialog.PageRangeSelection == PageRangeSelection.UserPages ? Math.Max(0, dialog.PageRange.PageFrom - 1) : 0;
            int last = dialog.PageRangeSelection == PageRangeSelection.UserPages ? Math.Min(source.PageCount - 1, dialog.PageRange.PageTo - 1) : source.PageCount - 1;
            if (first > last) { MessageBox.Show(owner, "В документе нет страниц выбранного диапазона."); return; }
            dialog.PrintDocument(new DecoratedPaginator(source, first, last, String.IsNullOrEmpty(file.Path) ? "Без названия" : Path.GetFileName(file.Path), header, footer), "Apple Notepad — " + Path.GetFileName(file.Path));
        }
        internal static FlowDocument CreateDocument(string text, string font, double fontSize, Size size, Thickness margins)
        {
            if (size.Width - margins.Left - margins.Right < 30 || size.Height - margins.Top - margins.Bottom < 30) throw new ArgumentException("Поля слишком велики для выбранной бумаги.");
            FlowDocument document = new FlowDocument(new Paragraph(new Run(text)) { Margin = new Thickness(0) }) { FontFamily = new FontFamily(font), FontSize = fontSize, PageWidth = size.Width, PageHeight = size.Height, PagePadding = margins, ColumnWidth = Double.PositiveInfinity };
            return document;
        }
    }
    internal sealed class DecoratedPaginator : DocumentPaginator
    {
        private readonly DocumentPaginator inner;
        private readonly int first, last;
        private readonly string filename, header, footer;
        internal DecoratedPaginator(DocumentPaginator inner, int first, int last, string filename, string header, string footer) { this.inner = inner; this.first = first; this.last = last; this.filename = filename; this.header = header; this.footer = footer; }
        public override DocumentPage GetPage(int pageNumber)
        {
            if (pageNumber < 0 || pageNumber >= PageCount) return DocumentPage.Missing;
            DocumentPage original = inner.GetPage(pageNumber + first);
            ContainerVisual visual = new ContainerVisual(); DrawingVisual decorations = new DrawingVisual();
            using (DrawingContext context = decorations.RenderOpen())
            {
                Draw(context, header, pageNumber + first + 1, 12, original.Size.Width);
                Draw(context, footer, pageNumber + first + 1, original.Size.Height - 27, original.Size.Width);
            }
            // Use a brush instead of reparenting the original paginator visual.
            DrawingVisual content = new DrawingVisual(); using (DrawingContext dc = content.RenderOpen()) dc.DrawRectangle(new VisualBrush(original.Visual) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(original.Size), ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(original.Size) }, null, new Rect(original.Size));
            visual.Children.Add(content); visual.Children.Add(decorations); return new DocumentPage(visual, original.Size, original.BleedBox, original.ContentBox);
        }
        private void Draw(DrawingContext dc, string format, int page, double y, double width)
        {
            StringBuilder[] parts = { new StringBuilder(), new StringBuilder(), new StringBuilder() }; int align = 1;
            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '&' || i + 1 >= format.Length) { parts[align].Append(format[i]); continue; }
                char token = Char.ToLowerInvariant(format[++i]);
                switch (token)
                {
                    case 'l': align = 0; break; case 'c': align = 1; break; case 'r': align = 2; break;
                    case 'f': parts[align].Append(filename); break; case 'p': parts[align].Append(page); break;
                    case 'd': parts[align].Append(DateTime.Now.ToShortDateString()); break; case 't': parts[align].Append(DateTime.Now.ToShortTimeString()); break;
                    case '&': parts[align].Append('&'); break; default: parts[align].Append('&').Append(token); break;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                FormattedText text = new FormattedText(parts[i].ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, Brushes.Black, 1.0);
                double x = i == 0 ? 30 : i == 1 ? (width - text.Width) / 2 : width - 30 - text.Width; dc.DrawText(text, new Point(Math.Max(0, x), y));
            }
        }
        public override bool IsPageCountValid { get { return inner.IsPageCountValid; } }
        public override int PageCount { get { return Math.Max(0, last - first + 1); } }
        public override Size PageSize { get { return inner.PageSize; } set { inner.PageSize = value; } }
        public override IDocumentPaginatorSource Source { get { return inner.Source; } }
    }
}
