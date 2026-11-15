param([string]$file, [int[]]$frames)
$j = Get-Content $file -Raw | ConvertFrom-Json
$names = $j.joints
function ix($n) { [array]::IndexOf($names, $n) }
$figs = @($j.figures)
"figures: $($figs.Count); props: " + (($figs[0] | Get-Member -MemberType NoteProperty).Name -join ',')
foreach ($fig in $figs) {
  "--- " + $fig.name + " frames=" + $fig.frames.Count
  foreach ($f in $frames) {
    if ($f -ge $fig.frames.Count) { continue }
    $r = $fig.frames[$f]
    $l = ix 'leftAnkle'; $rr = ix 'rightAnkle'; $lt = ix 'leftToe'; $rt = ix 'rightToe'; $p = ix 'pelvis'
    $lx = $r[3*$l]; $ly = $r[3*$l+1]; $lz = $r[3*$l+2]; $rx = $r[3*$rr]; $ry = $r[3*$rr+1]; $rz = $r[3*$rr+2]
    "{0,4}: L({1:N3},{2:N3},{3:N3}) R({4:N3},{5:N3},{6:N3}) dx={7:N3} dz={8:N3} d={9:N3} pelvis({10:N3},{11:N3},{12:N3}) Ltoe({13:N3},{14:N3}) Rtoe({15:N3},{16:N3})" -f $f, $lx, $ly, $lz, $rx, $ry, $rz, ($rx-$lx), ($rz-$lz), [math]::Sqrt(($rx-$lx)*($rx-$lx)+($rz-$lz)*($rz-$lz)), $r[3*$p], $r[3*$p+1], $r[3*$p+2], $r[3*$lt], $r[3*$lt+2], $r[3*$rt], $r[3*$rt+2]
  }
}

