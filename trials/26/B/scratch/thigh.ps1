param([string]$Json, [double]$Tempo, [double]$Beat, [string]$Side = 'left')
# thigh: angle above level, horizontal direction (deg from back toward side), knee height
$j = Get-Content $Json -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$names = [string[]]$j.joints
$f = [int][math]::Round($Beat * 60 / $Tempo * $j.fps)
$r = $j.figures[0].frames[$f]
function P($n) { $i = [array]::IndexOf($names, $n); return ,@($r[3*$i], $r[3*$i+1], $r[3*$i+2]) }
$h = P "${Side}Hip"; $k = P "${Side}Knee"; $a = P "${Side}Ankle"; $p = P 'pelvis'
$d = @(($k[0]-$h[0]), ($k[1]-$h[1]), ($k[2]-$h[2]))
$hor = [math]::Sqrt($d[0]*$d[0] + $d[2]*$d[2])
$up = [math]::Atan2($d[1], $hor) * 180 / [math]::PI
$side = [math]::Atan2([math]::Abs($d[0]), -$d[2]) * 180 / [math]::PI
"beat {0}: thigh {1:F0} deg above level, horizontal {2:F0} deg from straight back toward the side; knee y={3:F3}, ankle y={4:F3} z={5:F3}, hip y={6:F3}, pelvis x={7:F3} y={8:F3} z={9:F3}" -f $Beat, $up, $side, $k[1], $a[1], $a[2], $h[1], $p[0], $p[1], $p[2]

