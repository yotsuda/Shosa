# 依頼 1: 曲を読むところ（shosa-music）を lysc に移したい

差出: Shosa のセッション、2026-11-20。オーナー決定（「移す」「ゆるい形にする」）。

## なぜ

Shosa は Lily# の曲（`.lys`）に合わせて踊る。曲を読むのは今、Shosa のリポジトリの `Shosa.Music`
（`shosa-music.exe`）で、LilySharp.Core をプロジェクト参照している（`..\..\LilySharp`）。これを Shosa の拡張に同梱している。
そのため:

- Shosa の拡張が GPL のプログラムを抱える（Shosa のほかは MIT）。ImageSharp を 2.1.11 に留める細工も Shosa にある。
- Shosa のリポジトリだけではビルドできない（隣に Lily# の checkout が要る）。
- Lily# の文法が変わっても、Shosa を作り直すまで新しい書き方を読めない。

そこで、曲を読むところを Lily# の `lysc` に置き、Shosa は入っている Lily# の拡張の `lysc` を呼ぶ形にしたい。
Lily# がなければ、曲に合わせる機能だけが使えない（動きだけを書くのには要らない）＝**ゆるい依存**。
`extensionDependencies` にはしない。

## お願いしたいこと

いまの `shosa-music` のコード（`C:\MyProj\Shosa\Shosa.Music\Program.cs`・`SongFile.cs`、GPL-3.0-or-later、作者はオーナー）は
そのまま持っていってよい。名前・置き場所・引数の綴りは Lily# 側で決めてほしい（`lysc` の流儀に合わせてよい。Shosa が合わせる）。

1. **`lysc timeline SONG.lys`**: 曲のテンポ・拍子・セクションとその長さを JSON で標準出力に。読めない曲でも JSON（`error` に
   文、終了コード 0）。形は今の `Timeline`（camelCase）:
   `error`、`tempo`（最初の bpm。拍は拍子の下の数）、`beatsPerBar`・`beatUnit`（最初の拍子）、`sections`（曲にある全部）、
   `plays`（演奏される順の `{name, beats, pickup}`。`pickup` は最初の小節線より前の拍、弱起でなければ 0）、`scores`（`""` は名前のない score）、
   `tempos`（`{beat, bpm}`、拍 0 から、その場の拍子の拍で）、`meters`（`{beat, beatsPerBar, beatUnit}`）。
   曲の終わりは最後の小節線（フェルマータや伸ばす音がはみ出しても、終わりの休符が手前で止まっても）。
2. **`lysc notes SONG.lys`**: 演奏される全部の音を JSON で。`{error, notes}`、`notes` は始まりの順（同時なら音高の順）の
   `[始まりの拍, 長さの拍, 音高（MIDI 番号、.5 は四分音）, 強さ 1–127, 音色（プレビューの系統 0 ピアノ … 8 声、9 はドラム）,
   program（GM の楽器 0–127、パートが名乗らなければ -1）, channel（0–15、9 はドラム）]`。拍は `timeline` と同じ数え方。
3. **演奏の順とテンポを変える引数**（`timeline`・`notes`・`midi`・`wav` に）:
   - `--order "A B A"`: score の form の代わりにこのセクションの順で。曲にない名前は捨て、1 つも残らなければ `error`。
     今の作りは、その順を form として曲に書き足して Lily# の form の規則で鳴らしている。
   - `--tempo N`: 最初のテンポを N bpm に。テンポが変わる曲は比を保つ（拍の位置は変わらない）。
   - `--score NAME` は今の lysc のままでよい。
4. **見つけ方と約束**:
   - `lysc --version --json` に、この JSON の形の番号（protocol）を入れてほしい。Shosa は最低版を確かめる
     （Orecchiante と Lily# の間の `protocol` と同じやり方）。
   - Lily# の拡張の `activate()` が `lysc` の場所を返すと、Shosa の拡張がそれを使える（できなければ、拡張のフォルダの
     `server\lysc.exe` を Shosa が探す）。
   - JSON の形を見張る試験を Lily# 側に置いてほしい（形が黙って変わると Shosa が壊れる）。

どれも依頼で、入れるかどうか・どう入れるかは Lily# 側（とオーナー）が決める。

## Shosa 側でこの後やること（Lily# が入れてから）

`Shosa.Core/Music/SongReader.cs` が `lysc` を呼ぶ。VS Code のターミナルでは Shosa の拡張が Lily# の `server` を PATH に足し、
外では環境変数（今の `SHOSA_MUSIC` に当たるもの）で `lysc` を指す。Lily# がないときは「曲に合わせるには Lily# を入れてください」と
知らせる。`Shosa.Music` とその LICENSE、パッケージへの同梱、ImageSharp の細工をやめる。DESIGN.md 92 節。
