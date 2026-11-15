param([string]$Json, [string]$Joint, [double]$Tempo, [double[]]$Beats)
# prints position and speed (m/s) of a joint at the given beats
$j = Get-Content $Json -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$idx = [array]::IndexOf([string[]]$j.joints, $Joint)
$fr = $j.figures[0].frames
$fps = $j.fps
function P($f) { $r = $fr[$f]; if ($r[0] -is [array]) { $p = $r[$idx] } else { $p = @($r[3*$idx], $r[3*$idx+1], $r[3*$idx+2]) }; return ,$p }
foreach ($b in $Beats) {
  $t = $b * 60 / $Tempo; $f = [int][math]::Round($t * $fps)
  if ($f -ge $fr.Count) { $f = $fr.Count - 1 }
  $p = P $f; $a = P ([math]::Max(0,$f-1)); $c = P ([math]::Min($fr.Count-1,$f+1))
  $v = [math]::Sqrt(([math]::Pow($c[0]-$a[0],2)+[math]::Pow($c[1]-$a[1],2)+[math]::Pow($c[2]-$a[2],2))) * $fps / 2
  "{0,6} beat f{1,4}: x={2,7:F3} y={3,7:F3} z={4,7:F3}  speed={5:F3} m/s" -f $b, $f, $p[0], $p[1], $p[2], $v
}
