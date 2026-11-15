param([string]$Json, [double]$RefSeconds = 1.0)
# Largest deviation of the face from where it was at RefSeconds, over every frame:
#  - the `angles` face (degrees on the floor, as check --at says) and faceUp
#  - the true 3D angle between the head->nose vectors
$j = Get-Content $Json -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$names = @($j.joints); $ih = [array]::IndexOf($names, 'head'); $in = [array]::IndexOf($names, 'nose')
$fig = $j.figures[0]; $ref = [int][math]::Round($RefSeconds * $j.fps)
function V($row) { $x=$row[3*$in]-$row[3*$ih]; $y=$row[3*$in+1]-$row[3*$ih+1]; $z=$row[3*$in+2]-$row[3*$ih+2]; $l=[math]::Sqrt($x*$x+$y*$y+$z*$z); @(($x/$l), ($y/$l), ($z/$l)) }
$r = V $fig.frames[$ref]; $ra = $fig.angles[$ref]
$best3 = 0; $bf3 = 0; $bestF = 0; $bfF = 0; $bestU = 0; $bfU = 0
for ($f = 0; $f -lt $fig.frames.Count; $f++) {
  $v = V $fig.frames[$f]; $d = [math]::Min([double]1,[math]::Max([double]-1,$r[0]*$v[0]+$r[1]*$v[1]+$r[2]*$v[2]))
  $a3 = [math]::Acos($d) * 180 / [math]::PI
  if ($a3 -gt $best3) { $best3 = $a3; $bf3 = $f }
  $af = [math]::Abs($fig.angles[$f][3] - $ra[3]); if ($af -gt $bestF) { $bestF = $af; $bfF = $f }
  $au = [math]::Abs($fig.angles[$f][4] - $ra[4]); if ($au -gt $bestU) { $bestU = $au; $bfU = $f }
}
'ref frame {0}: face {1:F1}, faceUp {2:F1}' -f $ref, $ra[3], $ra[4]
'largest 3D angle from ref : {0:F1} deg at frame {1} ({2:F2} s)' -f $best3, $bf3, ($bf3 / $j.fps)
'largest floor (face) diff : {0:F1} deg at frame {1} ({2:F2} s)' -f $bestF, $bfF, ($bfF / $j.fps)
'largest faceUp diff       : {0:F1} deg at frame {1} ({2:F2} s)' -f $bestU, $bfU, ($bfU / $j.fps)
