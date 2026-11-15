param([string]$Path, [int[]]$Frames, [string[]]$Joints = @('pelvis','leftAnkle','rightAnkle','leftToe','rightToe','leftKnee','rightKnee','head','nose'), [int]$Figure = 0)
$j = Get-Content $Path -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$fig = $j.figures[$Figure]
$names = $j.joints
foreach ($f in $Frames) {
  $row = $fig.frames[$f]
  $line = "frame $f ($([math]::Round($f / $j.fps, 3)) s):"
  foreach ($n in $Joints) {
    $i = [array]::IndexOf($names, $n)
    $line += " $n=(" + (($row[3*$i], $row[3*$i+1], $row[3*$i+2] | ForEach-Object { '{0:F3}' -f $_ }) -join ',') + ")"
  }
  if ($fig.angles) { $line += " angles=" + (($fig.angles[$f] | ForEach-Object { '{0:F1}' -f $_ }) -join ',') }
  $line
}
