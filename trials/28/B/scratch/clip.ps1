param([string]$Path, [double]$From = 0, [double]$To = 1e9, [double]$Step = 0.25, [string[]]$Joints = @('pelvis'))
# Prints joint positions (x y z, m) at beat steps. tempo from clip (assumes one tempo).
$j = Get-Content $Path -Raw | ConvertFrom-Json
$fps = $j.fps; $tempo = [double]$j.tempo
$fr = $j.figures[0].frames
$names = @($j.joints)
$row0 = $fr[0]
"frames=$($fr.Count) rowlen=$(@($row0).Count) tempo=$tempo"
for ($b = $From; $b -le [math]::Min($To, $j.totalBeats) + 1e-9; $b += $Step) {
  $f = [int][math]::Round($b * 60 / $tempo * $fps); if ($f -ge $fr.Count) { $f = $fr.Count - 1 }
  $row = @($fr[$f])
  $line = "beat {0,6:0.00} f{1,4}" -f $b, $f
  foreach ($n in $Joints) { $i = [array]::IndexOf($names, $n); $line += "  {0} {1,7:0.000} {2,6:0.000} {3,7:0.000}" -f $n, $row[3*$i], $row[3*$i+1], $row[3*$i+2] }
  $line
}
