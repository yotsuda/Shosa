param([string]$file, [double[]]$seconds, [string[]]$names = @('pelvis','leftKnee','rightKnee','leftToe','rightToe','rightAnkle','leftAnkle'), [int]$fig = 0)
$c = Get-Content $file -Raw | ConvertFrom-Json
if ($c -is [array]) { $c = $c[0] }
$f = $c.figures[$fig]
foreach ($s in $seconds) {
  $fr = [int][math]::Round($s * $c.fps)
  $row = $f.frames[$fr]
  $out = "t=$s f=$fr"
  foreach ($n in $names) {
    $i = [array]::IndexOf($c.joints, $n)
    $out += ("  {0}=({1:N3},{2:N3},{3:N3})" -f $n, $row[3*$i], $row[3*$i+1], $row[3*$i+2])
  }
  $out
}
