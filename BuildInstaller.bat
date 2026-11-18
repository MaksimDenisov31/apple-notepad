@echo off
setlocal
cd /d "%~dp0"
if not exist AppleNotepad.exe (
  call Build.bat
  if errorlevel 1 exit /b 1
)
set "NOTEPAD_ISCC="
if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" set "NOTEPAD_ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
if not defined NOTEPAD_ISCC if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" set "NOTEPAD_ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
if not defined NOTEPAD_ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "NOTEPAD_ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined NOTEPAD_ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "NOTEPAD_ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not "%~1"=="" set "NOTEPAD_ISCC=%~1"
if not defined NOTEPAD_ISCC (
  echo Install Inno Setup from https://jrsoftware.org/inno-setup/downloads
  echo Or pass the full ISCC.exe path as the first argument.
  exit /b 1
)
"%NOTEPAD_ISCC%" installer\AppleNotepad.iss
if errorlevel 1 exit /b 1
echo Installer is ready in installer-output.
