param([string]$file, [double]$stepBeats = 0.5)
. "$PSScriptRoot\an.ps1" $file
$fr = $JJ.figures[0].frames; $fpb = $JJ.fps * 60 / $JJ.tempo
$n = $fr.Count
"last vs first frame max diff: " + ((0..($fr[0].Count-1) | % { [math]::Abs($fr[$n-1][$_]-$fr[0][$_]) } | measure -Max).Maximum)
for ($b = 0; $b * $fpb -le $n - 1; $b += $stepBeats) {
  $f = [int][math]::Round($b * $fpb)
  $v = if ($f -gt 0) { [math]::Abs($fr[$f][0]-$fr[$f-1][0])*100 } else { 0 }
  "b{0,5}  pelvis x={1,7:N2} y={2,7:N2} z={3,7:N2}  |dx|/fr={4,6:N3}  face={5,6:N1}" -f $b, ($fr[$f][0]*100), ($fr[$f][1]*100), ($fr[$f][2]*100), $v, $JJ.figures[0].angles[$f][3]
}
$xs = 0..($n-1) | % { $fr[$_][0]*100 }
"pelvis x min {0:N2} max {1:N2}" -f ($xs|measure -Min).Minimum, ($xs|measure -Max).Maximum
"frames where pelvis x speed < 0.01 cm/frame: " + ((1..($n-1) | ? { [math]::Abs($fr[$_][0]-$fr[$_-1][0])*100 -lt 0.01 }) -join ',')
