# Renders .vrma files on VRM models with tools\vrm_video.html (three-vrm) and adds the sound.
#   .\render.ps1 -Vrma /out/edge/runs/chorus/edge.vrma,/out/edge/runs/chorus2/edge.vrma -Out runs\chorus\edge.mp4 -Audio runs\chorus\audio.wav [-Start 22.5] [-Seconds 15]
param(
    [string[]]$Vrma,
    [string[]]$Vrm = @('/out/vrm/mini-zundamon.vrm', '/out/vrm/mini-zunko.vrm'),
    [string]$Out,
    [string]$Audio,
    [double]$Start = 0,
    [double]$Seconds = 15,
    [int]$Port = 8378,
    [string]$Cam = '0,1.1,3.6',
    [string]$At = '0,0.45,0',
    [int]$Tries = 3
)
$ErrorActionPreference = 'Stop'
$repo = 'C:\MyProj\Shosa'
$chrome = @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe", "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
$url = "http://127.0.0.1:$Port/tools/vrm_video.html?vrm=$($Vrm -join ',')&vrma=$($Vrma -join ',')&fps=30&start=$Start&seconds=$Seconds&cam=$Cam&at=$At"

for ($try = 1; $try -le $Tries; $try++) {
    $frames = Join-Path $env:TEMP ("vrmframes-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $profile = "$frames-chrome"
    # py starts python as a child: stop both, or the old server keeps the port (a later page then fails to fetch)
    Start-Process py -ArgumentList "$repo\tools\frame_server.py", $frames, '--port', $Port -NoNewWindow -RedirectStandardError "$frames-server.err" -RedirectStandardOutput "$frames-server.log" | Out-Null
    Start-Sleep 2
    $browser = Start-Process $chrome -ArgumentList '--headless=new', "--user-data-dir=$profile", '--window-size=1920,1080', $url -PassThru
    $deadline = (Get-Date).AddMinutes(10)
    while (-not (Test-Path "$frames\done.txt") -and (Get-Date) -lt $deadline) { Start-Sleep 2 }
    Stop-Process -Id $browser.Id -ErrorAction SilentlyContinue
    Get-CimInstance Win32_Process -Filter "Name='chrome.exe' OR Name='python.exe' OR Name='py.exe'" |
        Where-Object { $_.CommandLine -like "*$frames*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
    $done = if (Test-Path "$frames\done.txt") { Get-Content "$frames\done.txt" -Raw } else { 'no done.txt' }
    if ($done -notmatch '^failed|^no done') { break }
    Write-Warning "try $try failed: $($done.Split("`n")[0])"
    Start-Sleep 2
}
if ($done -match '^failed|^no done') { throw "rendering failed in $frames" }
$done
ffmpeg -loglevel error -y -framerate 30 -i "$frames\%05d.png" -i $Audio -c:v libx264 -crf 18 -pix_fmt yuv420p -c:a aac -shortest $Out
Get-Item $Out | Select-Object FullName, Length
$frames
