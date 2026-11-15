param([string]$Path)
# Loads a clip json; defines helper functions in the caller's scope when dot-sourced.
$global:clip = Get-Content $Path -Raw | ConvertFrom-Json
function global:Get-Score([string]$name) {
  if ($global:clip -is [array]) { return $global:clip | Where-Object { $_.name -eq $name } | Select-Object -First 1 }
  return $global:clip
}
function global:P($s, [string]$joint, [int]$frame) {
  $i = [array]::IndexOf($s.joints, $joint)
  $row = $s.figures[0].frames[$frame]
  return [double[]]@($row[3*$i], $row[3*$i+1], $row[3*$i+2])
}
function global:D([double[]]$a, [double[]]$b) {
  return [math]::Sqrt(($a[0]-$b[0])*($a[0]-$b[0]) + ($a[1]-$b[1])*($a[1]-$b[1]) + ($a[2]-$b[2])*($a[2]-$b[2]))
}
function global:Fmt([double[]]$a) { return ('({0:N3}, {1:N3}, {2:N3})' -f $a[0], $a[1], $a[2]) }
