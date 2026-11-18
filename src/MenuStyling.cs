using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppleNotepad
{
    internal static class MenuStyling
    {
        internal static ContextMenu Attach(Window owner, TextBox editor, Action find = null)
        {
            ContextMenu menu = new ContextMenu { Resources = owner.Resources, Style = (Style)owner.FindResource(typeof(ContextMenu)) };
            MenuItem undo = Command("Отменить", "Ctrl+Z", ApplicationCommands.Undo, editor);
            MenuItem redo = Command("Повторить", "Ctrl+Y", ApplicationCommands.Redo, editor);
            MenuItem cut = Command("Вырезать", "Ctrl+X", ApplicationCommands.Cut, editor);
            MenuItem copy = Command("Копировать", "Ctrl+C", ApplicationCommands.Copy, editor);
            MenuItem paste = Command("Вставить", "Ctrl+V", ApplicationCommands.Paste, editor);
            MenuItem delete = new MenuItem { Header = "Удалить", InputGestureText = "Del" };
            delete.Click += delegate { editor.SelectedText = ""; };
            MenuItem all = Command("Выделить всё", "Ctrl+A", ApplicationCommands.SelectAll, editor);
            object[] standard = { undo, redo, new Separator(), cut, copy, paste, delete, new Separator(), all };
            foreach (object item in standard) menu.Items.Add(item);
            if (find != null)
            {
                MenuItem search = new MenuItem { Header = "Найти…", InputGestureText = "Ctrl+F" }; search.Click += delegate { find(); };
                menu.Items.Add(new Separator()); menu.Items.Add(search);
            }
            List<object> spellingItems = new List<object>();
            int spellingIndex = -1;
            editor.ContextMenuOpening += delegate(object sender, ContextMenuEventArgs e)
            {
                spellingIndex = e.CursorLeft < 0 ? editor.CaretIndex : editor.GetCharacterIndexFromPoint(new Point(e.CursorLeft, e.CursorTop), true);
            };
            menu.Opened += delegate
            {
                foreach (object old in spellingItems) menu.Items.Remove(old); spellingItems.Clear();
                delete.IsEnabled = editor.SelectionLength > 0 && !editor.IsReadOnly;
                int position = Math.Max(0, Math.Min(editor.Text.Length, spellingIndex < 0 ? editor.CaretIndex : spellingIndex));
                SpellingError error = SpellCheck.GetIsEnabled(editor) ? editor.GetSpellingError(position) : null;
                if (error != null)
                {
                    int index = 0;
                    foreach (string suggestion in error.Suggestions)
                    {
                        if (index >= 5) break;
                        string word = suggestion; MenuItem item = new MenuItem { Header = word.Replace("_", "__"), FontWeight = FontWeights.SemiBold };
                        item.Click += delegate { error.Correct(word); }; menu.Items.Insert(index++, item); spellingItems.Add(item);
                    }
                    MenuItem ignore = new MenuItem { Header = "Пропустить это слово" }; ignore.Click += delegate { error.IgnoreAll(); };
                    menu.Items.Insert(index++, ignore); spellingItems.Add(ignore); Separator separator = new Separator(); menu.Items.Insert(index, separator); spellingItems.Add(separator);
                }
                CommandManager.InvalidateRequerySuggested();
            };
            editor.ContextMenu = menu;
            editor.IsInactiveSelectionHighlightEnabled = true;
            return menu;
        }
        private static MenuItem Command(string header, string shortcut, ICommand command, TextBox editor)
        {
            return new MenuItem { Header = header, InputGestureText = shortcut, Command = command, CommandTarget = editor };
        }
    }
}
