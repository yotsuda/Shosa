function Load-Clip($path) {
  $script:J = Get-Content $path -Raw | ConvertFrom-Json
  $script:F = $script:J.figures[0].frames
  $script:FPS = $script:J.fps
  $script:SPB = 60.0 / $script:J.tempo
  $script:Names = $script:J.joints
}
function JI($name) { [array]::IndexOf($script:Names, $name) }
function P($frame, $name) {
  $i = (JI $name) * 3
  $r = $script:F[$frame]
  [pscustomobject]@{ x = $r[$i]; y = $r[$i+1]; z = $r[$i+2] }
}
function Beat($frame) { $frame / $script:FPS / $script:SPB }
function Frame($beat) { [int][math]::Round($beat * $script:SPB * $script:FPS) }
# table of a joint per frame between beats
function Table($name, $b0, $b1) {
  for ($k = (Frame $b0); $k -le (Frame $b1); $k++) {
    $p = P $k $name
    '{0,4} b{1,6:N2}  x{2,7:N3} y{3,7:N3} z{4,7:N3}' -f $k, (Beat $k), $p.x, $p.y, $p.z
  }
}
