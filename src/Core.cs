using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AppleNotepad
{
    [DataContract] internal sealed class Preferences
    {
        [DataMember] public bool Wrap = true;
        [DataMember] public bool LineNumbers = false;
        [DataMember] public bool SpellCheck = true;
        [DataMember] public bool RestoreSession = true;
        [DataMember] public bool StatusBar = true;
        [DataMember] public string Theme = "light";
        [DataMember] public string Font = "Cascadia Mono";
        [DataMember] public double FontSize = 15;
        [DataMember] public bool Bold, Italic;
        [DataMember] public int Zoom = 100;
        [DataMember] public double Width = 1120, Height = 760;
        [DataMember] public List<string> Recent = new List<string>();
        internal static string OverrideDataDirectory;
        internal static string DataDirectory { get { return OverrideDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppleNotepad"); } }
        internal static Preferences Load()
        {
            Preferences value = Storage.Read<Preferences>(Path.Combine(DataDirectory, "settings.json")) ?? new Preferences();
            if (value.Recent == null) value.Recent = new List<string>();
            if (String.IsNullOrWhiteSpace(value.Font)) value.Font = "Consolas";
            if (value.FontSize < 8 || value.FontSize > 96) value.FontSize = 15;
            if (value.Zoom < 20 || value.Zoom > 500) value.Zoom = 100;
            return value;
        }
        internal void Save() { Storage.Write(Path.Combine(DataDirectory, "settings.json"), this); }
    }
    [DataContract] internal sealed class Session
    {
        [DataMember] public List<SessionTab> Tabs = new List<SessionTab>();
        [DataMember] public int Selected;
    }
    [DataContract] internal sealed class SessionTab
    {
        [DataMember] public string Path, Text, SavedText, Encoding, Newline;
        [DataMember] public int Caret;
        [DataMember] public long DiskTicks, DiskLength;
        [DataMember] public bool MetadataDirty;
    }
    internal static class Storage
    {
        internal static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { using (FileStream stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream); }
            catch (Exception error)
            {
                if (!(error is IOException) && !(error is SerializationException) && !(error is UnauthorizedAccessException)) throw;
                return null;
            }
        }
        internal static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (MemoryStream stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); AtomicWrite(path, stream.ToArray());
            }
        }
        internal static void AtomicWrite(string path, byte[] bytes)
        {
            string full = System.IO.Path.GetFullPath(path);
            string temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full), "." + System.IO.Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(full)) File.Replace(temporary, full, null); else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    internal sealed class SessionVault : IDisposable
    {
        private readonly string path;
        private readonly FileStream lease;
        private readonly List<KeyValuePair<string, FileStream>> recovered = new List<KeyValuePair<string, FileStream>>();
        internal SessionVault()
        {
            string root = System.IO.Path.Combine(Preferences.DataDirectory, "sessions"); Directory.CreateDirectory(root);
            path = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N") + ".json");
            lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        internal List<Session> Recover()
        {
            List<Session> sessions = new List<Session>();
            foreach (string candidate in Directory.GetFiles(System.IO.Path.GetDirectoryName(path), "*.json"))
            {
                FileStream guard;
                try { guard = new FileStream(candidate + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { continue; }
                Session session = Storage.Read<Session>(candidate);
                if (session == null) { guard.Dispose(); continue; }
                sessions.Add(session); recovered.Add(new KeyValuePair<string, FileStream>(candidate, guard));
            }
            return sessions;
        }
        internal void Save(Session session)
        {
            Storage.Write(path, session);
            foreach (KeyValuePair<string, FileStream> old in recovered)
            {
                File.Delete(old.Key); old.Value.Dispose(); File.Delete(old.Key + ".lock");
            }
            recovered.Clear();
        }
        internal void Forget() { if (File.Exists(path)) File.Delete(path); }
        public void Dispose()
        {
            foreach (KeyValuePair<string, FileStream> old in recovered) old.Value.Dispose();
            recovered.Clear(); lease.Dispose(); if (File.Exists(path + ".lock")) File.Delete(path + ".lock");
        }
    }
    internal sealed class TextFile
    {
        internal string Path = "", Text = "", SavedText = "", EncodingName = "utf8", Newline = "CRLF";
        internal long DiskTicks, DiskLength;
        internal bool MetadataDirty;
        internal bool Dirty { get { return MetadataDirty || Text != SavedText; } }
        internal static string Normalize(string text) { return text.Replace("\r\n", "\n").Replace('\r', '\n'); }
        internal static Encoding EncodingFor(string name)
        {
            switch (name)
            {
                case "utf8bom": return new UTF8Encoding(true, true);
                case "utf16": return new UnicodeEncoding(false, true, true);
                case "utf16be": return new UnicodeEncoding(true, true, true);
                case "utf32": return new UTF32Encoding(false, true, true);
                case "utf32be": return new UTF32Encoding(true, true, true);
                case "ansi": return System.Text.Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                default: return new UTF8Encoding(false, true);
            }
        }
        internal static TextFile Open(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string name = "utf8"; int skip = 0;
            if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 254 && bytes[2] == 0 && bytes[3] == 0) { name = "utf32"; skip = 4; }
            else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 254 && bytes[3] == 255) { name = "utf32be"; skip = 4; }
            else if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191) { name = "utf8bom"; skip = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254) { name = "utf16"; skip = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255) { name = "utf16be"; skip = 2; }
            string raw;
            try { raw = EncodingFor(name).GetString(bytes, skip, bytes.Length - skip); }
            catch (DecoderFallbackException) { if (skip != 0) throw; name = "ansi"; raw = EncodingFor(name).GetString(bytes); }
            string newline = raw.Contains("\r\n") ? "CRLF" : raw.Contains("\n") ? "LF" : raw.Contains("\r") ? "CR" : "CRLF";
            TextFile file = new TextFile { Path = System.IO.Path.GetFullPath(path), EncodingName = name, Newline = newline, Text = Normalize(raw) };
            file.SavedText = file.Text; file.RememberDisk(); return file;
        }
        internal void Save(string path)
        {
            Encoding encoding = EncodingFor(EncodingName);
            string content = Normalize(Text).Replace("\n", Newline == "LF" ? "\n" : Newline == "CR" ? "\r" : "\r\n");
            byte[] body = encoding.GetBytes(content), bom = encoding.GetPreamble(), result = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, result, 0, bom.Length); Buffer.BlockCopy(body, 0, result, bom.Length, body.Length);
            Storage.AtomicWrite(path, result); Path = System.IO.Path.GetFullPath(path); Text = Normalize(Text); SavedText = Text; MetadataDirty = false; RememberDisk();
        }
        internal void RememberDisk()
        {
            if (String.IsNullOrEmpty(Path) || !File.Exists(Path)) return;
            FileInfo info = new FileInfo(Path); DiskTicks = info.LastWriteTimeUtc.Ticks; DiskLength = info.Length;
        }
        internal bool ChangedOnDisk()
        {
            if (String.IsNullOrEmpty(Path)) return false;
            if (!File.Exists(Path)) return true;
            FileInfo info = new FileInfo(Path); return DiskTicks != info.LastWriteTimeUtc.Ticks || DiskLength != info.Length;
        }
    }
    internal static class Search
    {
        private static bool Word(char c) { return Char.IsLetterOrDigit(c) || c == '_'; }
        internal static bool At(string text, int index, string term, bool matchCase, bool wholeWord)
        {
            if (index < 0 || index + term.Length > text.Length || term.Length == 0) return false;
            if (String.Compare(text, index, term, 0, term.Length, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) != 0) return false;
            return !wholeWord || ((index == 0 || !Word(text[index - 1])) && (index + term.Length == text.Length || !Word(text[index + term.Length])));
        }
        internal static int Find(string text, string term, int start, bool reverse, bool matchCase, bool wholeWord, bool wrap)
        {
            if (String.IsNullOrEmpty(term) || term.Length > text.Length) return -1;
            int last = text.Length - term.Length;
            if (reverse)
            {
                for (int i = Math.Min(start, last); i >= 0; i--) if (At(text, i, term, matchCase, wholeWord)) return i;
                if (wrap) for (int i = last; i > start; i--) if (At(text, i, term, matchCase, wholeWord)) return i;
            }
            else
            {
                for (int i = Math.Max(0, start); i <= last; i++) if (At(text, i, term, matchCase, wholeWord)) return i;
                if (wrap) for (int i = 0; i < Math.Min(start, last + 1); i++) if (At(text, i, term, matchCase, wholeWord)) return i;
            }
            return -1;
        }
        internal static string ReplaceAll(string text, string term, string replacement, bool matchCase, bool wholeWord, out int count)
        {
            count = 0; if (String.IsNullOrEmpty(term)) return text;
            StringBuilder output = new StringBuilder(); int consumed = 0;
            for (int i = 0; i <= text.Length - term.Length; i++)
            {
                if (!At(text, i, term, matchCase, wholeWord)) continue;
                output.Append(text, consumed, i - consumed).Append(replacement); count++; i += term.Length - 1; consumed = i + 1;
            }
            return output.Append(text, consumed, text.Length - consumed).ToString();
        }
    }
}
