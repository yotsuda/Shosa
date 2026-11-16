param([string]$Path, [double]$From, [double]$To, [string]$Joint = 'pelvis', [int]$Axis = 0)
# sideways (axis 0=x,1=y,2=z) speed per frame (m/s) of a joint between beats; prints min, mean, and the frames
$j = Get-Content $Path -Raw | ConvertFrom-Json
$fps = $j.fps; $tempo = [double]$j.tempo
$fr = $j.figures[0].frames
$i = [array]::IndexOf(@($j.joints), $Joint)
$f0 = [int][math]::Round($From * 60 / $tempo * $fps); $f1 = [int][math]::Round($To * 60 / $tempo * $fps)
$v = @()
for ($f = $f0; $f -lt $f1; $f++) {
  $a = @($fr[$f])[3*$i+$Axis]; $b = @($fr[$f+1])[3*$i+$Axis]
  $v += ($b - $a) * $fps
}
$disp = @($fr[$f1])[3*$i+$Axis] - @($fr[$f0])[3*$i+$Axis]
$mean = $disp / (($f1 - $f0) / $fps)
$k = 0
foreach ($x in $v) { "f{0,4} beat {1,6:0.000} v {2,7:0.000}" -f ($f0+$k), (($f0+$k+0.5)/$fps*$tempo/60), $x; $k++ }
"displacement {0:0.000} m, mean {1:0.000} m/s, min {2:0.000}, max {3:0.000}" -f $disp, $mean, ($v | Measure-Object -Minimum).Minimum, ($v | Measure-Object -Maximum).Maximum
