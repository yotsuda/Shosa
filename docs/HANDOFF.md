# Shosa 引き継ぎ (HANDOFF)

動きをテキストで書き、コンパイルして人形の動きにする言語。Lily# の動き版。
リポジトリは `C:\MyProj\Shosa`。このファイルは 2026-11-20 まで根の `CLAUDE.md` だった (ユーザー: ほかのプロジェクトと同じく `docs/HANDOFF.md` に)。
下のパスはリポジトリの根から。

## 現在地 (2026-11-20 の終わり)

方向は「YouTuber に見せる」(ROADMAP 2 節): AI が作った曲や録音に合わせて VRM の女の子が歌いながら踊る動画。この日にしたこと (DESIGN.md 94〜97 節):

- **書いていない段は凍らない** (94 節): 息、体重の左右の寄り、腕に付いていく胴と頭。既定で入る。ユーザーが見て「だいぶ自然」、揺れを 1.6 倍に。試験のプロセスでは切る (`Solver.LifeByDefault`)。
- **手を合わせても貫通しない** (95 節): 書き出しの手の骨を、両手が近いと平行に。`clap` の手首を 5 cm。頭上から下ろすときの肘の裏返りを直した。
- **プレビューに VRM のキャラクター** (96 節): `dancer alice humanoid "girl.vrm" on lead` (ユーザーの案で `.shosa` に書く)。three-vrm 3.5.5。CSP に `blob:` (色が付かなかった)。ユーザーが VS Code で見た: 「すごい、とても面白い」。見本は `out\vrm\dance.shosa`。
- **録音から作った曲で踊る** (97 節): 曲の隣の `NAME.orecchiante.json` を Shosa が直接読み (ユーザー決定)、拍ごとのテンポで録音に合わせる (`Shosa.Core\Music\RecordingSidecar.cs`)。見本は `out\ito\ito.shosa` (糸 (Bank Band)、曲は OneDrive の絶対パス)。
- **音は Lily# で混ぜ、Shosa はそれを鳴らす** (97.3、ユーザー決定)。Lily# への依頼 3・4 を出した (`docs/repro/REQUESTS.md`): 書き出しが約 0.07% 伸びる件、歌声が録音の揺れに付いていかない件、相談として混ぜた音のファイル名を隣のファイルに。

- **プレビューと MP4 で録音を鳴らす** (97.5): 拡張が録音を読んで webview へ、`preview/recording.js` が `offset` から鳴らし、鳴っている間は録音が踊りの時計。MP4 の音は録音とサンプル単位で一致。ユーザーが `out\ito\ito.shosa` を VS Code で聞くのはこれから。

- **曲を読むのは Lily# の `lysc`** (92.1): `Shosa.Music` (`shosa-music`、GPL) をやめた。Shosa は全部 MIT、リポジトリだけでビルドできる。`lysc` は `SHOSA_LYSC` → PATH → VS Code の Lily# 拡張の順に探す。拡張は Lily# の exports の `lyscPath` を `SHOSA_LYSC` で渡す。前から落ちていた試験 1 件は見本 `turning.lys` が古い規則 (セクションでテンポが戻る) に頼っていたため。343 件全部通る。

**次の一手**: (1) Lily# の返事を `docs/repro/REQUESTS.md` で見る (依頼 1 は done)。(2) Lily# の依頼 3・4 は fixed (`695fe386a`。DESIGN.md 97.6)。3 はユーザーが Lily# で書き出し直したら Shosa 側で測って done に。4 で方針が変わった: Lily# の拡張の API から楽譜のパート・歌声・録音を 1 本ずつ受け取り、**Shosa のプレビューのミキサーで混ぜる** (API の案は Lily# から次の便。来たら `docs/repro/REQUESTS.md` に)。(3) 歌詞に合わせた口の動き (VRM の あ・い・う・え・お)。(4) 歩きの足 (93.1)。詳しくは ROADMAP 2 節の「次の一手」。

試し方のメモ: VRM の見え方は `out\pv\harness.html` (プレビューのページに言語サーバーの clip を流す。CSP は本物を写す) を窓なしの Chrome で。録音の再生と MP4 は `out\pv\harness-rec.html` (`tools\frame_server.py` は要求を記録しないので、Chrome を `--enable-logging=stderr` で起こしてページの誤りを見る。97.5)。音の時刻は ffmpeg と Orecchiante の `.venv` の numpy で (DESIGN.md 97.4)。

## 最初に読むもの

- `docs/ROADMAP.md` — 開発計画と現在地、次の一手。
- `docs/DESIGN.md` — 設計の決定とその理由、試験の結果。
- `docs/GRAMMAR_FOR_LLM.md` — 言語の全体 (語彙表から生成。手で直さない)。
- `docs/FOR_AGENTS.md` — Shosa を使う側の AI エージェント向けの導入手順 (手で書く。出力の欄や CLI を変えたら直す)。

## セッションの終わりの義務 (ユーザー決定、2026-11-14)

各セッションの終わりに、**次は会話を続けるほうが有利か、コンテキストをクリアして新しく始めるほうが
有利か**を、理由を添えて報告する。判断の材料:

- 次の作業が、この会話の中にしかない知識 (未記録の調査結果、試行錯誤の経緯) に頼るか。
- 会話が長くなり、要約や古いツール出力で文脈が劣化していないか。
- docs が現状に追いついていて、新しいセッションがそこから始められるか。

クリアを勧めるときは、その前に、会話の中にしかない知識を docs (上の 3 つ) に書き出しておく。

**判断を「クリア」に偏らせない** (ユーザーの指摘、2026-11-15: ほとんどいつもクリアを勧めていた):

- **docs に書き出したことは、クリアする理由にならない。** 書き出しは、クリアしても失うものを減らすだけ。
  クリアする理由は、続けると損をすること (文脈の劣化、要約で失われた細部、次の作業と無関係な大量の出力) だけ。
- **続ける利点を必ず数える**: 次の作業に要るコードやファイルを、この会話でもう読んでいるか。
  ユーザーが決めることへの答えを待っていて、答えが来ればすぐ実装に入れるか (案を書いた本人なら意図も説明できる)。
- 会話が短く、要約がまだ起きておらず、次の作業がいまの作業の続きなら、**続けるのが既定**。
- クリアを勧めるのは、たとえば次の場合: 要約が何度か起きた、次の作業がいまと別の領域、会話の前半の試行錯誤が誤った前提を残している。
- 報告では、続ける利点とクリアの利点を両方挙げてから結論を言う。

## 作業の決まり

- 語を足す・変えるときは `Shosa.Core/Semantics/Vocabulary.cs` の表と `Binder.cs` を直し、
  `shosa grammar -o docs/GRAMMAR_FOR_LLM.md` で文法書を作り直す (試験が一致を見張る)。
- 動きを変えたら `shosa sheet` の PNG を自分で見て確かめる。左が青、右が赤。
- 計画が変わったら `docs/ROADMAP.md`、設計の決定は `docs/DESIGN.md` に書く。

## コマンド

```
dotnet build Shosa.slnx         # dotnet test は Shosa.Cli を作り直さない。shosa.exe で確かめる前に必ず build する
dotnet test Shosa.slnx          # VS Code が Shosa.Lsp\bin\Debug を掴んでいて失敗するときは: dotnet build Shosa.Cli; dotnet test Shosa.slnx -c Release
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\fighter.shosa                    # score の一覧と長さ
Shosa.Cli\bin\Debug\net10.0\shosa.exe sheet samples\fighter.shosa -o out\fighter.png # score ごとに out\fighter-Idle.png … (名前のない score は out\fighter.png)。--score 名前 で 1 つ
Shosa.Cli\bin\Debug\net10.0\shosa.exe sheet samples\walk.shosa -o out\walk.png
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\dance.shosa                   # 曲 (samples\song.lys) に合わせる。Lily# の lysc が読む (SHOSA_LYSC、PATH、VS Code の Lily# 拡張の順に探す。DESIGN.md 92 節)
Shosa.Cli\bin\Debug\net10.0\shosa.exe midi samples\dance.shosa -o out\dance.mid   # 曲を踊りの順とテンポで。wav は --soundfont FONT.sf2
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\turning.shosa                 # 途中でテンポと拍子が変わる曲 (samples\turning.lys)。DESIGN.md 21 節
Shosa.Cli\bin\Debug\net10.0\shosa.exe bvh samples\walk.shosa -o out\walk.bvh
Shosa.Cli\bin\Debug\net10.0\shosa.exe gltf samples\walk.shosa -o out\walk.glb    # blender_check.py は .glb も読む
Shosa.Cli\bin\Debug\net10.0\shosa.exe vrma samples\walk.shosa -o out\walk.vrma   # VRM モデルに載せて見る手順は DESIGN.md 10 節
& 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe' -b --python tools\blender_check.py -- out\walk.bvh out\blender\walk.clip.json out\blender\walk   # DESIGN.md 9 節
& out\tools\godot\Godot_v4.7.2-stable_win64_console.exe --headless --path out\godot\proj -s C:\MyProj\Shosa\tools\godot_check.gd -- walk.glb C:\MyProj\Shosa\out\godot\walk.clip.json   # Godot での確認。先に --import。スクリプトとクリップは絶対パス (Godot の作業フォルダーはプロジェクト)。pwsh の MCP コンソールからは `cmd /d /s /c "… < NUL > log 2>&1"` で (入力待ちで固まる)。DESIGN.md 42 節と 43 節
& out\tools\godot\Godot_v4.7.2-stable_win64_console.exe --path out\godot\proj -s C:\MyProj\Shosa\tools\godot_view.gd -- fighter.glb C:\MyProj\Shosa\out\godot\combo.png --animation Combo --on character.glb --view side   # Godot が再生する姿のコマ (棒の骨格と、--on でキャラクター)。--headless では描けない。BoneMap は tools\godot_bonemap.gd。DESIGN.md 46 節
tools\godot_editor_probe.gd   # Godot のエディターの画面 (取り込みの画面、骨の対応) を開いて撮る。エディターのプラグインとして置く。使い方は頭のコメント。DESIGN.md 53.2
cd editors\vscode; npm run build; npm run check; npm test   # npm test はプレビューのシンセ (sound.js) の試験
code --extensionDevelopmentPath=C:\MyProj\Shosa\editors\vscode C:\MyProj\Shosa\samples   # 拡張を読み込んだ VS Code
tools\deploy-extension.ps1   # 拡張をパッケージにして、この PC の VS Code に入れる (-SkipPublish: サーバーは前回のまま。C# を変えたコミットの後には使わない: 古いサーバーが入る。DESIGN.md 97.5。-NoPack: 入れるだけ)。あとは各ウィンドウで Reload Window
tools\test-extension-host.ps1   # 入れた拡張を実際の VS Code (空のプロファイル、窓が数秒開く) で試す: Copilot 向けの道具 3 つ、保存前の編集、ターミナルの shosa。-Repository で editors\vscode の拡張。DESIGN.md 45 節
tools\pack-extension.ps1     # パッケージだけ: artifacts\extension\shosa-win32-x64-<版>.vsix (サーバーと shosa を .NET ごと同梱。Lily# は入れない。DESIGN.md 24 節。THIRD-PARTY-NOTICES.txt は tools\third-party-notices.ps1 が毎回作る。87.7)
```
