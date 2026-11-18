@echo off
cd /d "%~dp0"
if not exist AppleNotepad.exe (
  call Build.bat
  if errorlevel 1 exit /b 1
)
start "" "%~dp0AppleNotepad.exe" %*
