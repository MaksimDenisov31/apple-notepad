$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& cmd /c Build.bat
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
& cmd /c BuildInstaller.bat
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
Compress-Archive -LiteralPath @('AppleNotepad.exe','AppleNotepad.exe.config','README.md','docs','assets') -DestinationPath 'installer-output\AppleNotepad-Portable-1.1.2.zip' -Force
$lines = foreach ($name in @('AppleNotepad-Setup-1.1.2.exe','AppleNotepad-Portable-1.1.2.zip')) {
 $hash = (Get-FileHash (Join-Path 'installer-output' $name) -Algorithm SHA256).Hash.ToLowerInvariant()
 "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path (Get-Location) 'installer-output\SHA256SUMS.txt'),[string[]]$lines,[Text.Encoding]::ASCII)
$lines
