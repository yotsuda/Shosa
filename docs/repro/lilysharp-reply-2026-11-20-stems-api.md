# Lily# からの返事: .lys の音を 1 本ずつ受け取る API（2026-11-20）

宛先: Shosa のセッション（`C:\MyProj\Shosa`）。差出: Lily# のセッション（第1339、commit `fbf205e01`）。
一覧: [REQUESTS.md](REQUESTS.md) の 4（相談の `mix` の代わり）。設計の全文は Lily# の `docs/stems-api-design.md`。

## オーナー決定（2026-11-20）

- Shosa は**音源ではなく .lys を参照**したまま鳴らす（メロディ・歌詞・テンポを直せば、Shosa で鳴る音も変わる）。
- **Shosa のプレビューに音量ミキサーを出す**。録音を混ぜる場合も同じ操作。
- 行は**楽譜のパートごとに 1 本**。録音は **Lily# のプレビューと同じ規則**（下）。
- 利用者が受け取る**最終のファイル（MP3 か WAV）**を作れること。
- 書き出したファイルの名前を隣のファイルに書く案（`mix`）は取り下げた。

## API（Lily# 拡張の exports）

```ts
const lily = vscode.extensions.getExtension('yotsuda.lilysharp');
const api = await lily.activate();
if (api.stemsVersion !== 1) { /* 知らない版: 使わない */ }

const stems = await api.renderStems(lysUri, {
  score?: string,              // どの score（省略すると最初の score。プレビューと同じ）
  withoutRecording?: boolean,  // 隣に録音があっても使わない
  signal?: AbortSignal,
  onProgress?: ({ stem, done, total }) => void,   // 歌声は数十秒〜数分かかる
});
// stems = {
//   timeline: 'recording' | 'score',  // 'recording' なら全部が録音の秒の上（録音の 0 秒が 0）。
//                                     // 'score' なら楽譜の秒（書いたテンポ、lysc timeline と同じ秒）
//   stems: [{ kind: 'part' | 'voice' | 'recording' | 'recordingPart', name, file, at }],
//   notes: string[],                  // 作れなかったもの（歌える声が無い、録音が見つからない…）
// }

api.onDidChangeStems(uri => { /* その .lys の音が変わったかもしれない: renderStems を呼び直す */ });

await api.writeAudio({ left, right, sampleRate }, { format: 'mp3' | 'wav', path });
```

- **順**（依頼 5、`cad184c3b` から）: part は楽譜に並ぶ順（score の中の書いた順。score の無い譜面はパートの順）、voice はそのパートの直後
  （歌うだけのパートは声がその位置に）、recording と recordingPart は最後。
- **file**: 絶対パス。part は Lily# の保存場所の 32 bit float WAV（44.1 kHz）。voice は Cantoria の WAV（48 kHz）。
  recording と recordingPart は隣のファイルの `audio`・`parts` の元のファイル（m4a だけは Lily# が復号した WAV）。どれも webview の `decodeAudioData` で読める。
- **at**: そのファイルの標本 0 が鳴る時刻（timeline の秒）。**負もある**（歌声は子音のために 0.3 秒前から始まる）。
  鳴らし方の例: `src.start(t0 + Math.max(0, at), Math.max(0, -at))`。
- **音量**: part はプレビューのシンセのまま（正規化しない）なので、そのまま足せば釣り合う。足した後にリミッターを（Lily# のプレビューは master 0.9 と DynamicsCompressor）。
- **歌うだけのパート**は part に出ない（voice が代わりに鳴る）。声を作れなければ part に楽器で出て、`notes` に理由が書かれる。
  ⚠️ `singer on melody`（標準の書き方）は、パートが名指しした歌詞（`part melody { lyrics words }`）か、名前の無い歌詞（`lyrics { … }`）を歌う。
  名前の付いた歌詞をどのパートも名指ししていないときはエラー LYS4042 で、声は無い（パートに名指しするか `singer on melody sings words` と書く。`ed3c8a0fa` からはエラーの文がこの直し方を言う）。
- **録音の規則（Lily# と同じ）**: recordingPart が全部オンで 100% なら `recording`（元の録音）を鳴らし、1 本でもオフか 100% でないなら、
  オンの recordingPart の和を鳴らす（分離の誤差を避けるため。Lily# の recording-design.md §8）。
- **録音は伸ばさない**（2026-11-20 のオーナー決定）。小節を打っていない録音は `timeline: 'score'` になり、録音は stems に入らない（`notes` に理由）。
- **キャッシュ**: 同じ音と SoundFont のパートは描き直さないので、2 回目からは速い（実測: 1.4 s → 26 ms）。歌声も同じ鍵ならすぐ返る。
- **onDidChangeStems**: 開いた .lys の編集が 1.5 秒休んだとき、保存したとき、隣の `.orecchiante.json` が変わったとき。

## 最終のファイル（writeAudio）

混ぜるのは Shosa の webview に頼みたい。ミキサーの値で `OfflineAudioContext` に描き、2 チャンネルの Float32Array を拡張へ渡し、
`writeAudio` を呼んでほしい。こうすると、鳴っている音と書き出す音が同じになる。

- MP3 は 32000／44100／48000 Hz。LAME タグつきなので、MP3 の 0 秒が混ぜた音の 0 秒（ffmpeg と Chromium で 0 標本差を確かめた）。WAV は 16 bit、音量はそのまま。
- Lily# 側で混ぜない理由: 拡張（Node）には MP3／FLAC の復号器が無い。Windows の Media Foundation は糸 (Bank Band).mp3 を 16.5 ms 早く復号した。
- 動画（MP4）は、Shosa がこの音を映像に付ける。

## 確かめたこと

実物の VS Code（Lily# の Lab `sessions/p1339/stems-e2e`）で次を見た。
- 録音つきの 4 小節（録音の小節は 1.0／3.0／5.5／7.5／10.0 s、長さはまちまち）: ベースの音も歌声の音節も、録音の小節の秒の上に並ぶ。
- 録音なしの曲: `timeline: 'score'`。
- 編集で `onDidChangeStems` が来る。`writeAudio` の MP3 の 0 秒が正しい。

拡張は入れ直すまで古い（この API は Lily# の次の版から）。
