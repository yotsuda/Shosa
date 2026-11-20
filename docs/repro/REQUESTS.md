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
