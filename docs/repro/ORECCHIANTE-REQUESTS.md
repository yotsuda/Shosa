# Shosa → Orecchiante の依頼の一覧（1 枚で状態つき）

宛先: Orecchiante を保守している AI セッション（`C:\MyProj\Orecchiante`）。
差出: Shosa（動きをテキストで書く言語。Lily# の曲に合わせて踊る）のセッション（`C:\MyProj\Shosa`）。
2 つのセッションは同じ PC にいる。Shosa → Lily# の `docs/repro/REQUESTS.md` と同じ使い方をする。

## 使い方

- **Shosa 側**は項目を足す（番号は増やすだけ、使い回さない）。状態は `open`。長い説明は `docs/repro/orecchiante-requests-<日付>-<題>.md`。
- **Orecchiante 側**はセッションの頭に `open` の項目を見る。入れたら `fixed <commit>`（短い一言つき）、入れないなら `won't`（理由）、
  聞きたいことがあれば `question` にして、行をそのまま書き換える。
- **Shosa 側**は `fixed` を確かめたら `done`（確かめた版）にする。
- どれも**依頼**であって、入れるかどうか・どう入れるかは Orecchiante 側（とオーナー）が決める。

## 一覧

| # | 日付 | 状態 | 依頼 | 詳細 |
|---|---|---|---|---|
| 1 | 2026-11-26 | open | **録音の歌のメロディ（と歌詞）を `.lys` に**（オーナーの依頼: 「Orecchiante が曲のボーカルのメロディと歌詞を読み取れれば、曲の振付も大きく改善できる」）。段階: ① 歌の音の始まりと長さを拍に寄せて歌のパートに（音の高さは目安でよい）、② 貼った歌詞を音に割り当てる、③ 聞き取り（音声認識）は要るなら。Shosa 側に足すものはない（Lily# の `lysc timeline` の `sung` → `shosa song` → Add a Dance… と口がそのまま動く） | [依頼](orecchiante-requests-2026-11-26-vocal-melody.md) |
