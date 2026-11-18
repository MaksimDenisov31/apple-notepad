using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

[assembly: AssemblyTitle("Apple Notepad")]
[assembly: AssemblyProduct("Apple Notepad")]
[assembly: AssemblyVersion("1.0.0.0")]
namespace AppleNotepad
{
    internal static class Program
    {
        [STAThread] internal static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--self-test") return Verification.Core();
                Application app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                { Log(e.Exception); MessageBox.Show(e.Exception.Message, "Apple Notepad", MessageBoxButton.OK, MessageBoxImage.Warning); e.Handled = true; };
                bool test = args.Length > 0 && args[0] == "--ui-test";
                if (test) Preferences.OverrideDataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results", "ui-" + Guid.NewGuid().ToString("N"));
                EditorWindow editor = new EditorWindow(test, args.Length > 0 && args[0] == "--new-window");
                if (!test) foreach (string arg in args) if (arg != "--new-window") editor.OpenPath(arg);
                if (test) editor.Window.Loaded += delegate { editor.Window.Dispatcher.BeginInvoke(new Action(delegate { Verification.UI(editor); })); };
                app.Run(editor.Window); return Verification.UIFailed ? 1 : 0;
            }
            catch (Exception error) { Log(error); if (args.Length == 0) MessageBox.Show(error.Message, "Apple Notepad", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
        }
        internal static Window ReadWindow()
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleNotepad.MainWindow.xaml"))
            {
                Window window = (Window)XamlReader.Load(resource);
                using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleNotepad.Icon"))
                { window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(icon, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad); }
                return window;
            }
        }
        internal static void Log(Exception error)
        {
            try { Directory.CreateDirectory(Preferences.DataDirectory); File.AppendAllText(Path.Combine(Preferences.DataDirectory, "error.log"), DateTime.Now.ToString("s") + " " + error + Environment.NewLine); } catch { }
        }
    }
}
