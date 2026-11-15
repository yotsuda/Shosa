param([string]$Json, [string]$Score, [int[]]$Frames, [string[]]$Names = @('pelvis','leftAnkle','rightAnkle'))
$all = Get-Content $Json -Raw | ConvertFrom-Json
if ($all -is [array]) { $c = $all | Where-Object { $_.name -eq $Score } } else { $c = $all }
$joints = $c.joints
foreach ($fi in 0..($c.figures.Count-1)) {
  $fig = $c.figures[$fi]
  foreach ($f in $Frames) {
    $row = $fig.frames[$f]
    $ang = $fig.angles[$f]
    $line = "fig$fi f$f :"
    foreach ($n in $Names) {
      $i = [array]::IndexOf($joints, $n)
      $line += (" {0}=({1:F3},{2:F3},{3:F3})" -f $n, $row[3*$i], $row[3*$i+1], $row[3*$i+2])
    }
    $line += " angles=" + (($ang | ForEach-Object { '{0:F1}' -f $_ }) -join ',')
    $line
  }
}
