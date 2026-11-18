param([string]$Compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$testId = [Guid]::NewGuid().ToString('N').Substring(0,12)
$runRoot = Join-Path $repo ('.tools\installer-tests\' + $testId)
$installRoot = Join-Path $runRoot 'Установка с пробелами'
$appName = 'Apple Notepad Test ' + $testId
$appId = 'AppleNotepad.InstallerTest.' + $testId + '_is1'
$registeredName = 'AppleNotepad.InstallerTest.' + $testId
$progId = $registeredName + '.Text'
$capabilitiesPath = 'Software\' + $registeredName + '\Capabilities'
[IO.Directory]::CreateDirectory($runRoot) | Out-Null
function Assert([bool]$value, [string]$message) { if (-not $value) { throw $message } }
function Run([string]$file, [string[]]$arguments) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    Assert ($process.ExitCode -eq 0) ("Exit code $($process.ExitCode): $file")
}
function Registration {
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $view)
        $key = $base.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appId)
        if ($key) {
            $result = @{Name=$key.GetValue('DisplayName');Path=$key.GetValue('InstallLocation')}
            $key.Dispose(); $base.Dispose(); return $result
        }
        $base.Dispose()
    }
    return $null
}
function RegistryValue([string]$path, [string]$name) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
    if (-not $key) { return $null }
    try { return $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) } finally { $key.Dispose() }
}
function HasRegistryValue([string]$path, [string]$name) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($path)
    if (-not $key) { return $false }
    try { return $key.GetValueNames() -contains $name } finally { $key.Dispose() }
}
function AssociationsSnapshot {
    $values = foreach ($extension in @('.txt','.log')) {
        $choice = 'Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\' + $extension + '\UserChoice'
        [ordered]@{ Extension=$extension; ProgId=(RegistryValue $choice 'ProgId'); Hash=(RegistryValue $choice 'Hash'); LegacyDefault=(RegistryValue ('Software\Classes\' + $extension) '') }
    }
    return ConvertTo-Json -InputObject @($values) -Compress
}
function CheckAssociations {
    Assert ((RegistryValue 'Software\RegisteredApplications' $registeredName) -eq $capabilitiesPath) 'Default-app registration is missing.'
    Assert ((RegistryValue $capabilitiesPath 'ApplicationName') -eq $appName) 'Wrong application name in Default apps.'
    $command = '"' + (Join-Path $installRoot 'AppleNotepad.exe') + '" "%1"'
    Assert ((RegistryValue ('Software\Classes\' + $progId + '\shell\open\command') '') -eq $command) 'Open command does not quote paths safely.'
    Assert ((RegistryValue ('Software\Classes\Applications\' + $registeredName + '.exe\shell\open\command') '') -eq $command) 'Open With command is incorrect.'
    Assert ((RegistryValue ('Software\Classes\' + $progId + '\DefaultIcon') '') -eq ('"' + (Join-Path $installRoot 'assets\app.ico') + '",0')) 'Document icon is not registered.'
    foreach ($extension in @('.txt','.log')) {
        Assert ((RegistryValue ($capabilitiesPath + '\FileAssociations') $extension) -eq $progId) ('Missing capability: ' + $extension)
        Assert (HasRegistryValue ('Software\Classes\' + $extension + '\OpenWithProgids') $progId) ('Missing Open With candidate: ' + $extension)
    }
    Assert ((AssociationsSnapshot) -eq $originalAssociations) 'Installer modified protected UserChoice or existing extension defaults.'
}
$originalAssociations = AssociationsSnapshot
# A unique AppId and shortcut name isolate this test from the user's real installation.
& $Compiler /Qp ("/DInstallerTest=" + $testId) ("/O" + $runRoot) (Join-Path $repo 'installer\AppleNotepad.iss')
Assert ($LASTEXITCODE -eq 0) 'Installer test compilation failed.'
$setup = Join-Path $runRoot 'AppleNotepad-Setup-Test.exe'
$setupArguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/SP-','/NORESTART','/NOCLOSEAPPLICATIONS','/LANG=russian',('/DIR="' + $installRoot + '"'),'/TASKS=desktopicon',('/LOG="' + (Join-Path $runRoot 'install.log') + '"'))
$uninstaller = Join-Path $installRoot 'unins000.exe'
$programShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) ($appName + '\Apple Notepad.lnk')
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($appName + '.lnk')
$dataFile = Join-Path $runRoot 'Пользовательский документ.txt'
$insideFile = Join-Path $installRoot 'Мой документ.txt'
[IO.File]::WriteAllText($dataFile, 'Test document must survive setup and removal.')
try {
    Run $setup $setupArguments
    Assert (Test-Path -LiteralPath $uninstaller) 'Missing uninstaller.'
    foreach ($name in @('AppleNotepad.exe','AppleNotepad.exe.config','README.md')) {
        Assert ((Get-FileHash -LiteralPath (Join-Path $repo $name)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $installRoot $name)).Hash) ("Installed bytes differ: $name")
    }
    $registered = Registration
    Assert ($null -ne $registered -and $registered.Name -eq $appName) 'Missing or incorrect Windows uninstall registration.'
    CheckAssociations
    Assert (Test-Path -LiteralPath $programShortcut) 'Missing Start menu shortcut.'
    Assert (Test-Path -LiteralPath $desktopShortcut) 'Missing optional desktop shortcut.'
    $shell = New-Object -ComObject WScript.Shell
    Assert ($shell.CreateShortcut($programShortcut).TargetPath -eq (Join-Path $installRoot 'AppleNotepad.exe')) 'Shortcut points to the wrong executable.'
    [IO.File]::WriteAllText($insideFile, 'User-created file is not owned by the installer.')
    # Reinstall simulates an update using the same AppId and installation directory.
    Run $setup $setupArguments
    CheckAssociations
    Assert ([IO.File]::ReadAllText($insideFile) -eq 'User-created file is not owned by the installer.') 'Update changed a user-created file.'
    foreach ($mode in @('--self-test','--ui-test')) { Run (Join-Path $installRoot 'AppleNotepad.exe') @($mode) }
    Run $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $runRoot 'uninstall.log') + '"'))
    Assert (-not (Test-Path -LiteralPath (Join-Path $installRoot 'AppleNotepad.exe'))) 'Uninstall left the installed executable.'
    Assert ($null -eq (Registration)) 'Uninstall registration remains.'
    Assert (-not (HasRegistryValue 'Software\RegisteredApplications' $registeredName)) 'Default-app registration remains after uninstall.'
    Assert ($null -eq (RegistryValue ('Software\Classes\' + $progId + '\shell\open\command') '')) 'File open command remains after uninstall.'
    Assert ($null -eq (RegistryValue $capabilitiesPath 'ApplicationName')) 'Capabilities remain after uninstall.'
    Assert ($null -eq (RegistryValue ('Software\Classes\Applications\' + $registeredName + '.exe') 'FriendlyAppName')) 'Open With application remains after uninstall.'
    foreach ($extension in @('.txt','.log')) { Assert (-not (HasRegistryValue ('Software\Classes\' + $extension + '\OpenWithProgids') $progId)) ('Open With candidate remains: ' + $extension) }
    Assert ((AssociationsSnapshot) -eq $originalAssociations) 'Uninstaller modified existing defaults.'
    Assert (-not (Test-Path -LiteralPath $programShortcut)) 'Start menu shortcut remains.'
    Assert (-not (Test-Path -LiteralPath $desktopShortcut)) 'Desktop shortcut remains.'
    Assert ([IO.File]::ReadAllText($insideFile) -eq 'User-created file is not owned by the installer.') 'Uninstall deleted a user-created file.'
    Assert ([IO.File]::ReadAllText($dataFile) -eq 'Test document must survive setup and removal.') 'Uninstall deleted an external document.'
    $report = 'PASS: per-user installation without admin rights; Unicode path with spaces; exact file hashes; shortcuts; uninstall entry; .txt/.log Default apps and Open With registration; quoted commands and document icon; reinstall/update preserving user files; installed EXE core/UI tests; uninstall removes only owned registrations/shortcuts; protected UserChoice and existing extension defaults are unchanged; user documents are preserved. Silent setup never opens Default apps.'
    [IO.File]::WriteAllText((Join-Path $repo '.tools\installer-verification.txt'), $report + [Environment]::NewLine)
    $report
} finally {
    # Clean up only this uniquely named test installation; never delete user directories recursively.
    if (Test-Path -LiteralPath $uninstaller) { Run $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') }
}
