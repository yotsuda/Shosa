param([string]$file, [string]$hand = 'rightHand', [double]$from = 0, [double]$to = 999, [double]$step = 0.125)
# Lateral position of a hand from the chest midline, in the body's own frame (shoulder line).
$j = Get-Content $file -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$fr = $j.figures[0].frames; $fpb = $j.fps * 60 / $j.tempo
function I($n) { [array]::IndexOf($j.joints, $n) }
$c = I 'chest'; $ls = I 'leftShoulder'; $rs = I 'rightShoulder'; $h = I $hand; $p = I 'pelvis'
$out = @()
for ($f = 0; $f -lt $fr.Count; $f++) {
  $b = $f / $fpb; if ($b -lt $from -or $b -gt $to) { continue }
  $r = $fr[$f]
  $rx = $r[3*$rs] - $r[3*$ls]; $rz = $r[3*$rs+2] - $r[3*$ls+2]; $l = [math]::Sqrt($rx*$rx+$rz*$rz); $rx/=$l; $rz/=$l
  # forward = up x right ... right=(rx,0,rz); forward = (−rz,0,rx)? choose so that facing front (right=(-1,0,0)) gives fwd=(0,0,1)
  $fx = $rz; $fz = -$rx
  $dx = $r[3*$h] - $r[3*$c]; $dz = $r[3*$h+2] - $r[3*$c+2]
  $out += [pscustomobject]@{ f = $f; beat = [math]::Round($b,3); rightOfMid_cm = [math]::Round(($dx*$rx+$dz*$rz)*100,1); fwd_cm = [math]::Round(($dx*$fx+$dz*$fz)*100,1); y_cm = [math]::Round($r[3*$h+1]*100,1); facing = [math]::Round([math]::Atan2($fx,$fz)*180/[math]::PI,1) }
}
$global:LAT = $out
$out | Where-Object { ($_.f % [math]::Max(1,[int]($step*$fpb))) -eq 0 } | Format-Table -AutoSize | Out-String
"min rightOfMid in range: " + ($out | Sort-Object rightOfMid_cm | Select-Object -First 1 | Out-String)
