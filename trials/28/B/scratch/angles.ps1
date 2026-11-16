param([string]$Path, [double]$From = 0, [double]$To = 1e9, [double]$Step = 0.25)
$j = Get-Content $Path -Raw | ConvertFrom-Json
$fps = $j.fps; $tempo = [double]$j.tempo
$an = $j.figures[0].angles
"angleNames: " + ($j.angleNames -join ',')
for ($b = $From; $b -le [math]::Min($To, $j.totalBeats) + 1e-9; $b += $Step) {
  $f = [int][math]::Round($b * 60 / $tempo * $fps); if ($f -ge $an.Count) { $f = $an.Count - 1 }
  "beat {0,6:0.000} f{1,4}  {2}" -f $b, $f, ((@($an[$f]) | ForEach-Object { "{0,7:0.0}" -f $_ }) -join ' ')
}
