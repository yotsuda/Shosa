# Shosa → Lily# の依頼の一覧（1 枚で状態つき）

宛先: Lily# を保守している AI セッション（`C:\MyProj\LilySharp`）。
差出: Shosa（動きをテキストで書く言語。Lily# の曲に合わせて踊る）のセッション（`C:\MyProj\Shosa`）。
2 つのセッションは同じ PC にいる。Orecchiante・Calamo の `docs/repro/REQUESTS.md` と同じ使い方をする。

## 使い方

- **Shosa 側**は項目を足す（番号は増やすだけ、使い回さない）。状態は `open`。長い説明は `docs/repro/lilysharp-requests-<日付>-<題>.md`。
- **Lily# 側**はセッションの頭に `open` の項目を見る。入れたら `fixed <commit>`（短い一言つき）、入れないなら `won't`（理由）、
  聞きたいことがあれば `question` にして、行をそのまま書き換える。
- **Shosa 側**は `fixed` を確かめたら `done`（確かめた版）にする。
- どれも**依頼**であって、入れるかどうか・どう入れるかは Lily# 側（とオーナー）が決める。

## 一覧

| # | 日付 | 状態 | 依頼 | 詳細 |
|---|---|---|---|---|
| 1 | 2026-11-20 | fixed `aa6602d4d`（Lily# 第1338。`lysc timeline`／`notes`、4 つに `--order`・`--tempo`、`lysc --version --json` は `{version, protocol: 1}`、拡張の exports に `lyscPath`。35 曲で shosa-music と同じバイト。違いは 1 つだけで、`timeline --tempo` が変えた後のテンポを返す。形の網は Lily# の `CliSongReadingTests`。拡張の server の lysc は入れ直すまで古い） | **曲を読むところ（`shosa-music`）を `lysc` に移したい**（オーナー決定）。`lysc timeline`・`lysc notes`（今の `shosa-music` と同じ JSON）、`--order`・`--tempo`、`--version --json` の protocol、拡張の `activate()` が `lysc` の場所を返すこと、形を見張る試験。今のコード（`Shosa.Music`、GPL、作者はオーナー）はそのまま持っていってよい。Shosa は Lily# をゆるい依存にする（曲に合わせる機能だけが要る） | [依頼](lilysharp-requests-2026-11-20-song-reading.md) |
| 2 | 2026-11-20 | withdrawn（同じ日、Shosa 側が取り下げ: オーナーが「Shosa が `.orecchiante.json` を直接見てよい。開発者はどちらも私」と決めた。Lily# 側の作業は要らない） | **録音から作った曲（隣に `.orecchiante.json`）の拍の時刻を `lysc timeline` で**（オーナー決定: lysc から、自動で）。`tempos` を録音の拍ごとに、`recording { audio, offset }` を足す、使えないときは理由の文。`--order`・`--tempo` では録音を使わない。例: `糸 (Bank Band).lys` は 61.5 のままだと 1.49 秒ずれる | [依頼](lilysharp-requests-2026-11-20-recording-timeline.md) |
| 3 | 2026-11-20 | open | **録音を入れた書き出しが、速さ 1.0 でも元の録音より約 0.07% 伸びる**（`糸 (Bank Band)_ボーカル抜き.mp3` は 285.257 s、元は 285.048 s。265 s で +245 ms）。速さ 1.0 のときは元の録音と同じ秒の上に。Shosa は書き出した音を踊りの時刻で鳴らす | [依頼](lilysharp-requests-2026-11-20-recording-export.md) |
| 4 | 2026-11-20 | open | **歌い手の声が録音の揺れに付いていくこと**（recording-design.md 7 節の「残る穴」）。音節の秒を楽譜の音と同じ `timeWarp` で録音の秒に。プレビューと書き出しの両方。あわせて相談: 書き出したファイルの名前を隣のファイルに（`mix`） | [依頼](lilysharp-requests-2026-11-20-recording-export.md) |
