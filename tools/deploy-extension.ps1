param(
    [string]$Code,          # the VS Code CLI (code.cmd); found on the PATH or in the user install when not given
    [switch]$NoPack,        # install the win32-x64 .vsix already in artifacts\extension instead of packing it again
    [switch]$SkipPublish,   # pack, but reuse the server of the last run (the extension's code changed, not the server)
    [string]$Out = "$PSScriptRoot\..\artifacts\extension"
)
# Deploys Shosa to THIS PC's VS Code, Windows x64, without closing VS Code: packs the win-x64
# target (pack-extension.ps1: the language server and shosa-music published with their .NET
# runtime, the extension built, `vsce package --target win32-x64`) and installs the .vsix over
# the earlier one. Then one "Developer: Reload Window" in each window loads the new extension.
# Never publishes.
#
# The .vsix is chosen by name — shosa-win32-x64-<version>.vsix, the version from
# editors\vscode\package.json — never "the newest file".
#
# A VS Code that has a .shosa file open runs the language server from the installed extension,
# and Windows will not replace a program that runs. So when the install is refused, the server
# (and a shosa-music it started) is stopped and the install tried again. The window it belonged
# to then says the server stopped; Reload Window starts the new one.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."

if (-not $NoPack) {
    & "$PSScriptRoot\pack-extension.ps1" -Out $Out -Targets win-x64 -SkipPublish:$SkipPublish
}
$version = (Get-Content "$root\editors\vscode\package.json" -Raw | ConvertFrom-Json).version
$vsix = Get-Item (Join-Path $Out "shosa-win32-x64-$version.vsix") -ErrorAction SilentlyContinue
if (-not $vsix) { throw "no shosa-win32-x64-$version.vsix in ${Out}: run without -NoPack" }

if (-not $Code) {
    $Code = (Get-Command code.cmd -ErrorAction SilentlyContinue).Source
    if (-not $Code) { $Code = "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" }
}
if (-not (Test-Path $Code)) { throw "no VS Code CLI at ${Code}: pass -Code <path to code.cmd>" }

$extensions = "$env:USERPROFILE\.vscode\extensions"
# The programs a running VS Code started from the installed extension. (A VS Code started with
# --extensionDevelopmentPath runs the repository's own build, which is not touched here.)
function Stop-InstalledServer {
    $running = @(Get-Process Shosa.Lsp, shosa-music -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith("$extensions\yotsuda.shosa", [StringComparison]::OrdinalIgnoreCase) })
    foreach ($process in $running) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    return $running | ForEach-Object { "$($_.ProcessName)($($_.Id))" }
}

# The CLI writes a Node DeprecationWarning to stderr on every run, which Windows PowerShell turns
# into an error under 'Stop': it runs through cmd with stderr merged, and its exit code is the verdict.
function Install-Vsix {
    $said = cmd /d /s /c "`"$Code`" --install-extension `"$($vsix.FullName)`" --force < NUL 2>&1" | Where-Object { $_ -notmatch 'DeprecationWarning|--trace-deprecation' }
    return [pscustomobject]@{ Ok = $LASTEXITCODE -eq 0 -and ($said -match 'successfully installed'); Said = $said }
}

"installing {0} with {1}" -f $vsix.Name, $Code
$result = Install-Vsix
for ($round = 0; -not $result.Ok -and $round -lt 6; $round++) {
    # Refused: most likely the old server runs. The editor starts it again a few times when it
    # is stopped, so it is stopped until the install gets through.
    $stopped = Stop-InstalledServer
    if ($stopped) { "stopped the running server: $($stopped -join ', ')" } elseif ($round -gt 0) { break }
    Start-Sleep -Milliseconds 700
    $result = Install-Vsix
}
$result.Said | ForEach-Object { "  $_" }
if (-not $result.Ok) { throw 'code --install-extension failed' }

# What landed: the folder, its platform, its programs.
$dir = Get-ChildItem $extensions -Directory -Filter "yotsuda.shosa-$version*" | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $dir) { throw "no yotsuda.shosa-$version folder under $extensions after the install" }
$manifest = Get-Content (Join-Path $dir.FullName '.vsixmanifest') -Raw
$platform = if ($manifest -match 'TargetPlatform="([^"]+)"') { $Matches[1] } else { '(none)' }
$server = Join-Path $dir.FullName 'server\Shosa.Lsp.exe'
$music = Join-Path $dir.FullName 'server\shosa-music.exe'
if ($platform -ne 'win32-x64' -or -not (Test-Path $server) -or -not (Test-Path $music)) { throw "the installed extension is $($dir.Name) for ${platform}: not the Windows package, or without its server" }
# The installed files are the packed ones: the same program, byte for byte.
$packed = "$Out\stage\server\Shosa.Lsp.exe"
if ((Test-Path $packed) -and (Get-FileHash $packed).Hash -ne (Get-FileHash $server).Hash) { throw "$server is not the server that was just packed: the install did not replace it" }
"installed {0} ({1}), server {2:yyyy-MM-dd HH:mm}" -f $dir.Name, $platform, (Get-Item (Join-Path $dir.FullName 'server\Shosa.Lsp.dll')).LastWriteTime
cmd /d /s /c "`"$Code`" --list-extensions --show-versions < NUL 2>&1" | Select-String '^yotsuda\.shosa' | ForEach-Object { "  $_" }
'done: in each VS Code window, Developer: Reload Window loads the new extension (VS Code itself need not be closed)'
