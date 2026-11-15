param([string]$json, [int[]]$frames, [string[]]$joints, [switch]$angles)
$j = Get-Content $json -Raw | ConvertFrom-Json
$c = if ($j -is [array]) { $j[0] } else { $j }
$n = $c.joints; $f = $c.figures[0].frames; $a = $c.figures[0].angles
foreach ($fr in $frames) {
  foreach ($name in $joints) {
    $i = [array]::IndexOf($n, $name); $r = $f[$fr]
    '{0,-12} f{1,-4}: x {2,7:N3} y {3,7:N3} z {4,7:N3}' -f $name, $fr, $r[3*$i], $r[3*$i+1], $r[3*$i+2]
  }
  if ($angles) { "  angles f$fr ($($c.angleNames -join ',')): " + ($a[$fr] -join ' ') }
}
