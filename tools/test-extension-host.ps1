# Runs editors\vscode\hosttest inside a real VS Code: the language model tools answer, the check
# reads unsaved edits, a new terminal finds `shosa`. DESIGN.md 45.
#
#   tools\test-extension-host.ps1              the extension installed on this PC (what a user has)
#   tools\test-extension-host.ps1 -Repository  the extension in editors\vscode (after npm run build)
#
# A window of VS Code opens for some seconds, with its own empty profile (a temp folder), so the
# VS Code you have open is not touched and none of your other extensions are loaded.
param([switch]$Repository)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if ($Repository) {
    $extension = Join-Path $root 'editors\vscode'
} else {
    $extension = Get-ChildItem (Join-Path $env:USERPROFILE '.vscode\extensions') -Directory -Filter 'yotsuda.shosa-*' |
        Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
    if (-not $extension) { throw 'The Shosa extension is not installed: run tools\deploy-extension.ps1, or pass -Repository.' }
}

$code = (Get-Command code.cmd -ErrorAction Stop).Source
$exe = Join-Path (Split-Path (Split-Path $code -Parent) -Parent) 'Code.exe'
$temp = Join-Path ([IO.Path]::GetTempPath()) "shosa-hosttest-$PID"
$work = Join-Path $temp 'work'
New-Item -ItemType Directory -Force $work | Out-Null
Copy-Item (Join-Path $root 'samples\fighter.shosa') $work   # a copy: the test edits it (and reverts)
Copy-Item (Join-Path $root 'samples\dance.shosa'), (Join-Path $root 'samples\song.lys') $work   # a dance to a song: the MP4 with sound

$env:SHOSA_HOSTTEST_OUT = Join-Path $temp 'report.json'
try {
    $arguments = @(
        "--extensionDevelopmentPath=$extension",
        "--extensionTestsPath=$(Join-Path $root 'editors\vscode\hosttest\index.cjs')",
        "--user-data-dir=$(Join-Path $temp 'profile')",
        '--disable-extensions', '--disable-workspace-trust', '--skip-welcome', '--skip-release-notes',
        $work)
    $process = Start-Process $exe -ArgumentList ($arguments | ForEach-Object { '"' + $_ + '"' }) -PassThru -Wait
    "extension: $extension"
    if (Test-Path $env:SHOSA_HOSTTEST_OUT) {
        $report = Get-Content $env:SHOSA_HOSTTEST_OUT -Raw | ConvertFrom-Json
        "VS Code $($report.vscode)"
        foreach ($check in $report.checks) {
            $sign = if ($null -eq $check.ok) { 'skip' } elseif ($check.ok) { 'ok  ' } else { 'FAIL' }
            "$sign $($check.name)"
            if ($check.ok -ne $true) { "       " + ($check.detail | ConvertTo-Json -Depth 5 -Compress) }
        }
        foreach ($failure in $report.failures | Where-Object { $_ -like 'threw:*' }) { $failure }
    } else {
        'FAIL no report was written (the test did not run)'
    }
    "exit code $($process.ExitCode)"
} finally {
    Remove-Item Env:\SHOSA_HOSTTEST_OUT
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
