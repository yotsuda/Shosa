# Shosa 引き継ぎ (HANDOFF)

動きをテキストで書き、コンパイルして人形の動きにする言語。Lily# の動き版。
リポジトリは `C:\MyProj\Shosa`。このファイルは 2026-11-20 まで根の `CLAUDE.md` だった (ユーザー: ほかのプロジェクトと同じく `docs/HANDOFF.md` に)。
下のパスはリポジトリの根から。
- ★ **開始時に `C:\MyProj\Bamen\docs\repro\REQUESTS.md` の `open` を見る** (2026-11-23〜)。Bamen (撮影の言語。`shosa clip` を読んでカメラを解く) からの依頼の 1 枚。使い方は Shosa → Lily# の `docs/repro/REQUESTS.md` と同じ (入れたら `fixed <commit>`、入れないなら `won't`、聞くなら `question`)。2026-11-25: 1 (座る) は `fixed f9ae160`、2 (曲がった道) は `question` (曲線は `@turn` の歩でもう書けた。「この点まで歩く」の語が要るかを Bamen に聞いている)。`open` は無い。

## 曲から踊りへの導線、第 1 段 (2026-11-26、DESIGN.md 123)

方向 (ROADMAP 2 節の頭): 主な使い方は AI が `.lys` で作詞作曲した曲でキャラクターが歌いながら踊る。導線は 曲 → 歌 → 踊り → 動画 の 4 段で、第 1 段 (この日) が曲から踊りへ。第 2 段 Write a Song over These Chords… と第 3 段 Walkthrough・次の一手の表示は Lily# 側、第 4 段 Arrange an Accompaniment… (Lily#)。3 つの使い方 (AI の曲・既存の曲のコードに新しい歌・既存の曲で踊るだけ) は、どれも最後は「`.lys` に踊りを付ける」に集まるので、第 1 段から作った。

- **`shosa song FILE [--score NAME]`** (`SongReport.cs`) と AI の道具 `shosa_song` (`#shosaSong`、サーバー `shosa/songReport`): セクション (小節・拍・秒、繰り返しは `Chorus, verse 2`)、歌うパート、歌詞を小節ごとに `bar 2 (beat 20)  zunda+metan: 1い 1.5は … 3.5よ~1.5`。
- 文法書「Dancing to a song」に **「Choreographing the words」** (GrammarDoc.cs)。`check` が `X sings N syllables, from beat A to B` を言う。
- **Add a Dance…** (`.lys` の右クリック、`addDance.ts`・`danceText.ts`): score・歌うパートごとのキャラクター・どんな踊りかを聞き、`SONG.shosa` の骨組み (music・dancer … sings・score) を書いてプレビューを開き、Copilot のチャットに振り付けを頼む。**実際の VS Code で通して使っていない** (ユーザーの手で: 拡張を入れ直し、`.lys` を右クリック)。
- 書き手の試験 (123.4): サブエージェントが文法書と `shosa song` だけで Spring Breeze Bicycle に振りを付け、言葉に置いた 8 か所はみな狙った拍。指摘 5 つを直した。残りの指摘は 123.4 の「まだ」。踊りは `out\choreo\bicycle.shosa` (曲 `out\choreo\bicycle.lys` は Lily# の site の写し): ユーザーの目で見る候補。
- 試験 C# 564 件、拡張 32 件。**`tools\test-extension-host.ps1` は変更前から動かない** (31 秒で報告なし、VS Code 1.147.0): 調べていない。
- この日、ほかのリポジトリにも手を入れた (未コミット): Lily# の New File を 1 項目に (`Lily# Score (.lys)…` → Blank / Compose with AI… / Recording / Scan / MusicXML)、Compose with Copilot… → Compose with AI…、Sound Font と Cantoria に `extensionDependencies: yotsuda.lilysharp`。

## 最初のリリース 0.3.0 の準備 (2026-11-25 の 7 つ目のセッション)

ユーザー: 「最初のリリースの準備をして。GitHub の README に動画をのせたい。ふさわしい .shosa を書いて。私が VS Code で確認して動画にする」。

- **動画の譜 `samples\hello.shosa`**: 最初は曲なしの 8 小節のループを書いたが、ユーザー: 「Shosa の大きな特徴は音楽に合わせて figure を動かせるところだから、音楽があった方が良い」→ **ずんだもんとめたんの自己紹介の 30 秒の切り出し** (`form { Intro Chorus Coda }`、16 小節)。曲は Lily# の `site\songs\` の写しを `samples\Zunda-to-Metan-no-Jikoshokai.lys` に。振りは `scratch\` の試作のサビを英語の名前とコメントに直したもの (1 小節 1 語の `move`、セクションは `body` の段)。`check` は no problems、連続写真で見た。**ユーザーの目と録画はこれから**。
  - **歌声** (ユーザー: 「歌声シンセを入れて」): 曲には NEUTRINO の `singer` が 2 行あるが、踊りが自分の `form` を持つと拡張は歌声 (Lily# の stems) を使わない (`stems.ts`: 曲の順のままのときだけ)。そこで **曲の方を切った**: `samples\hello.lys` (写しの `form` を `Intro Chorus Coda` に)。`hello.shosa` は `form` を書かない。この PC の Lily# は 0.17.1 (フォルダー名は 0.16.0 のまま)。
  - **モデル** (ユーザー: 「規約を確認して、ずんだもんのモデルを。めたんは無償モデルがないので zunko で」): `dancer zundamon humanoid "models/zundamon.vrm"`、`dancer zunko humanoid "models/zunko.vrm" sings metan`。ファイルは `samples\models\` に置いたが **git には入れない** (`.gitignore` に `samples/models/*.vrm`、37 MB)。`samples\models\README.md` に出所と規約の要点。規約 (人型 2025 ver.A1 と ずん子 2023K の `*vn3license*` を全文読んだ、どちらも ver.2023.1 で同じ): 個人・法人とも非営利に限り可、映像作品・配信 (YouTube 含む) 可、未改変の再配布は無償に限り可、調整と形式の変換は可、改変は要問い合わせ、ソフトウェアへ組み込んで取り出せない形で配るのは要問い合わせ、性的・暴力・政治宗教は不可、クレジット不要。README の動画は「映像作品・配信」に当たり、Shosa は無償なので範囲内と読んだ。README にクレジットを 1 行足した。**読んでいないもの**: zunko.jp のキャラクターのガイドライン (ずん子をめたんの役に使うこと) と NEUTRINO の歌声ライブラリの規約 (クレジットの要否)。窓なしの Chrome で 2 体が踊るのは見た (`out\hello\shots\`)。
- **2 本目の動画の譜 `samples\juggle.shosa`** (ユーザー: 「ジャグリングの動作も紹介したい。hello の後ろに 1 人足すか、別の動画か」→ 別の動画を勧めて書いた): ずん子 1 人。最初は 11 小節 24 秒だったが、ユーザー: 「もっと複雑なルーティンとサイトスワップをばりばり入れて」→ **25 小節 52 秒、テンポ 116** (`@clawCatch` が 113 以上を要る。テイクアウトをユーザーが合わせたのは 110: 116 での見え方はユーザーの目で)。3 → リバース → 531 → 441 → (4) 51 → (2) 52512 → 55500 → (4) 7131 → (2) 4413 → 5241 → 45141 → テニス → ハーフシャワー → ミルズ → ウインドミル → テイクアウト → クロー → 同期の (4,2x)* → 止めてお辞儀。つなぎの投げ (かっこの中) は `check` の SH2019 が言ったとおり。球は 3 個のまま (1 つの段で個数は変えられない)。`check` は no problems、窓なしの Chrome で 4 コマ見た (全部の技は見ていない)。**見つけたこと (未修正)**: `3:4@halfShower` からすぐ `3:4@mills` へ続けると、書き出しの右手の骨が 1 コマで 54 度回る (`No_bone_flips` が落ちた、テンポ 116 の 802 コマ目)。譜では間に素のカスケードを 1 拍入れて避けた (`3:3@halfShower 3:1`)。README に 2 本目の置き場所のコメント。**ユーザーの目と録画はこれから**。
  - **4 人にした** (2026-11-25 の 8 つ目のセッション。ユーザー: 「ちょっとさびしいので、5 個ボール、クラブ、リングをまわす 3 人を追加。リングは少し遠めで 7 つ、クラブは 3 つでサイトスワップをばりばり」): 中央のずん子と左のずんだもん。最初はずん子が 3 個・左が 5 個だったが、ユーザー: 「中央のずん子が 5 個、ずんだもんは 3 個」→ 役を入れ替えた (役 `five` = ずん子、`every 1/3`: 645・744・75751・753・663・97531・7733・66661。役 `three` = `zundamon`、元の 3 個の譜のまま)、右に `clubber` (最初はクラブ 3 本を `every 3/4` で書いたが、ユーザー: 「クラブは 5 本回して」→ **5 本、`every 1/2`**: クラブは 0.45 m より低いとぶつかるので 4 以上の数だけ。5・645・744・7445・6455・64645)、後ろ 2.2 m に `ringer` (リング 7 枚、`every 1/3`。ユーザー: 「投げ方も少し変化を」→ 6 以上の数だけで 867・966・9667・8677・86867・96686、`7:4@halfShower` 2 小節と `7:4@reverse` 1 小節。リングの技は `check` が通り、止め絵でハーフシャワーの手が左右に開くのは見たが、動きは見ていない。立ち位置はユーザーの指示で `at 0.5 -2.2`: 正面からはずん子の袖に少し隠れる)。4 人とも 25 小節目でお辞儀。モデルは `../out/vrm/` (ユーザーが直して zunko・zundamon・sample・sample)。`check` は no problems (100 拍、51.72 秒)、窓なしの Chrome で 4 コマ見た (`out\pv\shots-four\`: 4 人が立って回している所まで。動きは見ていない)。**ユーザーの目と録画し直しはこれから**。コミットしていない。
  - **無料のモデル** (同じセッション。ユーザーが VRoid Hub の「歴代サンプルモデル」(登録者 Coatie、<https://hub.vroid.com/characters/4593660874193246717>) を見つけた): Vita のページを Chrome で読んだ: 説明に「全て CC0」、利用条件は商用・再配布・改変すべて OK、クレジット不要、VRM 1.0。同じキャラクターに Sendagaya_Shino・Sendagaya_Shibu・Darkness_Shibu・Vivi・Victoria_Rubin (それぞれのページは開いていない)。ユーザーが落としたのは **千駄ヶ谷 篠の `.vrm`** (`Downloads\3Dmodel\7989649190987387872.vrm`。ファイルの中の条件も全部可) → `out\vrm\shino.vrm` と `samples\models\shino.vrm` に写し、`clubber` に着せた (止め絵で見た)。ほかの 2 つの zip (ダークネス渋・千駄ヶ谷渋) は中身が `.vroid` (VRoid Studio の編集ファイル) で、そのままでは使えない: VRoid Studio で VRM に書き出すか、VRoid Hub から `.vrm` を落とす。`sample.vrm` の正体はファイルの中を読んで分かった: pixiv の `VRM1_Constraint_Twist_Sample` (© 2022 pixiv Inc.、商用・再配布・改変可、クレジット不要)。`samples\models\README.md` に 2 体の出所を足した (sample の配布元の URL は記憶から書いた: 未確認)。**未コミット** (`juggle.shosa`、`samples\models\README.md`)。README の録画は `clubber` が sample のときのもの。
  - **見つけたこと (未修正、譜で避けた)**: 5 個・クラブ 5 本・リング 7 枚の 3 人が `5:2 skip:2` (`7:2 skip:2`) で 98 拍目に止めると、お辞儀へ傾く胴 (`up:2 up.fwd(30):1`) と重なって、止まる直前 (97.5〜97.8 拍、1513〜1518 コマ目) に上腕が 1 コマで 48〜50 度回り、`No_bone_flips("juggle")` が落ちた (4 人にしたコミット `492175f` から。その時は全試験を走らせていなかった)。胴の段を外すと通る。3 個 (`3:2 skip:2`) は同じ書き方で通る。→ いったん 3 人を 1 拍早く止めて避け (`d403189`)、続けて **ソルバーを直した (DESIGN.md 118.50)**: 原因は、手の輪の中心が骨盤の前に固定で、傾いた肩が手の上へ来て手が肩から 0.17 m を通ること。両肩が骨盤より前へ出たぶん、輪の中心を前へ (`JuggleLeanFree`)。64 譜の `--compare` で変わったのは傾きながらジャグリングする 4 譜だけ。譜の最後は `5:2 skip:2` に戻した (README の録画と同じ譜)。試験 558 件。再現 `out\juggle\stopbow.shosa`、測る `out\juggle\stopbow.py`、控え `out\pv\corpus-lean` (この直しの前)。試験の文言に踊り手の名前を足した (`SkeletonTests.cs`)。**ユーザーの目はまだ** (最後のお辞儀で手が前へ出る)。拡張は入れ直した。
- **プレビューにキャラクターが残る件** (ユーザー: 「juggle.shosa から hello.shosa にタブを切り替えたら、ジャグラーの figure が残った」): `characters.js` の `dress` が、踊り手の数が減ったとき (2 人の譜 → 1 人の譜) に余った分を片付けていなかった (最初の VRM のコミットから。コメントだけあって中身が無かった)。残った 1 体は場面に立ったままになり、次に 2 人の譜へ戻ると 3 体見える。→ 余りを `undress`。窓なしのページでは再現を撮っていない (ハーネスは 1 クリップだけ): **ユーザーの目で確かめる**。
- **README の動画** (ユーザー: 「そのふたつを README にのせて」): ユーザーが書き出した `samples\hello.mp4` (32 MB)・`juggle.mp4` (50 MB、どちらも 1920×1080、git には入れない: `.gitignore`) を ffmpeg で 1280×720・CRF 26 に縮めて `docs\media\hello.mp4` (3.4 MB)・`juggle.mp4` (3.0 MB)、止め絵 `hello.jpg`・`juggle.jpg`。README の冒頭は止め絵 2 枚の表で、押すと mp4 のページへ行く。**ページの中で再生するプレーヤーにするには、GitHub の編集画面へ mp4 を落とす必要がある** (GitHub が自分の置き場の URL にだけプレーヤーを出す、という理解。ここでは確かめていない): README のコメントに書いた。
  - **その後** (同日): ユーザーが GitHub に 2 本を上げ、その URL (`github.com/user-attachments/assets/…`) を README の冒頭に置いた (見出し 1 行 + URL だけの行、の 2 組。URL は行に単独で置かないとプレーヤーにならない、という理解)。止め絵の表はやめた。`docs\media\` の mp4 と jpg は README からは使われなくなったが、残してある (消すかはユーザー次第)。
- **モデルのパスの探し方** (ユーザー: 「Ctrl+Shift+Up/Down で出てくるモデルは、設定のフォルダーからの相対パスであるべき」。DESIGN.md 114 の末尾): dancer の行のファイルは **`.shosa` のフォルダーから、なければ `shosa.models.folder` から** 探す (`models.ts` の `resolveModel`)。キーは設定のフォルダーからの短い名前で書く。言語サーバーは変えていない (文法書の 1 行だけ)。ホストの試験は通った (キーの書く形 3 つ)。**試験していない**: プレビューが短い名前のモデルを実際に着せること (`extension.ts` の `post`。型検査だけ) → ユーザーの目で。設定を替えた瞬間には引き直さない。`samples\juggle.shosa` の作業ツリーにはユーザーの `"../out/vrm/sample.vrm"` が未コミットで残っている (触っていない)。
- **文書**: CHANGELOG 0.3.0 に日付 (2026-11-25。出す日が変われば直す)、ジャグリング・歌う口とマイクを足した。拡張の README にも。根の README は Status を「0.3.0 が最初のリリース」にし、冒頭に動画を置く場所のコメント (`<!-- VIDEO: … -->`)、`samples/hello.shosa` への案内。FOR_AGENTS の「まだ公開していない」を直した。
- **README を書き直した** (2026-11-26、ユーザー: 「readme をあるべき形に」): 先頭の売りを「テキスト (または AI) で書き、キャラクターが曲に合わせて踊り・歌い・ジャグリングする。動画にも、ゲームエンジンへの書き出しにも」に。用途の順は 動画 → AI が書ける → ゲーム (固有の動きに限る、定番の歩きは素材集で) → 記譜法。例は wave をやめ hello と juggle の抜粋。「モーションキャプチャーほど自然ではない」と正直に書いた。節は Trying it (hello・モデル・juggle・AI に書かせる) を足し、拡張の節は短く。拡張の README は冒頭の 1 文だけ同じ向きに。Trying it に「自分の録音 (MP3) で踊る」= Orecchiante で `.lys` (小節線・コード・セクション、音符なし) を作り `music` で参照、を足した (ユーザー: 「それはすでにできる」。前は README に道が無かった)。**出す前に `tools\pack-extension.ps1` をもう一度**。
- **確かめた**: `dotnet test` 543 件、`npm test` 28 件、`tools\pack-extension.ps1` → `artifacts\extension\shosa-win32-x64-0.3.0.vsix` (36 MB)、それをこの PC に入れた (`deploy-extension.ps1 -NoPack`)。
- **残り (ユーザーの手)**: (1) `samples\hello.shosa` を見て録画 → GitHub の README の編集画面で `<!-- VIDEO -->` の所へ mp4 を落とす (GitHub がリンクを書く)。(2) push。(3) `vsce publish --packagePath artifacts\extension\shosa-win32-x64-0.3.0.vsix` (publisher `yotsuda`)。(4) タグ `v0.3.0` と GitHub のリリース (本文は CHANGELOG の 0.3.0、.vsix を添付)。README や CHANGELOG を直したら、出す前に `tools\pack-extension.ps1` をもう一度 (パッケージに入る)。

## 体の場所に置く手を、キャラクター自身の体へ (2026-11-25 の 7 つ目のセッション、DESIGN.md 122)

ユーザー: `hello.shosa` で「めたんの手が頭にめり込んでいる」→「自動で補正する機能があると良いかな」→ 方針を答えて「着手して」。

- **入れた**: 頭の場所の語 (`head` `chin` `forehead`) の手を、プレビューがキャラクター自身の頭 (メッシュから測ったあご・額・頭の横) へ寄せる。クリップに `touch` (手・場所・コマごとの重み、`Solver.Touches`)、プレビューは `preview\touch.js`。向きの語の手は動かさない (方針)。書き出しは変わらない。
- **確かめた**: ずん子・ずんだもん・ミニで補正なし/ありを撮って比べた (`out\touch\`、122.2)。`hello.shosa` の `listen` は `head` に戻した。試験 547 件と 28 件。拡張は入れ直した。
- **ユーザーの目はまだ**。**残り**: 胴の場所 (`chest` `hip` `belly` `shoulder`、`clap`)、向きの語で埋まった手を知らせること (122.2 の「まだ」)。
- `out\pv\corpus` の `juggle.takeouts`・`takeouts-test` の控えは 13:38 のテイクアウトの変更より前のもの: 次にソルバーを速くする前に `--dump` し直す。

## 座る: `height { sit(0.45m) }` (2026-11-25 の 5 つ目のセッション、DESIGN.md 121、Bamen の依頼 1)

- **語**: 高さの段の `sit(0.45m)` (かっこは床から座面まで。`sit` だけなら 0.45 m。0.1〜0.8 m)。ほかの高さ (`mid` など) で立つ。移りは高さの決まりどおり前の語の時間: `mid:2 then:2 sit(0.45m):4 then:4 then:2 mid`。
- **ソルバー** (`Solver.cs` の `SitAt`・`Core`・`TrunkAt`、`Rig.SeatHip`・`Rig.SeatBack`): 足はそのまま、腰が座面の 0.09 m 上・かかとの後ろ 0.45 m (椅子) へまっすぐ。途中は胴が前へ 28° まで傾く (`torso` の段の上に足す)。座っている間は体重の揺れを止め、書いていない腕と `down` は手を腿に置く (`ArmOf.Rest`)。座っている間・移りの間に足が動くと SH2024 (踊り手ごとに最初の 1 語)。
- **確かめた**: 連続写真 (`out\sit\`)、clip の数、2 人で違う座面、ずん子 (`out\sit\vrm.shosa` を `run-shots.ps1`)。試験 `SitTests` 8 件、全部で 530 件。前からの 60 譜は数も IR も同じ。見本 `samples\sit.shosa`。
- **ユーザーの目はまだ**。拡張は入れ直した (各ウィンドウで Reload Window して `samples\sit.shosa` をプレビュー)。
- 入れていない: 背もたれ・ひじ掛け、脚を組む、足が浮く高い椅子、床に座る。向き合って座る 2 人が同時に立つと、前かがみの頭が近づく (0.6 m 離れで重なりかけた。SH2007 は出なかった): Bamen の見本で気になれば、立つ時刻をずらすか `SitLean` を見直す。
- **曲がった道 (依頼 2)**: `leftFoot.fwd@turn(left(15))` の歩を続けると弧 (121.3)。文法書の「言えないこと」から外し「The feet」に書き方を足した。行き先を言う語 (「b の前まで歩く」) は Bamen の答え待ち。

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
  - **プレビュー: ジャグリングの figure も変わったコマだけ** (120.15、4 つ目のセッション): 全コマやり直しで 3 人 44 秒の `out\juggle\demo.shosa` は編集 1 回 約 450 ms かかっていた → 28 ms (テイクアウトは 65〜100 → 9〜18 ms)。`characters.js` の `movedFrames`・`juggledOtherwise`・`handsOf` (手に言うことは figure ごとに 1 回)・`millsOf`・`swingsOf` の `moved`・`trackPalms(…, was, moved)`。全部やり直した結果と数が完全に同じ (`out\pv\harness-perf.html` が手のひら・前腕・腰・肘の角度も比べる)。
  - **長さが変わる編集もパッチで** (120.16): 末尾に小節を足す・削る編集 (曲に合わせない譜ではいちばん多い) は、クリップ丸ごと送り直し・全コマやり直しだった → パッチに `counts` を足し、ページは前に無かったコマだけ「変わった」とする。テイクアウトの末尾に 2 秒足して 約 100 → 14〜20 ms。試験 `ClipPatchTests`・`patch.test.mjs`。
  - ★ 120.15・120.16 の数は **静かな窓の外** (CPU 13〜32%) で測った。差が大きいので結論は変わらない見込みだが、数を引くときは測り直す。
- **ユーザーが見るのはこれから**。120.16 まで拡張に入れ直した (2026-11-25 10:33 のサーバー。各ウィンドウで Reload Window)。残り: (1) 開いたとき・テンポを変えたとき・キャラクターを替えたときのページの全コマ (ずんだもんとめたん 320 ms、demo 約 450 ms)。(2) サーバーの編集 1 回 約 37 ms の大半はソルバー (6 本で約 28 ms: `Chain` が段の頭から語を探す所が候補。静かな窓でプロファイルしてから)。(3) `juggle` はパッチでも丸ごと送る (160〜260 KB、読むのは 1〜2 ms)。束縛の差分はもう要らない (120.14)。ソルバーを時間の窓で差分にするのは、いまの作りでは正しさの根拠が取れないのでしていない。
- Bamen の依頼 1・2 は 5 つ目のセッションで答えた (上の「座る」)。

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

**ジャグリング (DESIGN.md 118)**: `dancer … juggles balls` (`clubs`、`rings`) と `juggle { every 1/2  3:4 | 531:3 3:1 | }`。サイトスワップはそのまま書く (`(4,2x)*`、`[32]1`)。くっついて書いた語は 1 つの投げの並びで、語の境目で宙の球が次のパターンに合うかを見る (SH2019、合わなければ切り替えの投げを勧める)。1 周以下の語は「つなぎ」(単独で正しくなくてよい)。部品は `Shosa.Core\Semantics\Siteswap.cs` (記法と正しさ)、`Shosa.Core\Motion\Juggling.cs` (時刻表、手の輪、球の放物線)、プレビューは `editors\vscode\src\preview\juggle.js`。見本 `out\juggle\juggle.shosa`、キャラクター付き `out\juggle\vrm.shosa`。ユーザーが VS Code で見るのはこれから (拡張を入れ直す)。技は `@mills` `@windmill` `@reverse` `@penguinCatch` `@clawCatch` `@backThrow` `@backCatch` `@underThrow` `@halfShower` (片手はいつも外から高く、もう片手は内から低く、118.48、見本 `out\juggle\halfshower.shosa`) `@takeouts` (118.49、2026-11-24: 423。払い始めに自分の側から 3 を相手へ投げ、中央へ落ちてくる自分の 4 を素早く払って反対側へ運び、一度止めて、球が額のすぐ前を通る大きな一回りで自分の側の外から下へ回し (2)、体の内側から次の 4 を投げ上げる。その下で相手の手が 4 を上げる。球の役割は固定: 右が払うのは球 0 (赤)、左は球 2 (黄)、外から投げる 3 は球 1 (青、テニスの球のように外から外へ高く)。4 は体の中心から投げて中心で受け、一回りの手は反対の手が 3 を投げ上げる時にその高さで自分の側の外を通る。一回りは曲線 1 本をなめらかな速さで回る。払って取る手のひらは外向き (`Style.Outward` = 5、プレビューも) で、投げの直前まで返さない。一回りの頂上と青の頂点、払って取る所と青を投げる所を、高さと時刻で組にしている (正面からは頂点近くの青と反対の手の 4 が重なって見える: 前後には 20 cm 離れる)。run の始めの持ち方で決める (`TakeoutBalls`)。一回りの底は低く (骨盤の 4 cm 上)、払って取る所は取る手の側へ 20 cm、青は高めに早く受けて下げて弧で振り上げ、投げた手は斜め上へ上がってから斜め下へ払って 4 を取る。一回りは顔の近く、4 は払った腕の下で交差して低い所から投げる (2026-11-24、ユーザーの指摘。腰を横へ傾けるのは払いを肘の弧にしてから不要になりやめた)。腕の下から 4 を投げる時、払った腕は持ったまま止まらず弧へ上がり始め、投げる腕も球と一緒に上がる (ほぼ同時、`TakeoutRoundStartSpeed`、`TakeoutUnder*`)。払って取るのは肘のあたりを中心にした平らな弧で、青を投げた外から肘を水平に回して体の近くへ (`SwipeAlong`、止まる所は体の中心)。その間、肘の x は肩から外 0.16 m に留める (`Juggling.SwipeHold` と Solver の `ElbowOut`、測る `out\juggle\swipeelbow.py`)。左右交互。動きはユーザーが何度も直して決めた。**テイクアウトはユーザーが「いったん完了」とした (2026-11-25)**。越える球 (3) は受けてから下げて振り上げて投げる (その間だけ手のならしを弱める `TakeoutCrisp`。測る `out\juggle\throwup.py`)。続けるには語を並べる: 同じパターンのテイクアウトの語が続けば 423 は語をまたいで続く (`423:4@takeouts 423:4@takeouts`、2026-11-25。前は語ごとに頭からで `423:4~@takeouts | :2 3:2 |` と書く必要があった)。見本 `out\juggle\takeouts.shosa`、測る `out\juggle\run-takeouts.ps1`。腕全体をなめらかに (2026-11-24 の最後、118.49 の終わり): 手の道をガウスでならす (`TakeoutSoften`)、一回りを外へ 0.50 m まで、払った手と相手の肘のすれ違いで減速しない (頂上を早める・止まる所を上げる・プレビューがキャラクターの腕を解き直す `juggle.follow`)。測る `out\juggle\armsmooth.py`・`handspeed.py`・`roundout.py`、`out\pv\passelbow.py`。続けて、一回りは顔の前の垂直面で前腕がその面を回る (ベスト・キッドの「拭く」、`TakeoutPlaneAhead`・`RoundPlane`・Solver の `RoundElbowAhead`・プレビューの `juggle.plane`、測る `out\juggle\foreplane.py`)。一回りの間、肘はゆっくり外へ (Solver の `RoundElbowFrom`/`To`・`RoundThrough`、プレビューの `PLANE_SIDE_SLACK`)。その後、肘を 95 度に保って肩で回す形に (Solver の `FixedElbowAngle`・`FixedElbowDown`、プレビューも同じ角度、118.49 の終わり)。ユーザーが見るのはこれから (拡張を入れ直す: プレビューも変えた)。その後、顔の前は腕の形そのもの (`Juggling.ArcOut`: 上腕水平で肩のまわりを外へ、前腕は真上、外で前腕を開いて下ろす)。2026-11-25、腕全体をなめらかに: Solver の `Pose` がテイクアウトの間だけ両腕を時間でならす (`SoftenTakeoutArms`、0.05 秒) と、下ろす時間を長く (118.49 の最後。測る `out\juggle\armflow.py`)。同日 6 つ目のセッション、一回りで手の奥行きが跳ねる件 (ユーザー) を直した: 本命はプレビュー (`arms.js` が手を胴の前へ押し出す分を胴の横 6 cm で急にやめていた → `BEFORE_TRUNK` 0.45 m かけて)、人形は `ArcOut` の回し・開き・下ろしを重ねる (`EasedRamp`) と、奥行きだけ長めにならす (`TakeoutDepthSoften` 0.08 秒)。続けて、顔の前を通る手を高く (`ArcDrop` -8 度、`TakeoutStopDown` -0.28: 頂上 0.77 → 0.81 m)。測る `out\juggle\zflow.py`、`out\pv\zworked.py` (キャラクター、`run-elbows.ps1 -Clip` で前後)。その次の指摘 (赤を低く、反対の手と同時に上がる、払った腕の軸の回転): 4 を拍の 0.3 遅く放す (`TakeoutHighLater`: 頂点 0.86 → 0.75 m、青 0.85・左手の球の弧 0.90 より下)、下ろす間は肘を腕の真下へ (`ElbowAside`: 上腕の軸まわり 606 → 413 度/秒、わきが閉じる)。測る `out\juggle\redblue.py`・`armtwist.py`・`elbowpole.py`。さらに次の指摘 (払う 2 つの球がお互いを回る軌跡をはっきり、ミルズのように腕を交差): 下から投げる所を反対側へ (`TakeoutUnderCross` 0.28、`TakeoutHighAhead` 0.14、`TakeoutUnderHigh` 0)、上の腕を反対側まで運ぶ (`TakeoutCarryTo` 0.20、`ArcFrom` -50)。腕の届く範囲で頭打ち (手は中心から 13 cm 向こう)。測る `out\juggle\orbit.py` (軌跡の絵 `out\juggle\orbit.png`)。次の手は胴を交差の側へ回すこと。続けて「払って取った後、手を反対側の下の方へもう少し伸ばす」: `TakeoutCarryTo` 0.30・`TakeoutCarry` 0.14・`TakeoutStopDown` -0.20、`ArcOutFor` 0.27・`ArcRampFor` 0.16 (手は中心の 9 cm 向こう、8 cm 低い所で折り返す)。テイクアウトの並びが行の終わりや休みで終わると最後の手がぐるぐる回る件も直した (`_takeoutNext` を「投げるはずだった時刻」に。`423:3@takeouts` を並べて書くのは正しく、ほかは同じ動き: 測る `out\juggle\cycles.py`)。★ **色**: ユーザーは払わない球 (球 1、プレビューでは青) を「黄」、払うもう片方 (球 2、黄) を「青」と呼ぶ: 役で話す。(下の問いはこれで解けた: 「青をもつ反対の手」= もう片方の払う球を持つ手。) 前に聞いていたこと: ユーザーの言う「青をもつ反対の手」は、赤を投げる時に左手が持っている黄のことか、1 拍前に投げて赤の上を越える青のことか (青の投げを赤と同時にするのは 423 ではできない。118.49 の最後)。顔の前の弧のわきの開き (上腕水平) は手の高さと引き換えなのでそのまま。**ユーザーの目はまだ** (拡張は入れ直した)。もっと高くするなら肘の角度 (いま 84〜99 度) との兼ね合い: 前腕を真上のまま上腕を上げると肘が開く。Burke's Barrage・Boston Mess・Rubenstein's Revenge・Chops は調べて解釈が要るので未着手、118.48 に要点) `@tennis` (テニス: 1 個だけ外から高く上を越えて往復、ほかは内から低く、118.45・118.46。重なりは正面から見た距離で測る `FRONT=1`、見本 `out\juggle\tennis.shosa`。ユーザーが見るのはこれから。名前の決まりは 118.41: 全体の技は通り名 1 語、片方だけの技は + Throw / Catch の camelCase。118.12、背中は 118.38・118.39、見本 `out\juggle\tricks.shosa`) と `@windmill` (118.13、見本 `out\juggle\windmill.shosa`)。441 で球どうしが触れていたのを直した (118.15: 同じ手に戻る投げは前の内で投げ、後ろの外で受ける。ユーザーが見るのはこれから)。クラブの手 (118.16: 受けて投げるのは手のひらが上、持つ間だけ少し返す、柄は親指と人差し指の間) とペンギン (118.17: 腕を伸ばし、肩を回して腰の少し後ろで受ける) を直した。ミルズメスの肩の揺れを止めた (118.23)。ミルズの 1 投目の球は、手が球の道に沿わず自分の道で横切って受ける (118.24)。3 投目も 1・2 投目と同じ向き (左から中心へ) に投げる。代わりに右手は右の外まで回らなくなった (118.25)。人形の上腕の内向きをゆるくし、プレビューの肘の角とめり込みよけの肩の回しを全コマぶん先に求めてならした (118.26。キャラクターの上腕の内向きは体格のせいで変わらない)。キャラクターの腕を測るのは `out\pv\run-elbows.ps1` と `out\pv\elbows.py`。背中と腰の傾きを逆にする案は前腕が突き抜けて退けた。1 投目の投げる所を左へ (表 0.42、届く範囲で頭打ち。118.27)。ペンギンの受けをさらに直した (118.22: 投げで腕を球と一緒に振り上げる。118.21: 受けた後の手を腰から離す。118.20: 肘を伸ばしきってから手首を回す、受ける側の肩が前。118.19: 回しすぎない・手首を戻してから肘。118.18: 肩 43 度、腕のねじりを増やし、手首を内へ折って手のひらを外、親指は後ろと外)。ユーザーが見るのはこれから。残り: 頭が球を見る、同期と非同期の切り替えの提案。**長さのない語は書いた投げそのまま** (`every 1/2  3 3 531 531 4 3`、`531` は 1 周だけ)、長さのある語は繰り返し、投げの格子は語をまたいで続く (118.47、2026-11-24。118.11 の格子の件も直った)。**未決: ほかの踊り手と合わせるには小節線が要る件は、ユーザーが後で検討する**。

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
