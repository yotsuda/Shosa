# Shosa 引き継ぎ (HANDOFF)

動きをテキストで書き、コンパイルして人形の動きにする言語。Lily# の動き版。
リポジトリは `C:\MyProj\Shosa`。このファイルは 2026-11-20 まで根の `CLAUDE.md` だった (ユーザー: ほかのプロジェクトと同じく `docs/HANDOFF.md` に)。
下のパスはリポジトリの根から。

## 現在地 (2026-11-22 の終わり)

**試しの 1 曲を AI だけで振り付けた**: `scratch\Zunda-to-Metan-no-Jikoshokai.shosa` (曲は隣の `.lys`。`scratch\` は git に入っていない)。ずんだもんとめたんの 2 人、58 小節 108.75 秒、`check` は no problems。
歌う方が前に出て身ぶり・聞く方は揺れて相手を見る、サビは鏡写しのユニゾン (手拍子、指差し、お辞儀、「なのだ」「ですわ」の決め)、Bridge で向き合って右手をつなぐ、Coda で回って決めポーズ。
ユーザーの評: 「最初のバージョンとしては出色」「あとは背景とカメラワーク」。ただし「まだ生身の人間の動きには見えない、この方法で高度なダンスまでは難しい気がする」。

- **書いて分かった言語の穴**: 指 (ピース、ハート)、横並びで内側の手をつなぐ (同じ側の手どうししか合わせられない)、曲線に沿う歩き。定番の振り (ステップ・タッチ、お辞儀、指差し) を全部部品から定義した (語彙集があれば名前で済む)。誤りは語の順 (`X@sharp:N` → `X:N@sharp`)、大きな腕の `@sharp` が速すぎる (SH2003)、跳躍が高すぎる (SH2016) の 3 種。
- **VRM**: ユーザーが公式の無料モデルを落とした。`out\vrm\mini-zundamon.vrm`、`out\vrm\mini-zunko.vrm` (VRM 1.0 版の写し。元の zip の中身は `out\vrm\mini-zundamon\`、`out\vrm\mini-zunko\`)。口の形の名前は sample.vrm と同じ。ミニめたんは無いので **めたんの役は仮にミニずん子**。等身のめたんは公式で 3,300 円 (BOOTH、未購入)。
- **モデルの規約 (zip の `*_使用ライセンスvn3.pdf`)**: 個人は **非営利目的に限り**、YouTube への使用は許可、性的・暴力・政治宗教は不可、改変は要問い合わせ、クレジット不要。「営利」は「商用目的または対価を受け取るための行為」。zunko.jp のガイドラインは「YouTube の広告・スパチャ程度は非商用」と書くが、モデルの規約は別の文書なので、**収益化するなら SSS LLC. に問い合わせ** とユーザーに伝えた。
- **2 頭身でのずれ**: Shosa は標準の大人の骨格で解き、プレビューは角度だけを VRM に移すので、腕が体や顔にめり込む所がある。ユーザーの指摘「ですわで腕が不自然に曲がる」→ `chin.elbow(side)` にした (VRM ではまだ見ていない)。

**次の一手 (ユーザーが EDGE を選んだ。ROADMAP 第 3 段階の「清書」)**: EDGE (github.com/Stanford-TML/EDGE、曲を入力に取るダンス生成。コードは MIT) を手元で動かす小さな実験。
1. まずサビの 8 小節 (15 秒) を曲だけで生成 (固定なし) して、動きの質をユーザーに見せる。
2. 次に Shosa の要所 (手拍子、指差し、お辞儀の瞬間、手つなぎ) を固定して清書し、「振付どおりで、Shosa より生身らしいか」を見てもらう。

調べてある障害:
- EDGE は PyTorch 1.12 と CUDA 11.6 に固定。この PC は RTX 5070 Ti (16 GB、Blackwell) で新しい PyTorch (2.7 以降) が要る → 移植。pytorch3d は Windows で入れにくい → WSL2 (入っている)。メモリ 63 GB。
- 曲の特徴は Jukebox (jukemirlib)。16 GB に収まるか未確認。README には学習データの前処理だけを指す `--extract-baseline` がある (推論で使えるかは `args.py` が未読)。
- 一部を固定する生成は論文とデモページにはあるが、README の使い方には無い (`EDGE.py`、`args.py`、`demo.ipynb` は未読)。無ければ拡散の穴埋め (生成の各段で固定する関節を上書き) を自分で足す。
- 入出力は SMPL。Shosa → SMPL と SMPL → VRM の変換を書く。1 人ずつしか作らないので、2 人の関係 (手つなぎ、指差し) は Shosa の値で固定し直す。
- 学習済みの重みと AIST++ の利用条件は未確認 (収益化する動画に使う前に確かめる)。
- 前の Kimodo の実験 (DESIGN.md 25.7): 要所を間引くと振付を守らず、全部渡すと Shosa をなぞるだけ。`out\kimodo\combo\generated.bvh` はユーザーがまだ見ていない。

## それまでの現在地 (2026-11-20 の終わり)

方向は「YouTuber に見せる」(ROADMAP 2 節): AI が作った曲や録音に合わせて VRM の女の子が歌いながら踊る動画。この日にしたこと (DESIGN.md 94〜97 節):

- **書いていない段は凍らない** (94 節): 息、体重の左右の寄り、腕に付いていく胴と頭。既定で入る。ユーザーが見て「だいぶ自然」、揺れを 1.6 倍に。試験のプロセスでは切る (`Solver.LifeByDefault`)。
- **手を合わせても貫通しない** (95 節): 書き出しの手の骨を、両手が近いと平行に。`clap` の手首を 5 cm。頭上から下ろすときの肘の裏返りを直した。
- **プレビューに VRM のキャラクター** (96 節): `dancer alice humanoid "girl.vrm" on lead` (ユーザーの案で `.shosa` に書く)。three-vrm 3.5.5。CSP に `blob:` (色が付かなかった)。ユーザーが VS Code で見た: 「すごい、とても面白い」。見本は `out\vrm\dance.shosa`。
- **録音から作った曲で踊る** (97 節): 曲の隣の `NAME.orecchiante.json` を Shosa が直接読み (ユーザー決定)、拍ごとのテンポで録音に合わせる (`Shosa.Core\Music\RecordingSidecar.cs`)。見本は `out\ito\ito.shosa` (糸 (Bank Band)、曲は OneDrive の絶対パス)。
- **音は Lily# で混ぜ、Shosa はそれを鳴らす** (97.3、ユーザー決定)。Lily# への依頼 3・4 を出した (`docs/repro/REQUESTS.md`): 書き出しが約 0.07% 伸びる件、歌声が録音の揺れに付いていかない件、相談として混ぜた音のファイル名を隣のファイルに。

- **プレビューと MP4 で録音を鳴らす** (97.5): 拡張が録音を読んで webview へ、`preview/recording.js` が `offset` から鳴らし、鳴っている間は録音が踊りの時計。MP4 の音は録音とサンプル単位で一致。ユーザーが `out\ito\ito.shosa` を VS Code で聞くのはこれから。

- **リリースの準備** (ROADMAP 第 4 段階): 最初の版は 0.3.0、win32-x64 だけ (ユーザー決定)。`editors\vscode\CHANGELOG.md` を書いた。出すのは本物の Lily# でミキサーを確かめてから。
- **Lily# の音を 1 本ずつ受け取り、プレビューのミキサーで混ぜる** (98 節): `src/stems.ts` (Lily# の exports の `renderStems`)、`preview/stems.js` (鳴らす・混ぜる・`OfflineAudioContext` で描く)、フッターの **Mixer**、ヘッダーの **Audio** (MP3／WAV を Lily# の `writeAudio` で)。録音とクリックの見立てで、MP4 と混ぜた音が 0 標本。

- **曲を読むのは Lily# の `lysc`** (92.1): `Shosa.Music` (`shosa-music`、GPL) をやめた。Shosa は全部 MIT、リポジトリだけでビルドできる。`lysc` は `SHOSA_LYSC` → PATH → VS Code の Lily# 拡張の順に探す。拡張は Lily# の exports の `lyscPath` を `SHOSA_LYSC` で渡す。前から落ちていた試験 1 件は見本 `turning.lys` が古い規則 (セクションでテンポが戻る) に頼っていたため。343 件全部通る。

**次の一手**: (1) ミキサーは本物の Lily# で動いた (オーナー、依頼 4 は done)。録音の行の並びは直した (98.4)。楽譜の行の並びは Lily# の依頼 5 で直り done (オーナーが確かめた)。次はリリースへ (ROADMAP 第 4 段階。README に「Preview features」の節を足した: VRM・録音・Mixer・Audio。Mixer と Audio に要る Lily# は 0.17.0 以上と README・CHANGELOG・Mixer の文言に書いた。残るのは Lily# 0.17.0 を Marketplace に先に出すこと (準備中) と、Shosa の vsce publish)。(2) 依頼 3 はユーザーが Lily# で書き出し直したら Shosa 側で測って done に。(3) 歌詞に合わせた口の動き: Shosa 側は済み (2026-11-21、DESIGN.md 99。`dancer … sings melody`)。依頼 6 は入り、本物の Lily# の lysc で `out\sing6\sing.shosa` (walkthrough の `first-song-ja-short.lys` の写し) を通し、`.vrma` を three-vrm で見て done に (DESIGN.md 99.4)。ま・ば・ぱの前で唇が閉じるようにした。残りはユーザーの目 (Lily# の拡張を `sung` のある版に入れ直してから VS Code のプレビューで)。(4) 歩きの足 (93.1): 前への歩は直した (2026-11-21、DESIGN.md 100: 後ろ足がかかとを上げ、腰は歩ごとにしゃがまない)。ユーザーに `out\walkfeet\feet.shosa` か `samples\walk.shosa` を VS Code のプレビュー (VRM) で見てもらう。横への歩と交差は測って直さなくてよかった (100.3)。書いていない腕は歩くと振れる (ユーザーが決めた。DESIGN.md 101)。歩くと腰は前の脚と回り、肩は逆へ回る (DESIGN.md 102)。ユーザーが見て「後ろ足が離れるのが遅い」→ 直した (DESIGN.md 103)。「運ぶ足のつま先が上を向きすぎる」→ 直した (DESIGN.md 104)。ユーザー「だいぶ自然になってきた」→ 膝の曲げの時刻を直した (DESIGN.md 105)。「腕は前が速く後ろが遅い、膝がぎくしゃく、振り出した膝が伸びるのが遅い」→ 直した (DESIGN.md 106)。「最初と比べるとだいぶ自然。余地があれば直して」→ 立つ脚が反対の足の通るときに伸びる (DESIGN.md 107)。立った所からの最初の歩の膝の段 (106.3、107.3) は調べて直さないことにした (DESIGN.md 108)。測るスクリプトの形は 106.1 (コマごとの膝と腕の角度)。ユーザーの次の目を待つ。(5) MP4 の形を選べる (View・16:9・9:16 の Shorts。DESIGN.md 109)。ユーザーの目はまだ。(6) Lily# 0.17.1 は出た (2026-11-21 に確認) ので、Shosa の公開 (vsce publish) はユーザーが決めれば出せる。詳しくは ROADMAP 2 節の「次の一手」。

試し方のメモ: VRM の見え方は `out\pv\harness.html` (プレビューのページに言語サーバーの clip を流す。CSP は本物を写す) を窓なしの Chrome で。録音の再生と MP4 は `out\pv\harness-rec.html` (`tools\frame_server.py` は要求を記録しないので、Chrome を `--enable-logging=stderr` で起こしてページの誤りを見る。97.5)。音の時刻は ffmpeg と Orecchiante の `.venv` の numpy で (DESIGN.md 97.4)。ミキサーと混ぜた音は `out\pv\harness-stems.html` と `tools\measure_stems.py` (98.3)。

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
