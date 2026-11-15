param([string]$file)
# minimum distance between the two feet (segment ankle-toe, 3D) and between the two shins (knee-ankle), per jump
$j = Get-Content $file -Raw | ConvertFrom-Json
$names = $j.joints
function ix($n) { [array]::IndexOf($names, $n) }
function P($r, $n) { $i = ix $n; return @($r[3*$i], $r[3*$i+1], $r[3*$i+2]) }
function segdist($a, $b, $c, $d) {
  # sample both segments
  $best = 99
  for ($s = 0; $s -le 10; $s++) { for ($t = 0; $t -le 10; $t++) {
    $p = 0..2 | ForEach-Object { $a[$_] + ($b[$_]-$a[$_])*$s/10 }
    $q = 0..2 | ForEach-Object { $c[$_] + ($d[$_]-$c[$_])*$t/10 }
    $dd = [math]::Sqrt(($p[0]-$q[0])*($p[0]-$q[0]) + ($p[1]-$q[1])*($p[1]-$q[1]) + ($p[2]-$q[2])*($p[2]-$q[2]))
    if ($dd -lt $best) { $best = $dd } } }
  return $best
}
$fr = $j.figures[0].frames
$minF = 99; $minS = 99; $atF = -1; $atS = -1
for ($f = 0; $f -lt $fr.Count; $f++) {
  $r = $fr[$f]
  $df = segdist (P $r 'leftAnkle') (P $r 'leftToe') (P $r 'rightAnkle') (P $r 'rightToe')
  $ds = segdist (P $r 'leftKnee') (P $r 'leftAnkle') (P $r 'rightKnee') (P $r 'rightAnkle')
  if ($df -lt $minF) { $minF = $df; $atF = $f }
  if ($ds -lt $minS) { $minS = $ds; $atS = $f }
  if ($args -contains 'all') { "{0,4} foot {1:N3} shin {2:N3}" -f $f, $df, $ds }
}
"min foot-foot (ankle->toe segments) {0:N3} m at frame {1}; min shin-shin {2:N3} m at frame {3}" -f $minF, $atF, $minS, $atS
