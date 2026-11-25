# Shosa 引き継ぎ (HANDOFF)

動きをテキストで書き、コンパイルして人形の動きにする言語。Lily# の動き版。
リポジトリは `C:\MyProj\Shosa`。このファイルは 2026-11-20 まで根の `CLAUDE.md` だった (ユーザー: ほかのプロジェクトと同じく `docs/HANDOFF.md` に)。
下のパスはリポジトリの根から。
- ★ **開始時に `C:\MyProj\Bamen\docs\repro\REQUESTS.md` の `open` を見る** (2026-11-23〜)。Bamen (撮影の言語。`shosa clip` を読んでカメラを解く) からの依頼の 1 枚。使い方は Shosa → Lily# の `docs/repro/REQUESTS.md` と同じ (入れたら `fixed <commit>`、入れないなら `won't`、聞くなら `question`)。1 は座る (と立ち上がる)、2 は曲がった道の歩き (急がない)。

## 編集のたびに固まらないように (2026-11-25、DESIGN.md 120)

ユーザー: 「.shosa を編集するとプレビューが更新されるまで少し固まる。Roslyn の red-green tree を参考にインクリメンタルビルドを」。測ると、固まりはプレビューのページ (編集 1 回 3.3 秒: 全コマ・全員ぶんの腕のめり込みよけのやり直し) だった。

- **プレビュー**: 変わったコマだけ腕を出し直す (`characters.js` `movedFrames`、`clearance.js` `elbowSwings(…, was, dirty)`)。3320 → 16 ms、全部やり直しも 3300 → 320 ms。結果は前と完全に同じ (照合済み)。
- **サーバー → 拡張 → ページ**: 最初だけ全部、あとは変わった行だけのパッチ (`Shosa.Lsp\ClipPatch.cs`、`preview\patch.js`、拡張が `clipPatches: true` と言う)。13 MB → 113 KB。パッチは前のものを書き換えず、変わらなかった物は同じオブジェクトのまま (下流はそれで使い回す)。
- **打っている間は解かない**: 並んだ編集は最後の本文で 1 回だけ解く (`Program.cs` の読むスレッド、`LanguageServer.Flush`)。
- **ソルバー**: コマごとの体幹を覚える (`SolvedAt`、`CoreOf`)。105 → 約 70 ms、出力は全部同じ。サーバーの編集 1 回は 210 → 約 90 ms。
- **決まり (120.6)**: ソルバーを速くする変更は `tools\perf --dump` / `--compare` で全部の数が同じことを確かめる (60 譜の控えは `out\pv\corpus`、作り直しは 120.6 の一覧)。「ほぼ同じ時刻」の使い回しは数が変わるので退けた (最大 4.5 mm)。
- 測る道具: `tools\perf` (サーバー、段ごと)、`out\pv\run-perf.ps1` [-Messages] (ページ、窓なしの Chrome)。
- **続き (2026-11-25 の 2 つ目のセッション、DESIGN.md 120.8〜120.11)**:
  - **語の場所だけが動いたクリップは解かない** (120.8): `ShosaCompilation.Compile(…, previous)`、`Shosa.Core\SolvedClip.cs`、`Solver.Moved`。コメント・空行・踊らないセクションの編集は 85 → 約 13 ms。figure は同じオブジェクトのまま、ソルバーの診断は語の新しい場所へ。
  - **ジャグリングの手の場所を時刻で引く** (120.9): `takeouts.shosa` のソルバーが編集 1 回 373 ms かかっていた (手のキーを頭から探すのが 1 コマ 1 万回) → 25 ms。`Juggling.Planned`。出力は全部同じ。**固まりを調べるときは、ユーザーがいま触っている譜でも測る**。
  - **長いクリップは並列に解く** (120.10): `Solver.PosedAhead`、`Solver.FramesAtOnce` (環境変数 `SHOSA_FRAMES_AT_ONCE` は `tools\perf` 用)。出力はどう切っても同じ (試験 `FramesAtOnceTests`)。静かな窓で測って上限を 6 本に決めた (ずんだもんとめたんの編集 1 回 73 → 56 ms。4〜6 本が一番速く、増やすと GC 待ちで遅くなる)。
  - **ソルバーの割り当てを減らした** (120.12): 段の読み取りを構造体に (`IChain<T>`、`ArmOf`・`LegShapeOf`・`GazeOf`…)。1 回解くのに 60 → 17 MB、ソルバー 1 本 68 → 47 ms、6 本 40 → 25 ms、編集 1 回 57 → 約 50 ms。型ごとの割り当ては `tools\perf --allocations FILE.shosa`。残りの半分はソルバーの外 (束縛、パッチと JSON)。
  - **束縛の割り当てを減らした** (120.13): 構文木の子の配列をノードごとに 1 回だけ作る (`SyntaxNode.ChildNodes` `ChildTokens`)、語の `Seg` の余分な写しをやめる。18.8 → 9 MB、束縛 15.7 → 6.4 ms、編集 1 回 51 → 43 ms。`tools\perf --allocations FILE.shosa --binder`、`--bind`。残りの根はセクションを何度も束縛すること (差分にするには設計が要る)。
  - **束縛のどこが重いかを調べて直した** (120.14、2026-11-25 の 3 つ目のセッション): 束縛の 45% は「セクションの長さを知るためだけの歩き」だった → 長さだけの歩き (`_mute > 0`) では語の中身を作らない。語の意味は語のノード・チャンネル・鏡写し・踊り手ごとに 1 回だけ求めて写しを渡す (`Meant`)。`Resolve` の勝者探しを掃き出しに。`q` の指す `{ }` を覚える。歩く仕組みの割り当てを減らす。束縛 2.78 → **1.10 ms**、8.7 → 3.1 MB、編集 1 回 41.7 → 36.9 ms、動きの変わらない編集 10.6 → **3.8 ms**。出力は 60 譜と壊した 503 譜で IR まで同じ。**束縛はもう固まりの原因ではない** (残り 1 ms。差分にしても得るのは最大それだけ)。
  - ★ **速さは温めてから測る** (120.14): 300 回の平均は JIT の温まり途中で、本当の数の 2〜3 倍だった (120.13 の「束縛 6.4 ms」は 2.8 ms)。`tools\perf` は 2〜3 秒温めてから測る (`--cold` で温めない)。プロファイル (`dotnet-trace`) は安全点でしか取れず割り当てる所に偏る: 木は使えるが、関数の中は `Stopwatch` を仮に入れて測る。
  - **束縛を速くする変更の確かめ方** (120.14): `--dump`/`--compare` が束縛の出力 (IR 全部) も `NAME.ir.json` に控えて比べる。加えて `python tools\perf\mutate.py FOLDER` で壊した譜を作り (samples などの写しの中に)、直す前の束縛で `--dump`、後で `--compare` (40 譜ずつ渡す)。
  - ★ **速さを測る前に、ユーザーに静かな窓を頼み、静かだと確かめてから始める** (ユーザー、2026-11-25)。出力の照合 (`--compare`) や試験には要らない。
- **ユーザーが見るのはこれから**。120.13 までは拡張に入っている (各ウィンドウで Reload Window)。**120.14 (束縛) はまだ拡張に入れていない**: C# を変えたので `tools\deploy-extension.ps1` (`-SkipPublish` なし) で入れ直す。残り: 編集 1 回 約 37 ms の大半はソルバー (6 本で約 28 ms) とパッチ、長いジャグリングの譜のプレビュー (120.3)。束縛の差分はもう要らない (120.14)。ソルバーを時間の窓で差分にするのは、いまの作りでは正しさの根拠が取れないのでしていない。
- Bamen の依頼 1 (座る)・2 (曲がった道) は `open` のまま (この 3 つのセッションは速さだけ)。

## 現在地 (2026-11-22 の終わり)

**試しの 1 曲を AI だけで振り付けた**: `scratch\Zunda-to-Metan-no-Jikoshokai.shosa` (曲は隣の `.lys`。`scratch\` は git に入っていない)。ずんだもんとめたんの 2 人、58 小節 108.75 秒、`check` は no problems。
歌う方が前に出て身ぶり・聞く方は揺れて相手を見る、サビは鏡写しのユニゾン (手拍子、指差し、お辞儀、「なのだ」「ですわ」の決め)、Bridge で向き合って右手をつなぐ、Coda で回って決めポーズ。
ユーザーの評: 「最初のバージョンとしては出色」「あとは背景とカメラワーク」。ただし「まだ生身の人間の動きには見えない、この方法で高度なダンスまでは難しい気がする」。

- **書いて分かった言語の穴**: 指 (ピース、ハート)、横並びで内側の手をつなぐ (同じ側の手どうししか合わせられない)、曲線に沿う歩き。定番の振り (ステップ・タッチ、お辞儀、指差し) を全部部品から定義した (語彙集があれば名前で済む)。誤りは語の順 (`X@sharp:N` → `X:N@sharp`)、大きな腕の `@sharp` が速すぎる (SH2003)、跳躍が高すぎる (SH2016) の 3 種。
- **VRM**: ユーザーが公式の無料モデルを落とした。`out\vrm\mini-zundamon.vrm`、`out\vrm\mini-zunko.vrm` (VRM 1.0 版の写し。元の zip の中身は `out\vrm\mini-zundamon\`、`out\vrm\mini-zunko\`)。口の形の名前は sample.vrm と同じ。ミニめたんは無いので **めたんの役は仮にミニずん子**。等身のめたんは公式で 3,300 円 (BOOTH、未購入)。
- **モデルの規約 (zip の `*_使用ライセンスvn3.pdf`)**: 個人は **非営利目的に限り**、YouTube への使用は許可、性的・暴力・政治宗教は不可、改変は要問い合わせ、クレジット不要。「営利」は「商用目的または対価を受け取るための行為」。zunko.jp のガイドラインは「YouTube の広告・スパチャ程度は非商用」と書くが、モデルの規約は別の文書なので、**収益化するなら SSS LLC. に問い合わせ** とユーザーに伝えた。
- **ほかのモデル (2026-11-22、ユーザーが `C:\Users\ytsud\Downloads\3Dmodel` に集めた)**: VRM 1.0 版を `out\vrm\` に写した: `zundamon.vrm` (ずんだもん人型 2025 ver.A1、頭 1.11 m の子どもの背丈)、`zunko.vrm` (東北ずん子 2023K、頭 1.35 m)、`zundamon-normal-walking.vrm` と `zundamon-normal-flying.vrm` (ずんだもんの通常の姿 = 餅のようなマスコット)。どれも口の形 (aa ih ou ee oh) と、上胸以外の骨がある。人型の 2 体は袖のコンストレイントもある。**規約は 4 つともミニと同じ** (個人は非営利に限り、YouTube 可、改変は要問い合わせ、調整と形式の変換は可、クレジット不要。VN3 の個別条件を読み比べた)。ほかの zip は Live2D、FaceRig/Animaze、コミPo、PowerPoint 用で、Shosa には使わない。
  - サビを人型の 2 体に載せて撮った (`out\edge\runs\models-full.mp4`。カメラ `-Cam 0,1.3,4.8 -At 0,0.75,0`): 手拍子・指差し・V・お辞儀がそのまま読め、2 頭身で出ていた腕のめり込みが無い。通常の姿は踊りに向かない (歩く型は高さ 0.29 m で腕が短い、飛ぶ型は休みの腰が 1.26 m にあり、three-vrm が腰の動きを拡大して画面の外へ浮く)。
- **2 頭身でのずれ**: Shosa は標準の大人の骨格で解き、プレビューは角度だけを VRM に移すので、腕が体や顔にめり込む所がある。ユーザーの指摘「ですわで腕が不自然に曲がる」→ `chin.elbow(side)` にした (VRM ではまだ見ていない)。

**モデルの着せ替え (2026-11-22、DESIGN.md 114)**: 設定 `shosa.models.folder` (下 3 段までの `.vrm`) と、`dancer` の行の VRM のパスの上で Ctrl+Shift+Up/Down (`shosa.nextModel` / `previousModel`、文脈キー `shosa.onModelPath`)。`.shosa` のフォルダーからの相対パスを `/` で書く。ホストの試験は通った。ユーザーが VS Code で押して見るのはこれから。端で折り返さない (ユーザー決定)。設定画面に Lily# と同じ形の「Choose a folder…」リンク。
**キャラクターの背丈 (DESIGN.md 115)**: `dancer … "zundamon.vrm" height 1.5m`。倍率 (`scale`) はユーザーが退けた (試行錯誤になる)。背丈は髪・耳を含む一番上まで。ついでに、プレビューで `at` の立ち位置がキャラクターの脚の倍率で縮んでいたのを直した。ユーザーが自分の曲で見るのはこれから。
**腕のめり込み (DESIGN.md 116)**: プレビューがキャラクターの胴と頭をメッシュから測り、毎コマ、まず肘を肩–手首の線のまわりに振り (手は動かさない)、残りは肩で外へ回す (30 度まで)。`preview\clearance.js`。ずん子とミニで撮って、埋まっていた手と前腕が出ることを確かめた。案 2 (キャラクターの体格で解く) は未着手。

**マイクを持って歌う (DESIGN.md 117)**: `dancer … sings melody mic right` (`left`、`both`)。歌うフレーズの間だけ持つ腕が口元 (優先)、間は行どおりにマイクを持って踊り、行が何も言わなければ腹の前で構える。歌の間に持つ腕へ書いた語は SH2018。プレビューはマイクを描き、指を握り、キャラクター自身の口へ腕を寄せる (`preview\mic.js`)。書き出しにマイクと指は入らない。ユーザーが VS Code で見るのはこれから (拡張を入れ直す)。試しは `out\mic\` と `out\pv\harness-mic.html`。

**ジャグリング (DESIGN.md 118)**: `dancer … juggles balls` (`clubs`、`rings`) と `juggle { every 1/2  3:4 | 531:3 3:1 | }`。サイトスワップはそのまま書く (`(4,2x)*`、`[32]1`)。くっついて書いた語は 1 つの投げの並びで、語の境目で宙の球が次のパターンに合うかを見る (SH2019、合わなければ切り替えの投げを勧める)。1 周以下の語は「つなぎ」(単独で正しくなくてよい)。部品は `Shosa.Core\Semantics\Siteswap.cs` (記法と正しさ)、`Shosa.Core\Motion\Juggling.cs` (時刻表、手の輪、球の放物線)、プレビューは `editors\vscode\src\preview\juggle.js`。見本 `out\juggle\juggle.shosa`、キャラクター付き `out\juggle\vrm.shosa`。ユーザーが VS Code で見るのはこれから (拡張を入れ直す)。技は `@mills` `@windmill` `@reverse` `@penguinCatch` `@clawCatch` `@backThrow` `@backCatch` `@underThrow` `@halfShower` (片手はいつも外から高く、もう片手は内から低く、118.48、見本 `out\juggle\halfshower.shosa`) `@takeouts` (118.49、2026-11-24: 423。払い始めに自分の側から 3 を相手へ投げ、中央へ落ちてくる自分の 4 を素早く払って反対側へ運び、一度止めて、球が額のすぐ前を通る大きな一回りで自分の側の外から下へ回し (2)、体の内側から次の 4 を投げ上げる。その下で相手の手が 4 を上げる。球の役割は固定: 右が払うのは球 0 (赤)、左は球 2 (黄)、外から投げる 3 は球 1 (青、テニスの球のように外から外へ高く)。4 は体の中心から投げて中心で受け、一回りの手は反対の手が 3 を投げ上げる時にその高さで自分の側の外を通る。一回りは曲線 1 本をなめらかな速さで回る。払って取る手のひらは外向き (`Style.Outward` = 5、プレビューも) で、投げの直前まで返さない。一回りの頂上と青の頂点、払って取る所と青を投げる所を、高さと時刻で組にしている (正面からは頂点近くの青と反対の手の 4 が重なって見える: 前後には 20 cm 離れる)。run の始めの持ち方で決める (`TakeoutBalls`)。一回りの底は低く (骨盤の 4 cm 上)、払って取る所は取る手の側へ 20 cm、青は高めに早く受けて下げて弧で振り上げ、投げた手は斜め上へ上がってから斜め下へ払って 4 を取る。一回りは顔の近く、4 は払った腕の下で交差して低い所から投げる (2026-11-24、ユーザーの指摘。腰を横へ傾けるのは払いを肘の弧にしてから不要になりやめた)。腕の下から 4 を投げる時、払った腕は持ったまま止まらず弧へ上がり始め、投げる腕も球と一緒に上がる (ほぼ同時、`TakeoutRoundStartSpeed`、`TakeoutUnder*`)。払って取るのは肘のあたりを中心にした平らな弧で、青を投げた外から肘を水平に回して体の近くへ (`SwipeAlong`、止まる所は体の中心)。その間、肘の x は肩から外 0.16 m に留める (`Juggling.SwipeHold` と Solver の `ElbowOut`、測る `out\juggle\swipeelbow.py`)。左右交互。動きはユーザーが何度も直して決めた。続けるには `423:4~@takeouts | :2 3:2 |` (小節ごとに 423 が頭からになるため)。見本 `out\juggle\takeouts.shosa`、測る `out\juggle\run-takeouts.ps1`。腕全体をなめらかに (2026-11-24 の最後、118.49 の終わり): 手の道をガウスでならす (`TakeoutSoften`)、一回りを外へ 0.50 m まで、払った手と相手の肘のすれ違いで減速しない (頂上を早める・止まる所を上げる・プレビューがキャラクターの腕を解き直す `juggle.follow`)。測る `out\juggle\armsmooth.py`・`handspeed.py`・`roundout.py`、`out\pv\passelbow.py`。続けて、一回りは顔の前の垂直面で前腕がその面を回る (ベスト・キッドの「拭く」、`TakeoutPlaneAhead`・`RoundPlane`・Solver の `RoundElbowAhead`・プレビューの `juggle.plane`、測る `out\juggle\foreplane.py`)。一回りの間、肘はゆっくり外へ (Solver の `RoundElbowFrom`/`To`・`RoundThrough`、プレビューの `PLANE_SIDE_SLACK`)。その後、肘を 95 度に保って肩で回す形に (Solver の `FixedElbowAngle`・`FixedElbowDown`、プレビューも同じ角度、118.49 の終わり)。ユーザーが見るのはこれから (拡張を入れ直す: プレビューも変えた)。その後、顔の前は腕の形そのもの (`Juggling.ArcOut`: 上腕水平で肩のまわりを外へ、前腕は真上、外で前腕を開いて下ろす)。2026-11-25、腕全体をなめらかに: Solver の `Pose` がテイクアウトの間だけ両腕を時間でならす (`SoftenTakeoutArms`、0.05 秒) と、下ろす時間を長く (118.49 の最後。測る `out\juggle\armflow.py`)。Burke's Barrage・Boston Mess・Rubenstein's Revenge・Chops は調べて解釈が要るので未着手、118.48 に要点) `@tennis` (テニス: 1 個だけ外から高く上を越えて往復、ほかは内から低く、118.45・118.46。重なりは正面から見た距離で測る `FRONT=1`、見本 `out\juggle\tennis.shosa`。ユーザーが見るのはこれから。名前の決まりは 118.41: 全体の技は通り名 1 語、片方だけの技は + Throw / Catch の camelCase。118.12、背中は 118.38・118.39、見本 `out\juggle\tricks.shosa`) と `@windmill` (118.13、見本 `out\juggle\windmill.shosa`)。441 で球どうしが触れていたのを直した (118.15: 同じ手に戻る投げは前の内で投げ、後ろの外で受ける。ユーザーが見るのはこれから)。クラブの手 (118.16: 受けて投げるのは手のひらが上、持つ間だけ少し返す、柄は親指と人差し指の間) とペンギン (118.17: 腕を伸ばし、肩を回して腰の少し後ろで受ける) を直した。ミルズメスの肩の揺れを止めた (118.23)。ミルズの 1 投目の球は、手が球の道に沿わず自分の道で横切って受ける (118.24)。3 投目も 1・2 投目と同じ向き (左から中心へ) に投げる。代わりに右手は右の外まで回らなくなった (118.25)。人形の上腕の内向きをゆるくし、プレビューの肘の角とめり込みよけの肩の回しを全コマぶん先に求めてならした (118.26。キャラクターの上腕の内向きは体格のせいで変わらない)。キャラクターの腕を測るのは `out\pv\run-elbows.ps1` と `out\pv\elbows.py`。背中と腰の傾きを逆にする案は前腕が突き抜けて退けた。1 投目の投げる所を左へ (表 0.42、届く範囲で頭打ち。118.27)。ペンギンの受けをさらに直した (118.22: 投げで腕を球と一緒に振り上げる。118.21: 受けた後の手を腰から離す。118.20: 肘を伸ばしきってから手首を回す、受ける側の肩が前。118.19: 回しすぎない・手首を戻してから肘。118.18: 肩 43 度、腕のねじりを増やし、手首を内へ折って手のひらを外、親指は後ろと外)。ユーザーが見るのはこれから。残り: 頭が球を見る、同期と非同期の切り替えの提案。**長さのない語は書いた投げそのまま** (`every 1/2  3 3 531 531 4 3`、`531` は 1 周だけ)、長さのある語は繰り返し、投げの格子は語をまたいで続く (118.47、2026-11-24。118.11 の格子の件も直った)。**未決: ほかの踊り手と合わせるには小節線が要る件は、ユーザーが後で検討する**。

**プレビューのカメラ (DESIGN.md 119、2026-11-23)**: フッターの Camera → Fit (F)、Zoom・Height・Angle、Reset。Fit は譜全体のキャラクターと宙の道具が入る一番近い距離へ。視点はページの状態に残り、MP4 もそこから撮る。ユーザーが VS Code で見るのはこれから (拡張を入れ直す)。ミルズは 118.12 の後半 (2026-11-23) で大きく直した: 球の面 0.33 m、1・2 投目は拍より早く投げて高さを揃える、肘は左右に留めて前腕が回る、めり込みよけの肘の振りは曲ごとに先に求めてならす (`clearance.js` `elbowSwings`)、ミルズの間はプレビューでキャラクターの腕を自分の体で解き直す (`arms.js`)。ずん子では肘の x はまだ 0.2 m ほど動く (体格の限界、118.12 の最後)。
**ミルズを手本にほかの技 (2026-11-23、DESIGN.md 118.28〜118.30)**: ウインドミルは背中を上の腕の側へ傾けたまま、上の手は球の道を横切って受け、表を届く所へ寄せ、上の手の輪は投げより下へ回らない (肘の加速度 19 → 12、前腕どうし 10 → 15 cm、プレビューで 2 種の投げの頂点を揃える)。リバースはミルズと同じ止まらない輪 (折れ 141 → 56〜97 度)。クローの直後のペンギンの受けは、自分の投げから 0.15 秒より短ければふつうに受ける (毎秒 11 m の跳びを直した)。測る道具 `out\juggle\windmill.py`・`wmframes.py`・`handacc.py`、撮る `out\pv\run-shots.ps1`。背中・足の下は止まらずに振り上げ、肘の向きを前後の時間で混ぜる (肘の加速度の最大 241〜272 → 74〜76。118.31、測る `out\juggle\window.py`)。球のカスケードも止まらない輪 (118.32。リングとクラブは前のまま。手の加速度の最大 97 → 68、ずん子の肘の加速度の最大 25 → 12、測る `out\juggle\whole.py`)。ウインドミルの腕の下からの球が上の手を突き抜けていた (118.33): 上の手は球が来る前に上を通って右肩へ戻り手前に畳み、肘を引く (`Juggling.WindmillPull`、クリップの `juggle.pull` でプレビューの腕も)。左手は空いた所を上がる。その後ユーザーの指摘を 3 回受けて、いまは: 上の手は腕の下からの投げの 0.12 秒後まで、投げた所より 30 cm 内 (踊り手の左) で受けた高さに待ち、前腕を体に寄せる (`WindmillPull`、人形は手を 18 cm 寄せ、プレビューは胴の前の縛りを外す)。球は上の手の右 (踊り手の右) を上がる (ずん子で 8〜10 cm、`out\pv\pass.py`、まとめて `out\pv\run-windmill.ps1`)。ミニはまだ (腕が短く、球が手の右 2 cm、球どうし 4.3 cm)。下の手 (左) は肘を止めて前腕を水車のように回す (118.34、`Juggling.Wheel`、クリップの `juggle.wheel`、プレビューは肘を止めた点へ。測る `out\juggle\wheel.py`)。ミニは前腕どうしが重なる (0.7 cm): 次の課題。上の手 (右) は投げの前に自分の側で下がって球と一緒に振り上げる (118.35)。上の手も円で回す案は 3 通り試して腕か球が当たり、ユーザーが「腕が重ならないのを優先」と決めたのでやめた。測る `out\juggle\ballarm.py`、`out\pv\ballarm.py`・`balls.py`・`fore.py` (`harness-elbows.html` が球の位置も書く)。その後、上の手は半円で左へ・まっすぐ右へ戻り、腕の下からの球は寄せた手へ体の側に投げる (118.36)。**ウインドミルはユーザーが「完成」と言った (2026-11-24)**。**投げたら目が宙の一番高い球を見る** (プレビューの `vrm.lookAt`)、とても高い球では首が 20 度まで後ろへ (人形、118.37、見本 `out\juggle\look.shosa`)。続けて、一回りは顔の前の垂直面で前腕がその面を回る (ベスト・キッドの「拭く」、`TakeoutPlaneAhead`・`RoundPlane`・Solver の `RoundElbowAhead`・プレビューの `juggle.plane`、測る `out\juggle\foreplane.py`)。一回りの間、肘はゆっくり外へ (Solver の `RoundElbowFrom`/`To`・`RoundThrough`、プレビューの `PLANE_SIDE_SLACK`)。その後、肘を 95 度に保って肩で回す形に (Solver の `FixedElbowAngle`・`FixedElbowDown`、プレビューも同じ角度、118.49 の終わり)。ユーザーが見るのはこれから (拡張を入れ直す: プレビューも変えた)。残り: リングとクラブはカスケードの「止まって振り上げる」まま (ユーザーが合わせた振り)。ミニのウインドミルは球どうし 5.3 cm (まだ触れる)。

**EDGE の実験は終わった (2026-11-22、ユーザー決定: EDGE には頼らない)**。曲だけ → 要所の固定 → 固定を増やす → 描き直し (SDEdit) → 固定を長く、の順に試した (DESIGN.md 110〜113)。ユーザーの評: 曲だけは「くらげ」、固定は痙攣するか、固定していない間に EDGE が学習データのヒップホップの振り (肩を張る、しゃがむ、あさっての方へ腕) を入れて「.shosa のとおりに動かす」意図に反する。**Shosa が一番良い** (「よく練習した宴会芸」)。「ダンスはタイミングよくポーズを決めることの連続」。道具 (`tools\edge\`) と環境 (`out\edge\`、約 20 GB) は残してある。

**次の一手 (未決定。ユーザーに聞く)**: 自然さは Shosa 自身で上げる。~~腕も決めを保ってから動く (DESIGN.md 113.3)~~ はユーザーが取らなかった: 「いまの決めポーズはそれほど悪くない。ぴったりのタイミングで一瞬でもぴたっと決めている。長く続けると戦隊ヒーローの登場シーンになる。実際のダンスでも完全に静止する期間はごくわずか」(113.4)。良し悪しを測る数は手首の反し (113.2) と手首の加速度 (112.2)、最後はユーザーの目。

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

試し方のメモ: **窓なしの Chrome は `--user-data-dir` のキャッシュから古い `dist\preview.js` を使うことがある**: プレビューを変えたら、撮る前にその場所を消す (DESIGN.md 118.10)。VRM の見え方は `out\pv\harness.html` (プレビューのページに言語サーバーの clip を流す。CSP は本物を写す) を窓なしの Chrome で。録音の再生と MP4 は `out\pv\harness-rec.html` (`tools\frame_server.py` は要求を記録しないので、Chrome を `--enable-logging=stderr` で起こしてページの誤りを見る。97.5)。音の時刻は ffmpeg と Orecchiante の `.venv` の numpy で (DESIGN.md 97.4)。ミキサーと混ぜた音は `out\pv\harness-stems.html` と `tools\measure_stems.py` (98.3)。

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
dotnet run -c Release --project tools\perf -- scratch\Zunda-to-Metan-no-Jikoshokai.shosa --find "up.fwd(15"   # 編集 1 回のコストを段ごとに (サーバー側。DESIGN.md 120.1)
tools\perf\bin\Release\net10.0\Shosa.Perf.exe --compare out\pv\corpus @(Get-ChildItem samples\*.shosa, scratch\*.shosa, out\juggle\*.shosa, out\vrm\*.shosa, out\mic\*.shosa, out\walkfeet\*.shosa, out\sing6\*.shosa | % FullName)   # ソルバーと束縛の出力が控えと同じか (速くする変更の前に --dump で控える。DESIGN.md 120.6、120.14)
tools\perf\bin\Release\net10.0\Shosa.Perf.exe --bind scratch\Zunda-to-Metan-no-Jikoshokai.shosa   # 束縛だけ、2 秒温めてから 1000 回 (中央値・最小・割り当て。DESIGN.md 120.14)。型ごとの割り当ては --allocations FILE --binder
out\pv\run-perf.ps1 [-Messages]   # プレビューのページで編集 1 回のコスト (窓なしの Chrome)。-Messages はサーバーの本物のパッチで (先に dotnet build -c Release tools\perf)
tools\pack-extension.ps1     # パッケージだけ: artifacts\extension\shosa-win32-x64-<版>.vsix (サーバーと shosa を .NET ごと同梱。Lily# は入れない。DESIGN.md 24 節。THIRD-PARTY-NOTICES.txt は tools\third-party-notices.ps1 が毎回作る。87.7)
```
