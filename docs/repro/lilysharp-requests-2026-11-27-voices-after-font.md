# 依頼 7: サウンドフォントを替えると、プレビューの歌声が消えたまま戻らない（2026-11-27）

差出: Shosa のセッション。オーナーが Shosa の試験の途中で見つけた（Lily# 自身のプレビューで起きる）。

## 起きたこと（オーナー）

- `C:\MyProj\LilySharp-Lab\sessions\p1363\base-src\site\songs\Zunda-to-Metan-no-Jikoshokai.lys`（zunda と metan が歌う）を Lily# のプレビューで開き、
  設定 `lilysharp.playback.soundFont` を `C:\WINDOWS\System32\drivers\gm.dls` にした。
- 楽器は鳴るが、**歌声が鳴らない**。再生ボタンの上の**マイクが描かれない**。
- **サウンドフォントを元に戻しても（空 = Lily# Sound Font の MuseScore_General.sf3）、戻らない。** gm.dls に限らず、替えること自体が引き金らしい。

## Lily# の出力（`%APPDATA%\Code\logs\20261127T105604\window1\exthost\output_logging_*\1-Lily# Extension.log`）

```
10:57:05 zunda: to the engine / metan: to the engine
10:57:06 zunda sung (105.5 s) / metan sung (105.5 s)        ← ここまではマイクがあった
10:57:21 prepare (edit) …: sung: zunda, metan
Preview synth loading: fetching …/C%3A/WINDOWS/System32/drivers/gm.dls
Preview synth ready: 3.3 MB in 74 ms                         ← gm.dls 自体は読めている
10:57:25 prepare (play) …: sung: zunda, metan
Received message from webview: webviewBoot                   ← ページが作り直された
10:58:56 prepare (edit) …: sung: zunda, metan                ← 以後、声は送られない
10:59:16 prepare (edit) …: sung: zunda, metan
Preview synth ready: … MuseScore_General.sf3                ← 元に戻しても同じ
```

## 見立て（Shosa 側。確かめてはいない）

サウンドフォントが替わるとプレビューのページが作り直され（`webviewBoot`）、ページの `voices` は空になる。ところが
拡張の `singing.ts` の `postedVoices`（「a voice already there is not sent again」）はそのパネルに声を送り済みのまま
なので、`prepareVoices` が同じ key の声を二度と送らない。`postedVoices.delete(panel)` は文書が替わったとき
（`followVoices`）・エンジンが無いとき・パネルを閉じたときだけで、ページの作り直し（`extension.ts` の `webviewBoot` の
処理はログを 1 行書くだけ）では消えない。

回避: プレビューを閉じて開き直す（`forgetVoices`）。

## 頼みたいこと

ページが作り直されたら、そのパネルの「送った声」を忘れて、今の声を送り直す（マイクも元どおり）。直し方は Lily# 側で。
Shosa の Sound Font の設定は Lily# のものだけにした（オーナー決定 2026-11-26）ので、オーナーがサウンドフォントを試す機会は増える。
