param([string]$file, [int]$fig = 0)
# Generic analysis: pelvis x/y/z per beat, max per-frame joint movement, and where.
$j = Get-Content $file -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$fps = $j.fps; $tempo = $j.tempo
$nj = $j.joints.Count
$fr = $j.figures[$fig].frames
$n = $fr.Count
"frames=$n fps=$fps tempo=$tempo joints=$nj"
function J($f, $name) { $i = [array]::IndexOf($j.joints, $name); return @($fr[$f][3*$i], $fr[$f][3*$i+1], $fr[$f][3*$i+2]) }
$global:JJ = $j
# per-frame max displacement over all joints
$moves = @()
for ($f = 1; $f -lt $n; $f++) {
  $m = 0; $mj = ''
  for ($k = 0; $k -lt $nj; $k++) {
    $dx = $fr[$f][3*$k]-$fr[$f-1][3*$k]; $dy = $fr[$f][3*$k+1]-$fr[$f-1][3*$k+1]; $dz = $fr[$f][3*$k+2]-$fr[$f-1][3*$k+2]
    $d = [math]::Sqrt($dx*$dx+$dy*$dy+$dz*$dz)
    if ($d -gt $m) { $m = $d; $mj = $j.joints[$k] }
  }
  $moves += [pscustomobject]@{ f = $f; max = [math]::Round($m*100,2); joint = $mj }
}
$global:MOVES = $moves
"top 8 frame-to-frame moves (cm):"
$moves | Sort-Object max -Descending | Select-Object -First 8 | Format-Table -AutoSize | Out-String
