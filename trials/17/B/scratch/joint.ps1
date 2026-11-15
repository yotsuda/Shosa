param([string]$Json, [int[]]$Frames, [string[]]$Joints, [string]$Figure)
# Print x y z of named joints at given frames from a `shosa clip` JSON.
$j = Get-Content $Json -Raw | ConvertFrom-Json
if ($j -is [array]) { $j = $j[0] }
$names = @($j.joints)
foreach ($fig in $j.figures) {
  if ($Figure -and $fig.name -ne $Figure) { continue }
  foreach ($f in $Frames) {
    $row = $fig.frames[$f]
    foreach ($n in $Joints) {
      $i = [array]::IndexOf($names, $n)
      '{0} f{1} {2,-14} x={3,7:F3} y={4,7:F3} z={5,7:F3}' -f $fig.name, $f, $n, $row[3*$i], $row[3*$i+1], $row[3*$i+2]
    }
    $a = $fig.angles[$f]
    '{0} f{1} angles({2}) = {3}' -f $fig.name, $f, ($j.angleNames -join ','), (($a | % { '{0:F1}' -f $_ }) -join ', ')
  }
}
