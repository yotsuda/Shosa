# Shosa → Lily# の依頼 5: renderStems の part と voice を、楽譜に並ぶ順で（2026-11-20）

一覧: [REQUESTS.md](REQUESTS.md) の 5。前提は依頼 4 の API（`fbf205e01`、[返事](lilysharp-reply-2026-11-20-stems-api.md)）。

## 見えていること

オーナーが Shosa のプレビューのミキサーで `糸 (Bank Band).lys` を鳴らした（2026-11-20。API は「ちゃんと機能している」）。
ミキサーの楽譜の行が **bass、prog** の順に並ぶ。Shosa は `renderStems` が返した `stems` の順に並べている。

```
part bass { instrument bass5 }      // 10 行目: 宣言はこれだけ
section A { chords prog { … } … }   // prog は宣言なしで使われる
score {
  chords prog
  tab bass as removeEmpty all
}
```

`notesByPart` は `job.Parts` の順で、それが宣言の順（bass が先）になっているように見える。

## オーナーの決定（2026-11-20）

**楽譜の行は .lys に出てくる順**。この曲では **prog が先、bass が後**（`score` の中に書いた順、楽譜の上から下と同じ）。

## 依頼

- `renderStems` の `stems` のうち `part` と `voice` を、その score が楽譜に並べる順（`score { }` の中の書いた順。`score` を書かない .lys ではプレビューが楽譜に並べる順）で返してほしい。
- 歌声（`voice`）は、それが歌うパートの位置に（そのパートの `part` の直後、パートが歌うだけで `part` が出ないならその位置に）。
- `recording` と `recordingPart` の順はどちらでもよい（Shosa がオーナーの決めた順 vocal guitar piano other bass drums に並べ替える）。
- 形を変えるわけではないので `stemsVersion` は 1 のままでよいと思う（順を約束に加えるだけ）。

## Shosa の側

Shosa は返ってきた順をそのまま使う（`editors/vscode/src/preview/stems.js` の `stemRows`）。入ったら Shosa 側で糸を開いて確かめ、done にする。
