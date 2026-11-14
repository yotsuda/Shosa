param(
    [string]$Out = "$PSScriptRoot\..\artifacts\extension",   # where the .vsix files go
    [string[]]$Targets = @('win-x64'),                       # the programs to publish, one VSIX each (linux-x64, osx-arm64 … are untried)
    [switch]$SkipPublish                                     # reuse the programs of the last run (the extension's code changed, not the server; one target)
)
# Shosa as a VS Code extension (editors\vscode), one .vsix per target:
# shosa-<platform>-<version>.vsix in artifacts\extension.
#
# A package carries the language server (Shosa.Lsp) and shosa-music as programs with their own
# .NET runtime, published into one `server` folder where they share it and where the server
# finds shosa-music beside itself. So whoever installs it needs nothing but VS Code, and a
# package is specific to a platform. shosa-music needs the checkout of Lily# beside this
# repository (DESIGN.md 19.1).
#
# The package is put together in a stage folder ($Out\stage) and packed from there, so nothing
# is written into editors\vscode but `dist`: a VS Code started with --extensionDevelopmentPath
# keeps running the server `dotnet build` leaves, and never holds a file this script replaces.
# Packages only; it installs nothing (deploy-extension.ps1) and publishes nothing.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$ext = "$root\editors\vscode"
$vsceTarget = @{ 'win-x64' = 'win32-x64'; 'win-arm64' = 'win32-arm64'; 'linux-x64' = 'linux-x64'; 'linux-arm64' = 'linux-arm64'; 'osx-x64' = 'darwin-x64'; 'osx-arm64' = 'darwin-arm64' }
if ($SkipPublish -and $Targets.Count -ne 1) { throw '-SkipPublish reuses the programs of ONE target' }

$version = (Get-Content "$ext\package.json" -Raw | ConvertFrom-Json).version
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$stage = "$Out\stage"

Push-Location $ext
try {
    if (-not (Test-Path "$ext\node_modules")) {
        cmd /d /s /c "npm install < NUL"
        if ($LASTEXITCODE -ne 0) { throw 'npm install failed' }
    }
    cmd /d /s /c "npm run check < NUL"
    if ($LASTEXITCODE -ne 0) { throw 'the type check failed' }
    cmd /d /s /c "npm run build < NUL"
    if ($LASTEXITCODE -ne 0) { throw 'the build failed' }
} finally { Pop-Location }

foreach ($rid in $Targets) {
    if (-not $vsceTarget.ContainsKey($rid)) { throw "no VS Code platform for $rid (known: $($vsceTarget.Keys -join ', '))" }
    $server = "$stage\server"

    # Everything of the stage but the programs is made anew each time.
    if (Test-Path $stage) { Get-ChildItem $stage -Force | Where-Object { $SkipPublish -eq $false -or $_.Name -ne 'server' } | Remove-Item -Recurse -Force }
    New-Item -ItemType Directory -Force $stage | Out-Null
    foreach ($item in 'package.json', 'README.md', 'icon.png', 'language-configuration.json', 'syntaxes') { Copy-Item "$ext\$item" $stage -Recurse }
    New-Item -ItemType Directory -Force "$stage\dist" | Out-Null
    Copy-Item "$ext\dist\*" "$stage\dist" -Exclude *.map
    # Everything in the stage ships: an ignore file that leaves out only itself says so to vsce.
    [IO.File]::WriteAllText("$stage\.vscodeignore", ".vscodeignore`n")

    if (-not $SkipPublish) {
        foreach ($project in 'Shosa.Lsp', 'Shosa.Music') {
            cmd /d /s /c "dotnet publish `"$root\$project`" -c Release -r $rid --self-contained true -o `"$server`" -v q -nologo < NUL"
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project ($rid) failed" }
        }
        Get-ChildItem $server -Recurse -Filter *.pdb | Remove-Item
    }
    $suffix = if ($rid.StartsWith('win')) { '.exe' } else { '' }
    foreach ($program in "Shosa.Lsp$suffix", "shosa-music$suffix") { if (-not (Test-Path "$server\$program")) { throw "$server\$program is missing$(if ($SkipPublish) { ': run without -SkipPublish' })" } }
    '{0}: server {1:F1} MB' -f $rid, ((Get-ChildItem $server -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)

    # --no-dependencies: what the extension runs is already bundled into dist. No LICENSE yet: the
    # licence of Shosa is not decided (ROADMAP.md, stage 4), nor is its repository public.
    $vsix = "$Out\shosa-$($vsceTarget[$rid])-$version.vsix"
    Push-Location $stage
    try {
        cmd /d /s /c "npx --yes @vscode/vsce package --target $($vsceTarget[$rid]) --no-dependencies --skip-license --allow-missing-repository --out `"$vsix`" < NUL"
        if ($LASTEXITCODE -ne 0) { throw "vsce package $rid failed" }
    } finally { Pop-Location }
}
Get-ChildItem $Out -Filter "shosa-*-$version.vsix" | ForEach-Object { '{0} {1:F1} MB' -f $_.Name, ($_.Length / 1MB) }
