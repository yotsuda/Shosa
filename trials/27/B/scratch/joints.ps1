param([string]$Json, [double[]]$Beats, [string[]]$Names = @('pelvis','leftAnkle','rightAnkle'))
$j = Get-Content $Json -Raw | ConvertFrom-Json
$c = $j
if ($j -is [array]) { $c = $j[0] }
$tempo = $c.tempo
$fps = $c.fps
$frames = $c.figures[0].frames
foreach ($b in $Beats) {
  $f = [int][math]::Round($b * 60.0 / $tempo * $fps)
  if ($f -ge $frames.Count) { $f = $frames.Count - 1 }
  $row = $frames[$f]
  $parts = foreach ($n in $Names) {
    $i = [array]::IndexOf($c.joints, $n)
    '{0}=({1:F3},{2:F3},{3:F3})' -f $n, $row[3*$i], $row[3*$i+1], $row[3*$i+2]
  }
  "beat $b (f$f): " + ($parts -join ' ')
}
