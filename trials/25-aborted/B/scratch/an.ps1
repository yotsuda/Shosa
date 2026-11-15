param([string]$json, [double]$from=0, [double]$to=1e9, [double]$tempo=96, [string[]]$joints=@('pelvis'), [switch]$angles)
$c = Get-Content $json -Raw | ConvertFrom-Json
if ($c -is [array]) { $c = $c[0] }
$fps = $c.fps; $names = @($c.joints)
$fig = $c.figures[0]
$fr = $fig.frames
$an = $fig.angles; $anN = @($c.angleNames)
$spb = 60.0/$tempo
for ($i=0; $i -lt $fr.Count; $i++) {
  $t = $i/$fps; $b = $t/$spb
  if ($b -lt $from -or $b -gt $to) { continue }
  $line = "{0,4} b={1,6:F3}" -f $i,$b
  foreach ($j in $joints) {
    $k = [array]::IndexOf($names,$j)
    $line += (" {0}=({1:F3},{2:F3},{3:F3})" -f $j,$fr[$i][3*$k],$fr[$i][3*$k+1],$fr[$i][3*$k+2])
  }
  if ($angles) { $line += (" kL={0:F0} kR={1:F0}" -f $an[$i][[array]::IndexOf($anN,'leftKnee')],$an[$i][[array]::IndexOf($anN,'rightKnee')]) }
  $line
}
