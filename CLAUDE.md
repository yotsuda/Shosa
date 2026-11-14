# Shosa

動きをテキストで書き、コンパイルして人形の動きにする言語。Lily# の動き版。

## 最初に読むもの

- `docs/ROADMAP.md` — 開発計画と現在地、次の一手。
- `docs/DESIGN.md` — 設計の決定とその理由、試験の結果。
- `docs/GRAMMAR_FOR_LLM.md` — 言語の全体 (語彙表から生成。手で直さない)。

## セッションの終わりの義務 (ユーザー決定、2026-11-14)

各セッションの終わりに、**次は会話を続けるほうが有利か、コンテキストをクリアして新しく始めるほうが
有利か**を、理由を添えて報告する。判断の材料:

- 次の作業が、この会話の中にしかない知識 (未記録の調査結果、試行錯誤の経緯) に頼るか。
- 会話が長くなり、要約や古いツール出力で文脈が劣化していないか。
- docs が現状に追いついていて、新しいセッションがそこから始められるか。

クリアを勧めるときは、その前に、会話の中にしかない知識を docs (上の 3 つ) に書き出しておく。

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
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\dance.shosa                   # 曲 (samples\song.lys) に合わせる。Lily# は Shosa.Music (shosa-music.exe) が読む。..\LilySharp の checkout が要る (DESIGN.md 19 節)
Shosa.Cli\bin\Debug\net10.0\shosa.exe midi samples\dance.shosa -o out\dance.mid   # 曲を踊りの順とテンポで。wav は --soundfont FONT.sf2
Shosa.Cli\bin\Debug\net10.0\shosa.exe check samples\turning.shosa                 # 途中でテンポと拍子が変わる曲 (samples\turning.lys)。DESIGN.md 21 節
Shosa.Cli\bin\Debug\net10.0\shosa.exe bvh samples\walk.shosa -o out\walk.bvh
Shosa.Cli\bin\Debug\net10.0\shosa.exe gltf samples\walk.shosa -o out\walk.glb    # blender_check.py は .glb も読む
Shosa.Cli\bin\Debug\net10.0\shosa.exe vrma samples\walk.shosa -o out\walk.vrma   # VRM モデルに載せて見る手順は DESIGN.md 10 節
& 'C:\Program Files\Blender Foundation\Blender 5.3\blender.exe' -b --python tools\blender_check.py -- out\walk.bvh out\blender\walk.clip.json out\blender\walk   # DESIGN.md 9 節
cd editors\vscode; npm run build; npm run check; npm test   # npm test はプレビューのシンセ (sound.js) の試験
code --extensionDevelopmentPath=C:\MyProj\Shosa\editors\vscode C:\MyProj\Shosa\samples   # 拡張を読み込んだ VS Code
tools\deploy-extension.ps1   # 拡張をパッケージにして、この PC の VS Code に入れる (-SkipPublish: サーバーは前回のまま。-NoPack: 入れるだけ)。あとは各ウィンドウで Reload Window
tools\pack-extension.ps1     # パッケージだけ: artifacts\extension\shosa-win32-x64-<版>.vsix (サーバーと shosa-music を .NET ごと同梱。DESIGN.md 24 節)
```
