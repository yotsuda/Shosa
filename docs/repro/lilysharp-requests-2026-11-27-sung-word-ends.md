# 依頼 8: `lysc timeline` の `sung` で、単語の切れ目が分かるように（2026-11-27）

差出: Shosa のセッション。オーナーの依頼で出す（「lily# への依頼を出して」）。

## なぜ

`shosa song`（Add a Dance… が Copilot に渡す曲の読み）は、小節ごとに歌詞を引用で並べる。英語の曲は
音節の間に空白を入れるようにした（Shosa `34addaa`）が、`sung` の音節には単語の途中かどうかが無い。
Lily# の歌詞では `Lit- tle~Rock` のハイフンが「次の音節と同じ単語」を言っているのに、`sung` ではそれが落ちる。

Orecchiante 0.18.0 が作った We're An American Band（`C:\MyProj\Shosa\scratch\usertest\vocal\wab.lys`）:

```
lysc timeline … sung:  58|0.5|Lit   58.5|0.75|tle‿Rock
shosa song:            bar 3 (beat 56)  vocal: … 3Lit 3.5tle‿Rock   "Last night in Lit tle‿Rock"
```

ほしいのは `"Last night in Little‿Rock"`。AI が振付を考えるとき、単語が読めるほうが言葉に合った身ぶりを選べる。

## 頼みたいこと

音節が同じ単語の次の音節へ続く（歌詞でハイフンが付いている）ことを `sung` に。形は Lily# 側で決めてよい。Shosa からの案:

- **音節の 4 つ目の要素に `true`**（続く音節だけ）: `[58, 0.5, "Lit", true]`。Shosa の読み取りは
  4 つ目以降を読み飛ばす（`Song.cs` の `SongSung.Read`）ので、今の Shosa も壊れない。protocol は 1 のままでよいはず。
- 文字にハイフンを残す（`"Lit-"`）のは避けたい: Shosa の口は音節の文字から母音を読むので、読み取りの直しが要る。

日本語（かな）は今のままでよい（Shosa はかなを詰めて並べる）。`__`・`_`（メリスマ）は今どおり 1 音節の長さに入る。

## Shosa 側がすること

入ったら `SongReport.cs` で続く音節の後の空白をやめ、wab.lys で `"Little‿Rock"` を確かめて `done` にする。
急ぎではない（今は `Lit tle` と読める）。
