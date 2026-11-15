param([string]$Clip, [string[]]$Joints, [double]$Tempo, [double[]]$Ranges)
# Ranges: pairs of beats (from,to). Prints for each joint the max y and the frame/beat, plus min y.
$j = Get-Content $Clip -Raw | ConvertFrom-Json
$c = if ($j -is [array]) { $j[0] } else { $j }
$names = @($c.joints)
$fpb = $c.fps * 60.0 / $Tempo
$frames = $c.figures[0].frames
for ($r = 0; $r -lt $Ranges.Count; $r += 2) {
  $f0 = [int][math]::Floor($Ranges[$r] * $fpb); $f1 = [int][math]::Min([math]::Ceiling($Ranges[$r+1] * $fpb), $frames.Count - 1)
  foreach ($jn in $Joints) {
    $i = [array]::IndexOf($names, $jn)
    $best = -1e9; $bf = 0; $low = 1e9
    for ($f = $f0; $f -le $f1; $f++) { $y = $frames[$f][3*$i+1]; if ($y -gt $best) { $best = $y; $bf = $f }; if ($y -lt $low) { $low = $y } }
    '{0,-12} beats {1}-{2}: max y {3:N3} m at beat {4:N2}; min y {5:N3}' -f $jn, $Ranges[$r], $Ranges[$r+1], $best, ($bf / $fpb), $low
  }
}
