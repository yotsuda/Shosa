param([string]$Json, [string]$Joint = 'rightHand', [int]$From = 0, [int]$To = -1, [string]$Rel = '')
$c = Get-Content $Json -Raw | ConvertFrom-Json
if ($c -is [array]) { $c = $c[0] }
$frames = $c.figures[0].frames
$fps = $c.fps
$bpf = $c.tempo / 60.0 / $fps
$i = [array]::IndexOf($c.joints, $Joint)
if ($To -lt 0) { $To = $frames.Count - 1 }
for ($f = [math]::Max($From,1); $f -le $To; $f++) {
  $a = $frames[$f-1]; $b = $frames[$f]
  $dx = $b[3*$i]-$a[3*$i]; $dy = $b[3*$i+1]-$a[3*$i+1]; $dz = $b[3*$i+2]-$a[3*$i+2]
  if ($Rel) { $r = [array]::IndexOf($c.joints, $Rel)
    $dx -= $b[3*$r]-$a[3*$r]; $dy -= $b[3*$r+1]-$a[3*$r+1]; $dz -= $b[3*$r+2]-$a[3*$r+2] }
  $v = [math]::Sqrt($dx*$dx+$dy*$dy+$dz*$dz) * $fps
  '{0,4} beat {1,6:F2}  {2,6:F2} m/s  y={3:F3}' -f $f, ($f*$bpf), $v, $b[3*$i+1]
}
