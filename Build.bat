@echo off
setlocal
cd /d "%~dp0"
set "FX=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FX%\WPF"
"%FX%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:app.manifest /win32icon:assets\app.ico /out:AppleNotepad.exe /resource:MainWindow.xaml,AppleNotepad.MainWindow.xaml /resource:ControlStyles.xaml,AppleNotepad.ControlStyles.xaml /resource:assets\app.ico,AppleNotepad.Icon /r:System.dll /r:System.Core.dll /r:System.Xaml.dll /r:System.Runtime.Serialization.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\ReachFramework.dll" /r:"%WPF%\System.Printing.dll" src\*.cs
if errorlevel 1 exit /b 1
echo Built AppleNotepad.exe
