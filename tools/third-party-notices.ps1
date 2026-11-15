param(
    [Parameter(Mandatory)][string]$Server,   # the stage's server folder: the published programs and their *.deps.json
    [Parameter(Mandatory)][string]$Out,      # the THIRD-PARTY-NOTICES.txt to write
    [string]$Extension = "$PSScriptRoot\..\editors\vscode"   # dist\bundled.packages.json is written by its `npm run build`
)
# The notices of the third-party software a Shosa extension package carries (DESIGN.md 87.7), for
# tools\pack-extension.ps1: the npm packages bundled into dist (the list build.mjs writes from
# esbuild's metafile), and the .NET runtime and the NuGet packages the programs in `server` run
# with (their *.deps.json). Each with its licence text: the package's own file where it has one,
# otherwise the standard text of the licence it names, with its copyright line.
$ErrorActionPreference = 'Stop'
$Extension = (Resolve-Path $Extension).Path
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { "$env:USERPROFILE\.nuget\packages" }
$rule = '=' * 78
$text = [System.Collections.Generic.List[string]]::new()
function Add([string]$s = '') { $text.Add($s) }
function Read-Text([string]$path) { ([IO.File]::ReadAllText($path) -replace "`r`n", "`n").TrimEnd() }
function License-Files([string]$dir) {
    Get-ChildItem $dir -File | Where-Object Name -match '^(licen[cs]e|notice|copying|third-party-notices)' | Sort-Object Name
}

# The standard texts, for a package that names its licence but carries no file of it.
$apache = Read-Text "$Extension\node_modules\spessasynth_core\LICENSE"
$standard = @{
    'MIT'          = @"
{0}

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES
OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
"@
    'BSD-2-Clause' = @"
{0}
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions
   and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of
   conditions and the following disclaimer in the documentation and/or other materials provided
   with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
"@
    'Apache-2.0'   = "{0}`n`n$apache"
}

function Add-Entry([string]$title, [string]$license, [string]$url, [object[]]$files, [string]$copyright) {
    Add $rule
    Add $title
    Add "License: $license"
    if ($url) { Add "Source: $url" }
    Add $rule
    Add
    if ($files) {
        foreach ($f in $files) {
            if ($files.Count -gt 1) { Add "--- $($f.Name) ---"; Add }
            Add (Read-Text $f.FullName)
            Add
        }
    } elseif ($standard.ContainsKey($license)) {
        if (-not $copyright) { throw "$title names $license but carries no licence file and no copyright line" }
        if ($copyright -notmatch 'copyright') { $copyright = "Copyright (c) $copyright" }
        Add $standard[$license].Replace('{0}', $copyright).TrimEnd()
        Add
    } else {
        throw "$title ($license): no licence file, and no standard text for it here"
    }
}

Add 'THIRD-PARTY NOTICES'
Add
Add 'The Shosa extension carries the third-party software below, each under its own licence.'
Add 'Shosa itself is under the MIT license (LICENSE). shosa-music, in server, is under the GNU'
Add 'General Public License version 3 or later (server/LICENSE-shosa-music.txt).'
Add

# --- The extension's code (dist): the npm packages esbuild bundled. ---
$listed = "$Extension\dist\bundled.packages.json"
if (-not (Test-Path $listed)) { throw "$listed is missing: run npm run build in $Extension" }
$packages = Get-Content $listed -Raw | ConvertFrom-Json
Add 'PART 1. In dist (the extension and its preview), from npm'
Add
foreach ($dir in $packages) {
    $full = Join-Path $Extension $dir
    $json = Get-Content "$full\package.json" -Raw | ConvertFrom-Json
    $repo = if ($json.repository -is [string]) { $json.repository } elseif ($json.repository) { $json.repository.url } else { $json.homepage }
    $author = if ($json.author -is [string]) { $json.author } elseif ($json.author) { $json.author.name }
    Add-Entry "$($json.name) $($json.version)" $json.license $repo @(License-Files $full) $(if ($author) { "Copyright (c) $author" })
}

# --- The programs in server: the .NET runtime and the NuGet packages. ---
Add 'PART 2. In server (the language server, the compiler and shosa-music), from .NET and NuGet'
Add
$libraries = [ordered]@{}
foreach ($deps in Get-ChildItem $Server -Filter *.deps.json) {
    foreach ($lib in (Get-Content $deps.FullName -Raw | ConvertFrom-Json).libraries.PSObject.Properties) {
        if ($lib.Value.type -in 'package', 'runtimepack') { $libraries[$lib.Name] = $lib.Value.type }
    }
}
foreach ($name in $libraries.Keys) {
    $id, $version = $name -split '/', 2
    if ($libraries[$name] -eq 'runtimepack') { $id = $id -replace '^runtimepack\.', '' }
    $dir = Join-Path $nuget "$($id.ToLowerInvariant())\$version"
    if (-not (Test-Path $dir)) { throw "$name is not in the NuGet cache ($dir)" }
    $meta = ([xml](Get-Content (Get-ChildItem $dir -Filter *.nuspec | Select-Object -First 1).FullName -Raw)).package.metadata
    $license = if ($meta.license.type -eq 'expression') { $meta.license.'#text' } elseif ($meta.license) { "see $($meta.license.'#text')" } else { $meta.licenseUrl }
    $url = if ($meta.repository.url) { $meta.repository.url } else { $meta.projectUrl }
    $title = if ($libraries[$name] -eq 'runtimepack') { ".NET runtime $version ($id)" } else { "$id $version" }
    Add-Entry $title $license $url @(License-Files $dir) $meta.copyright
}

Add $rule
Add 'LilySharp.Core (Lily#), linked into shosa-music'
Add 'License: GPL-3.0-or-later, as shosa-music (server/LICENSE-shosa-music.txt)'
Add 'Source: https://github.com/yotsuda/LilySharp'
Add $rule
Add
Add 'The fonts Lily# engraves with are in server/Fonts, each with its licence beside it:'
foreach ($f in Get-ChildItem "$Server\Fonts" -Filter '*LICENSE*' | Sort-Object Name) { Add "  server/Fonts/$($f.Name)" }
Add

[IO.File]::WriteAllText($Out, (($text -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
'{0}: {1} npm packages, {2} from .NET and NuGet' -f (Split-Path $Out -Leaf), $packages.Count, $libraries.Count
