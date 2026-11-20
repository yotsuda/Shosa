# 依頼 2: 録音の拍を `lysc timeline` で（2026-11-20）

宛先: Lily# のセッション（`C:\MyProj\LilySharp`）。差出: Shosa のセッション（`C:\MyProj\Shosa`）。
一覧: [REQUESTS.md](REQUESTS.md) の 2。前提は依頼 1（`lysc timeline`、fixed `aa6602d4d`）。

## したいこと

オーナーが Orecchiante で録音から作った `.lys`（例: `糸 (Bank Band).lys`、隣に `糸 (Bank Band).orecchiante.json` と `.mp3`）に合わせて、
Shosa の踊りを**録音そのもののタイミングで**踊らせ、プレビューと MP4 で録音を鳴らしたい。

`.lys` のテンポは `tempo 4 = 61.5` の 1 つだが、録音は小節ごとに 52〜67 で揺れる。61.5 のまま踊らせると、
小節線が最大 1.49 秒（39 小節目、1.5 拍）ずれる。正しい時刻は隣のファイルの `bars[].beats`（各拍の秒）にある。

## オーナーの決定（Shosa 側で、2026-11-20）

- 拍の時刻は **Lily# の `lysc` から受け取る**（SIDECAR の形式の知識は Lily# に置いたまま。Shosa が曲を読むのを `lysc` に移す方針、Shosa DESIGN.md 92 節のとおり）。
- **自動**: 隣のファイルがある `.lys` に合わせて踊るときは、いつも録音の拍に合わせ、録音を鳴らす。`.shosa` には何も書かない
  （Lily# の recording-design.md 1 節「紐づけは隣のファイルだけ、`.lys` には何も書かない」と同じ考え）。

## お願い（形は Lily# 側で決めてよい。下は Shosa が受け取りやすい形の案）

1. **`lysc timeline SONG.lys`**: 隣に `<名前>.orecchiante.json`（SIDECAR.md の形式 1/2）があれば、
   - `tempos` を録音から作る: 拍ごとに 1 つ（`{ beat, bpm }`、bpm = 60 ÷ その拍から次の拍までの秒）。最後の拍は小節の `end` まで。
     `beats` がない小節は、その小節の `start`〜`end` を拍の数で等分。Shosa は今の `tempos` をそのまま使うので、ほかに変えることはない。
   - `recording` を足す: `{ audio: 音源の絶対パス, offset: 拍 0 が録音の何秒か（bars[0].start）, duration?: 録音の長さ }`。
     Shosa のプレビューと MP4 は、踊りの 0 秒を録音の `offset` 秒に合わせて鳴らす。
   - 録音を使わなかったときは `recording` なし。使えなかった理由（小節の数が合わない、ある小節の拍の数が `.lys` と違う、音源がない…）は
     `recordingError` などの文で（踊りは楽譜のテンポで続ける。Shosa はその文を作者に見せる）。
2. **`--order` や `--tempo` があるとき**（踊りが自分の `form` やテンポで曲を鳴らすとき）は、録音はその順やテンポでは鳴らせないので、
   録音を使わない（`recording` なし、理由の文つき）。
3. `lysc --version --json` の `protocol` を上げるかどうかは Lily# 側で。Shosa は `recording` の有無を見るだけにする。
4. 形を見張る試験（依頼 1 の `CliSongReadingTests` に並べて）: `糸 (Bank Band)` のような、隣のファイルがある曲で、
   `tempos` の拍の秒を足すと各小節の `start` に戻ること。

## Shosa 側がすること（入ったら）

- `SongReader` が `lysc timeline` を呼ぶ（依頼 1 の切り替え）。`tempos` は今の仕組みのまま。
- `recording` を `MusicIr` と clip に通し、プレビューと MP4 は合成の音の代わりに録音を鳴らす（`offset` から）。
- `check` は録音に合わせたことと、使えなかった理由を言う。
