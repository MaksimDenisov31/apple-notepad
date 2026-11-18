using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AppleNotepad
{
    internal static class Markdown
    {
        internal static FlowDocument Create(string text, bool dark)
        {
            FlowDocument document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 16, PagePadding = new Thickness(36, 26, 36, 36), Foreground = dark ? Brushes.WhiteSmoke : new SolidColorBrush(Color.FromRgb(37, 38, 43)), Background = dark ? new SolidColorBrush(Color.FromRgb(41, 43, 49)) : Brushes.White };
            bool code = false; Paragraph codeBlock = null;
            foreach (string raw in TextFile.Normalize(text).Split('\n'))
            {
                if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code = !code;
                    if (code) { codeBlock = new Paragraph { FontFamily = new FontFamily("Consolas"), FontSize = 14, Padding = new Thickness(14), Background = dark ? new SolidColorBrush(Color.FromRgb(32, 33, 38)) : new SolidColorBrush(Color.FromRgb(245, 245, 247)), Margin = new Thickness(0, 10, 0, 10) }; document.Blocks.Add(codeBlock); }
                    continue;
                }
                if (code) { codeBlock.Inlines.Add(new Run(raw)); codeBlock.Inlines.Add(new LineBreak()); continue; }
                string line = raw; Paragraph p = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 25 };
                Match heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (heading.Success) { int level = heading.Groups[1].Length; p.FontSize = 30 - level * 2; p.FontWeight = FontWeights.SemiBold; p.Margin = new Thickness(0, 16, 0, 10); line = heading.Groups[2].Value; }
                else if (Regex.IsMatch(line, @"^\s*[-*+]\s+")) line = "•  " + Regex.Replace(line, @"^\s*[-*+]\s+", "");
                else if (line.StartsWith("> ")) { line = line.Substring(2); p.BorderBrush = Brushes.LightGray; p.BorderThickness = new Thickness(3, 0, 0, 0); p.Padding = new Thickness(14, 4, 0, 4); p.FontStyle = FontStyles.Italic; }
                InlineText(p, line); if (line.Length == 0) p.Inlines.Add(new Run(" ")); document.Blocks.Add(p);
            }
            return document;
        }
        private static void InlineText(Paragraph p, string text)
        {
            int last = 0;
            foreach (Match match in Regex.Matches(text, @"\*\*(.+?)\*\*|\*(.+?)\*|`([^`]+)`|\[([^\]]+)\]\(([^)]+)\)"))
            {
                if (match.Index > last) p.Inlines.Add(new Run(text.Substring(last, match.Index - last)));
                if (match.Groups[1].Success) p.Inlines.Add(new Bold(new Run(match.Groups[1].Value)));
                else if (match.Groups[2].Success) p.Inlines.Add(new Italic(new Run(match.Groups[2].Value)));
                else if (match.Groups[3].Success) p.Inlines.Add(new Run(match.Groups[3].Value) { FontFamily = new FontFamily("Consolas"), Background = new SolidColorBrush(Color.FromArgb(40, 127, 127, 127)) });
                else
                {
                    Uri url;
                    if (Uri.TryCreate(match.Groups[5].Value, UriKind.Absolute, out url) && (url.Scheme == "http" || url.Scheme == "https"))
                    {
                        Hyperlink link = new Hyperlink(new Run(match.Groups[4].Value)) { NavigateUri = url, Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)) };
                        link.RequestNavigate += delegate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
                        { try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception error) { Program.Log(error); } e.Handled = true; };
                        p.Inlines.Add(link);
                    }
                    else p.Inlines.Add(new Run(match.Groups[4].Value));
                }
                last = match.Index + match.Length;
            }
            if (last < text.Length) p.Inlines.Add(new Run(text.Substring(last)));
        }
    }
}
