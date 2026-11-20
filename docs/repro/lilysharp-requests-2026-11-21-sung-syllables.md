# Shosa → Lily# の依頼 6: lysc が、歌われる音節を拍で出す（2026-11-21）

一覧: [REQUESTS.md](REQUESTS.md) の 6。前提は依頼 1 の `lysc timeline`／`notes`（protocol 1）。

## したいこと

Shosa のキャラクター（VRM）が、曲の歌詞に合わせて口を動かす（リップシンク。VRM の表情 `aa` `ih` `ou` `ee` `oh`）。
YouTube に出す「AI が作った曲で VRM の女の子が歌いながら踊る」動画のため（Shosa の ROADMAP 2 節）。

## オーナーの決定（2026-11-21）

- **どの踊り手がどの歌声で口を動かすかは `.shosa` に書く**: `dancer alice humanoid "girl.vrm" sings melody on solo`。
  `melody` は Lily# の**パートの名前**（`singer … on melody` の `PART`）。`.lys` には何も足さない。
- **口の時刻は `lysc` から受け取る**: 歌われる音節とその拍と長さ。Shosa が仮名を母音に変え、拍を秒にする
  （録音に付いていく曲では、今の仕組み（隣の `.orecchiante.json`）で拍が録音の秒になる）。
  Cantoria の音素やラベルは要らない（Lily# がラベルを見ない境界はそのまま）。

## 依頼

`lysc timeline` に、**`singer` の行が歌う音節**を足してほしい（`timeline` が望ましい: Shosa は口を動きの一部として
`timeline` から作る (`notes` はプレビューの音にだけ使う)。別の場所がよければ Lily# が決めてよい）。
新しい欄なので protocol は 1 のままでよいと思う。案:

```json
"sung": [
  { "part": "melody", "singer": "alice",
    "syllables": [[0, 1, "そ"], [1, 0.5, "ら"], [1.5, 2.5, "に"]] }
]
```

- `part`: `singer … on PART` のパートの名前。`singer`: 名前（無ければ `null`）。同じパートを 2 人が歌えば 2 項目。
- `syllables`: 歌われる順に `[beat, beats, text]`。拍は `notes` と同じ数え方（`--order`・`--tempo` も同じ）。
  - `beats` は**声が出ている長さ**（メリスマで音符をまたぐ音節は、またいだ音符の全部。延長線 `__` も）。休符は含めない。
  - `text` は歌われるとおりの文字（仮名の音節、`ー`、`ん`、`っ` など）。Cantoria に渡すのと同じ単位だとありがたい
    （拗音 `きゃ` は 1 つ、などの切り方が Lily# と同じになる）。
  - `in SECTION` で歌わないセクション、`sings` で選んだ歌詞、何番の歌詞かは、Lily# が実際に歌わせるとおりに。
- 仮名でない歌詞（英語など）もそのまま `text` に入れてほしい（Shosa は母音を推し量るか、口を開け閉めするだけにする）。
- 歌う行が無い曲は `"sung": []`。

## Shosa の側

- `.shosa` の `sings PART` を読む。曲に無いパート、歌う行の無いパートは Shosa が診断する（`sung` に無ければ）。
- 仮名 → 母音（`aa` `ih` `ou` `ee` `oh`）。`ん` は閉じ、`っ` と休みは閉じる。音節の頭で開き、終わりの少し前から閉じる。
- プレビュー（VRM）と MP4 で表情を動かし、`.vrma` の書き出しに表情のトラックを入れる。
- 入ったら Shosa 側で歌のある曲で確かめ、done にする。

それまでは Shosa は `sung` が無い lysc でも動く（口を動かさず、`sings` の行に「この Lily# は音節を出さない」と情報で言う）。
