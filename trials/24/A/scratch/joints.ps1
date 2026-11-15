param([string]$Json, [string]$Score, [string[]]$Joints = @('pelvis','leftAnkle','rightAnkle','leftToe','rightToe'), [string]$Coord = 'y', [int]$Every = 1, [double]$Bpm = 120)
# Prints, per frame, a coordinate (x, y or z, metres -> cm) of the named joints.
$all = Get-Content $Json -Raw | ConvertFrom-Json
$c = $all | Where-Object { $_.name -eq $Score } | Select-Object -First 1
if (-not $c) { $c = $all | Select-Object -First 1 }
$fps = $c.fps
$off = @{ x = 0; y = 1; z = 2 }[$Coord]
$idx = $Joints | ForEach-Object { [array]::IndexOf($c.joints, $_) }
$frames = $c.figures[0].frames
for ($f = 0; $f -lt $frames.Count; $f += $Every) {
  $row = $frames[$f]
  $vals = for ($k = 0; $k -lt $idx.Count; $k++) { '{0,7:N1}' -f (100 * $row[3 * $idx[$k] + $off]) }
  '{0,4} {1,6:N3}s {2,6:N2}b ' -f $f, ($f / $fps), ($f / $fps * $Bpm / 60) + ($vals -join ' ')
}
