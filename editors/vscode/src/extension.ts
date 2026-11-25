import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { LanguageClient, ServerOptions, State, TransportKind } from 'vscode-languageclient/node';
import { registerExport } from './export';
import { registerModels, resolveModel } from './models';
import { applyClipPatch } from './preview/patch.js';
import { ClipMusic, createStems } from './stems';
import { registerTools } from './tools';

// The extension is thin on purpose. The language server knows the language; the preview
// knows how to draw. This file starts the one, opens the other, and carries four kinds of
// message between editor and preview:
//   server  -> preview : the solved movement, after every edit           (shosa/clip)
//   preview -> server  : the notes of the song a clip is danced to, when the preview asks (shosa/song)
//   editor  -> preview : where the cursor is, so the preview shows the clip of that section
//                        and that word's pose
//   editor -> preview  : the bytes of the character files the dancers are dressed in (VRM)
//   preview -> editor  : which words are being performed (to light them up), and which
//                        word to go to when a limb is clicked
// Every position is an offset into the document in UTF-16 code units: what VS Code's
// offsetAt / positionAt count, and what the server's spans are (a C# string index).

interface ClipMessage {
  uri: string;
  version: number;
  /** The first clip (an empty one when the file has none). */
  clip: unknown;
  /** Every clip of the file: without a `form`, one per section. A figure may name a character file (`model`). */
  clips?: unknown[];
  /** The number of this message among the file's. */
  seq?: number;
  /** Instead of `clips`: what an edit changed in the clips of message `base` (preview/patch.js). */
  patch?: unknown;
  base?: number;
}

/**
 * What a new file starts as: one bar of walking, with everything a file cannot leave out
 * (a role, its dancer, a section, the form, the score). `shosa check` passes it.
 */
const newFileTitle = 'Untitled';
const newFileText = `title "${newFileTitle}"
tempo 100
time 4/4

role solo
dancer solo humanoid on solo

section Main {
  solo {
    steps { leftFoot.fwd rightFoot.fwd leftFoot.fwd stand | }
  }
}

form { Main }
score { dancer solo }
`;

let client: LanguageClient | undefined;
let panel: vscode.WebviewPanel | undefined;
let panelUri: string | undefined;
const clips = new Map<string, ClipMessage>();

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const performed = vscode.window.createTextEditorDecorationType({
    backgroundColor: new vscode.ThemeColor('editor.findMatchHighlightBackground'),
    borderRadius: '3px',
  });
  context.subscriptions.push(performed);

  // File > New File... > Shosa File: an untitled file that already compiles, so the preview
  // has something to perform. Registered before the server is looked for: it needs none.
  context.subscriptions.push(vscode.commands.registerCommand('shosa.newFile', async () => {
    const document = await vscode.workspace.openTextDocument({ language: 'shosa', content: newFileText });
    const editor = await vscode.window.showTextDocument(document);
    // The title is the one placeholder: selected, so the first keystroke names the piece.
    const start = newFileText.indexOf(newFileTitle);
    editor.selection = new vscode.Selection(document.positionAt(start), document.positionAt(start + newFileTitle.length));
  }));

  // Ctrl+Shift+Up/Down on the file of a dancer line: the character before or after it in shosa.models.folder (models.ts). Needs no server.
  registerModels(context);

  // The compiler the package carries (`shosa check`, `shosa sheet`, `shosa gltf` …) on the PATH
  // of VS Code's terminals: an AI agent working in one runs it by its name, whatever folder this
  // version of the extension was installed into. Running from the repository there is none here.
  const programs = path.join(context.extensionPath, 'server');
  // A package made on Windows has no execute bits (a zip written there carries none), so on
  // Linux and macOS the programs would be there and not start. Set them; a failure (a read-only
  // install) is left to show itself when the server is started.
  if (process.platform !== 'win32') {
    for (const program of ['Shosa.Lsp', 'shosa']) {
      try {
        fs.chmodSync(path.join(programs, program), 0o755);
      } catch {
        // not there (running from the repository), or not ours to change
      }
    }
  }
  if (fs.existsSync(path.join(programs, process.platform === 'win32' ? 'shosa.exe' : 'shosa'))) {
    context.environmentVariableCollection.description = 'The Shosa compiler (`shosa`)';
    context.environmentVariableCollection.prepend('PATH', programs + path.delimiter);
  } else {
    context.environmentVariableCollection.clear();
  }

  const server = findServer(context);
  if (!server) {
    void vscode.window.showErrorMessage(
      'Shosa: the language server was not found. Build it with "dotnet build", or set shosa.server.path.');
    return;
  }

  // Songs are read by Lily#'s lysc (DESIGN.md 92): the one the Lily# extension names, handed to the server and
  // to `shosa` in the terminals as SHOSA_LYSC. Lily# is a loose dependency: without it the server says, on a
  // `music` line, that it is needed; everything else works.
  const lysc = await lyscOfLilySharp();
  if (lysc) {
    context.environmentVariableCollection.replace('SHOSA_LYSC', lysc);
  } else {
    context.environmentVariableCollection.delete('SHOSA_LYSC');
  }
  const serverOptions: ServerOptions = {
    ...server,
    transport: TransportKind.stdio,
    options: lysc ? { env: { ...process.env, SHOSA_LYSC: lysc } } : undefined,
  };
  const editorFor = (uri: string | undefined) =>
    vscode.window.visibleTextEditors.find(e => e.document.uri.toString() === uri);

  // The preview is given every clip and where the cursor is; it shows one clip, and uses
  // the cursor to choose it when it has no clip of its own to stay on.
  // After an edit it is given what changed (`patch`, made on the clips of message `base`) and puts it into the
  // clips it has: megabytes of frames are not copied to the page again for a word that moved two bars (DESIGN.md 120).
  const post = (message: ClipMessage, reset: boolean, patch?: { patch: unknown; base: number }) => {
    const editor = editorFor(message.uri);
    const shown = message.clips ?? (message.clip ? [message.clip] : []);
    // The characters the dancers are dressed in (`dancer alice humanoid "girl.vrm"`): from the file's own folder,
    // or else from the models folder (models.ts).
    const models: Record<string, string> = {};
    // The recordings the songs keep time with (`X.orecchiante.json` beside `X.lys`: the server gives the full path).
    const recordings = new Set<string>();
    // The songs, to be had from Lily# one stem at a time for the preview's mixer (stems.ts).
    const musics: ClipMusic[] = [];
    for (const clip of shown as { figures?: { model?: string }[]; music?: ClipMusic }[]) {
      if (clip.music?.file) {
        musics.push(clip.music);
      }
      for (const figure of clip.figures ?? []) {
        if (figure.model && !(figure.model in models)) {
          const found = resolveModel(figure.model, vscode.Uri.parse(message.uri));
          models[figure.model] = found.file;
          lookedIn.set(found.file, found.tried);
        }
      }
      if (clip.music?.recording?.audio) {
        recordings.add(clip.music.recording.audio);
      }
    }
    void panel?.webview.postMessage({
      ...(patch ? { type: 'patch', patch: patch.patch, base: patch.base } : { type: 'clip', clips: shown }),
      seq: message.seq,
      models,
      offset: editor ? editor.document.offsetAt(editor.selection.active) : -1,
      reset,
    });
    for (const file of new Set(Object.values(models))) {
      void sendFile('model', file);
    }
    for (const file of recordings) {
      void sendFile('recording', file);
    }
    stems.want(vscode.Uri.parse(message.uri), musics);
  };

  // A character file or a recording is read here and its bytes handed to the page, which parses it: the page
  // need not be let read the folder it is in. Sent again when the file changes (it is checked at every edit of the dance).
  const sentModels = new Map<string, number>();   // type and file -> the time it was last written, as sent
  const lookedIn = new Map<string, string[]>();   // a character's file -> every place it was looked for in
  const sendFile = async (type: 'model' | 'recording' | 'stem', file: string) => {
    let written = -1;
    try {
      written = (await fs.promises.stat(file)).mtimeMs;
    } catch {
      // not there: the page says so
    }
    if (sentModels.get(type + '|' + file) === written) {
      return;
    }
    sentModels.set(type + '|' + file, written);
    if (written < 0) {
      const places = (type === 'model' && lookedIn.get(file)) || [file];
      void panel?.webview.postMessage({ type, file, error: `${places.join(' and ')} ${places.length > 1 ? 'were' : 'was'} not found.` });
      return;
    }
    try {
      const data = await fs.promises.readFile(file);
      void panel?.webview.postMessage({ type, file, data: new Uint8Array(data.buffer, data.byteOffset, data.byteLength) });
    } catch (error) {
      void panel?.webview.postMessage({ type, file, error: `${file} could not be read: ${String(error)}` });
    }
  };

  const stems = createStems(context, {
    post: message => void panel?.webview.postMessage(message),
    sendFile: file => void sendFile('stem', file),
  });

  // A dance follows its song, and the files of parts it brings in with `using`: when a .lys or
  // a .shosa is saved, the server compiles the dances again.
  const songs = vscode.workspace.createFileSystemWatcher('**/*.lys');
  const parts = vscode.workspace.createFileSystemWatcher('**/*.shosa');
  context.subscriptions.push(songs, parts);
  // The server sends a file's clips whole once, and then what each edit changed (`clipPatches`): the clips are
  // kept here as the server has them, for the preview whenever it is opened or turned to that file.
  client = new LanguageClient('shosa', 'Shosa', serverOptions,
    { documentSelector: [{ language: 'shosa' }], synchronize: { fileEvents: [songs, parts] }, initializationOptions: { clipPatches: true } });
  const whole = (message: ClipMessage | null | undefined) => {
    if (!message) {
      return;
    }
    clips.set(message.uri, message);
    if (panel && panelUri === message.uri) {
      post(message, false);
    }
  };
  client.onNotification('shosa/clip', (message: ClipMessage) => {
    if (message.patch === undefined) {
      whole(message);
      return;
    }
    const known = clips.get(message.uri);
    if (!known || known.seq !== message.base || !known.clips) {
      // Not made on what is kept here (it should not happen): every clip is asked for again.
      clips.delete(message.uri);
      void client?.sendRequest<ClipMessage | null>('shosa/clip', { uri: message.uri }).then(whole, () => undefined);
      return;
    }
    const next: ClipMessage = { uri: message.uri, version: message.version, seq: message.seq, clip: undefined, clips: applyClipPatch(known.clips, message.patch) };
    clips.set(message.uri, next);
    if (panel && panelUri === message.uri) {
      post(next, false, { patch: message.patch, base: message.base ?? -1 });
    }
  });
  // A server that has stopped (VS Code gives up after it crashes five times, as when the extension's files are
  // replaced under it) sends nothing more: the preview says so, rather than go on showing the last file's clip.
  const serverStopped = () => client?.state === State.Stopped;
  client.onDidChangeState(event => {
    if (event.newState === State.Stopped && panel && panelUri) {
      waiting(panelUri);
    }
  });
  await client.start();
  context.subscriptions.push({ dispose: () => void client?.stop() });

  /** The preview of a file the server has sent no clip for (yet): nothing of another file's is left on it. */
  const waiting = (uri: string) => {
    const name = path.basename(vscode.Uri.parse(uri).fsPath);
    void panel?.webview.postMessage({
      type: 'waiting',
      text: serverStopped()
        ? `The Shosa server has stopped, so ${name} is not compiled. Run "Developer: Reload Window" to start it again (Output > Shosa says why it stopped).`
        : `Waiting for the Shosa server to compile ${name}…`,
    });
  };

  // The grammar, the check and the sheet as tools of an AI assistant in the editor (tools.ts).
  registerTools(context, client);

  const show = (uri: string) => {
    panelUri = uri;
    if (!panel) {
      return;
    }
    panel.title = 'Preview ' + path.basename(vscode.Uri.parse(uri).fsPath);
    const known = clips.get(uri);
    if (known && !serverStopped()) {
      post(known, true);
    } else {
      waiting(uri);
    }
  };

  // The song of a dance is played through a sound font when this machine has one: the preview
  // is told where the synthesiser's worklet and the font are, and may read the font's folder.
  const dist = vscode.Uri.joinPath(context.extensionUri, 'dist');
  const resourceRoots = () => {
    const font = soundFontFile();
    return font ? [dist, vscode.Uri.file(path.dirname(font))] : [dist];
  };
  const tellSynth = () => {
    if (!panel) {
      return;
    }
    const font = soundFontFile();
    void panel.webview.postMessage({
      type: 'synth',
      processor: panel.webview.asWebviewUri(vscode.Uri.joinPath(dist, 'spessasynth_processor.min.js')).toString(),
      soundFont: font ? panel.webview.asWebviewUri(vscode.Uri.file(font)).toString() : '',
      name: font ? path.basename(font) : '',
    });
  };
  context.subscriptions.push(vscode.workspace.onDidChangeConfiguration(event => {
    if (panel && (event.affectsConfiguration('shosa.playback.soundFont') || event.affectsConfiguration('lilysharp.playback.soundFont'))) {
      panel.webview.options = { enableScripts: true, localResourceRoots: resourceRoots() };
      tellSynth();
    }
  }));

  // The preview's Export buttons (export.ts). A command that presses one opens the preview first
  // and waits for its page: `pageReady` is set when the page has said 'ready' and been given the clips.
  let pageReady = false;
  const exported = registerExport(context, {
    client,
    writeAudio: (mix, options) => stems.writeAudio(mix, options),
    panel: () => panel,
    uri: () => panelUri,
    ready: async () => {
      if (!panel) {
        await vscode.commands.executeCommand('shosa.openPreview');
      }
      for (let i = 0; i < 100 && panel && !(pageReady && panelUri && clips.has(panelUri)); i++) {
        await new Promise(resolve => setTimeout(resolve, 100));
      }
      return !!panel && pageReady;
    },
  });

  context.subscriptions.push(vscode.commands.registerCommand('shosa.openPreview', () => {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'shosa') {
      return;
    }
    const uri = editor.document.uri.toString();
    if (panel) {
      panel.reveal(vscode.ViewColumn.Beside, true);
      show(uri);
      return;
    }

    panel = vscode.window.createWebviewPanel('shosa.preview', 'Preview', { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
      { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: resourceRoots() });
    panel.webview.html = previewHtml(panel.webview, vscode.Uri.joinPath(dist, 'preview.js'));
    panelUri = uri;

    panel.onDidDispose(() => {
      editorFor(panelUri)?.setDecorations(performed, []);
      panel = undefined;
      panelUri = undefined;
      pageReady = false;
      sentModels.clear();
      stems.forget();
    });

    panel.webview.onDidReceiveMessage((message: { type: string; spans?: [number, number][]; start?: number; length?: number; key?: string; music?: object }) => {
      const editor = editorFor(panelUri);
      if (exported(message as Parameters<typeof exported>[0])) {
        return;
      }
      switch (message.type) {
        case 'song':
          // The notes of the song the clip is danced to: the server has Lily#'s lysc read them.
          if (panelUri && client) {
            const key = message.key;
            client.sendRequest('shosa/song', { ...message.music, uri: panelUri }).then(
              song => void panel?.webview.postMessage({ type: 'song', key, song }),
              () => undefined);
          }
          break;

        case 'resync': {
          // The page was sent a patch made on other clips than it has (it should not happen): every clip again.
          const known = panelUri ? clips.get(panelUri) : undefined;
          if (known) {
            post(known, false);
          }
          break;
        }

        case 'ready':
          sentModels.clear();   // a new page knows no character yet
          tellSynth();
          if (panelUri) {
            show(panelUri);
          }
          pageReady = true;
          break;

        case 'active':
          // The words being performed right now.
          editor?.setDecorations(performed, (message.spans ?? []).map(([start, length]) =>
            new vscode.Range(editor.document.positionAt(start), editor.document.positionAt(start + length))));
          break;

        case 'reveal':
          // A limb was clicked: go to the word that put it there.
          if (editor && message.start !== undefined) {
            const range = new vscode.Range(editor.document.positionAt(message.start),
              editor.document.positionAt(message.start + (message.length ?? 0)));
            editor.selection = new vscode.Selection(range.start, range.end);
            editor.revealRange(range, vscode.TextEditorRevealType.InCenterIfOutsideViewport);
            void vscode.window.showTextDocument(editor.document, editor.viewColumn);
          }
          break;
      }
    });
  }));

  // The cursor leads the preview: put it on a word and the figure shows that word's pose.
  context.subscriptions.push(vscode.window.onDidChangeTextEditorSelection(event => {
    if (!panel || event.textEditor.document.uri.toString() !== panelUri) {
      return;
    }
    // Only when the user moved it. Our own "reveal" also moves the cursor, and answering
    // that would send the preview back to where it already is.
    if (event.kind === vscode.TextEditorSelectionChangeKind.Command || event.kind === undefined) {
      return;
    }
    void panel.webview.postMessage({ type: 'cursor', offset: event.textEditor.document.offsetAt(event.selections[0].active) });
  }));

  // The preview follows the Shosa file being edited.
  context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(editor => {
    if (panel && editor && editor.document.languageId === 'shosa' && editor.document.uri.toString() !== panelUri) {
      editorFor(panelUri)?.setDecorations(performed, []);
      show(editor.document.uri.toString());
    }
  }));
}

export async function deactivate(): Promise<void> {
  await client?.stop();
}

/**
 * How to start the language server. The setting first; then the one the package carries — a
 * program with its own .NET runtime, so nothing but VS Code is needed (tools\pack-extension.ps1 puts it in
 * `server`); then, running from the repository, the one `dotnet build` leaves. A .dll is run with `dotnet`.
 */
function findServer(context: vscode.ExtensionContext): { command: string; args: string[] } | undefined {
  const configured = vscode.workspace.getConfiguration('shosa').get<string>('server.path') ?? '';
  const candidates = [
    configured,
    path.join(context.extensionPath, 'server', process.platform === 'win32' ? 'Shosa.Lsp.exe' : 'Shosa.Lsp'),
    path.join(context.extensionPath, 'server', 'Shosa.Lsp.dll'),
    path.join(context.extensionPath, '..', '..', 'Shosa.Lsp', 'bin', 'Debug', 'net10.0', 'Shosa.Lsp.dll'),
  ];
  const found = candidates.find(candidate => candidate.length > 0 && fs.existsSync(candidate));
  if (!found) {
    return undefined;
  }
  return found.toLowerCase().endsWith('.dll') ? { command: 'dotnet', args: [found] } : { command: found, args: [] };
}

/**
 * The lysc of the installed Lily# extension: what its exports name (`lyscPath`), else the one in its
 * `server` folder; undefined without Lily#. Lily# is woken for its exports, but not waited on for
 * long: past a few seconds its folder is used.
 */
async function lyscOfLilySharp(): Promise<string | undefined> {
  const lily = vscode.extensions.getExtension<{ lyscPath?: string }>('yotsuda.lilysharp');
  if (!lily) {
    return undefined;
  }
  let named: string | undefined;
  try {
    const exports = await Promise.race([
      lily.isActive ? Promise.resolve(lily.exports) : lily.activate(),
      new Promise<undefined>(resolve => setTimeout(() => resolve(undefined), 5000)),
    ]);
    named = exports?.lyscPath;
  } catch {
    // Lily# failed to start: its folder below
  }
  const bundled = path.join(lily.extensionPath, 'server', process.platform === 'win32' ? 'lysc.exe' : 'lysc');
  return [named, bundled].find(candidate => !!candidate && fs.existsSync(candidate));
}

/**
 * The sound font the preview plays a song with, or undefined where there is none (the preview
 * then plays simple tones). Shosa's own setting first; else what Lily# plays with, in Lily#'s
 * order: its setting, the font of the Lily# Sound Font extension, the operating system's
 * General MIDI bank. So a song sounds in the dance's preview as it does in its own.
 */
function soundFontFile(): string | undefined {
  for (const section of ['shosa', 'lilysharp']) {
    const named = (vscode.workspace.getConfiguration(section).get<string>('playback.soundFont') ?? '').trim();
    if (named && fs.existsSync(named)) {
      return named;
    }
  }
  const packaged = vscode.extensions.getExtension('yotsuda.lilysharp-soundfont');
  if (packaged) {
    const folder = path.join(packaged.extensionPath, 'soundfont');
    try {
      const fonts = fs.readdirSync(folder).filter(name => /\.(sf2|sf3|dls)$/i.test(name)).sort();
      if (fonts.length > 0) {
        return path.join(folder, fonts[0]);
      }
    } catch {
      // installed without its font
    }
  }
  const system = process.platform === 'win32' ? path.join(process.env.SystemRoot ?? 'C:\\Windows', 'System32', 'drivers', 'gm.dls')
    : process.platform === 'darwin' ? '/System/Library/Components/CoreAudio.component/Contents/Resources/gs_instruments.dls'
    : '';
  return system && fs.existsSync(system) ? system : undefined;
}

function previewHtml(webview: vscode.Webview, script: vscode.Uri): string {
  const nonce = Array.from({ length: 32 }, () => 'abcdefghijklmnopqrstuvwxyz0123456789'[Math.floor(Math.random() * 36)]).join('');
  // Beyond the page's own script: the synthesiser's worklet is loaded from the extension (or
  // from a blob of it), its decoder of compressed fonts is WebAssembly, and the sound font is fetched.
  const csp = `default-src 'none'; script-src 'nonce-${nonce}' ${webview.cspSource} 'wasm-unsafe-eval' blob:; style-src 'unsafe-inline'; `
    + `img-src ${webview.cspSource} data: blob:; connect-src ${webview.cspSource} blob:;`;   // a character's textures: blob URLs, fetched (characters.js)
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="${csp}">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Shosa preview</title>
</head>
<body>
<script nonce="${nonce}" src="${webview.asWebviewUri(script)}"></script>
</body>
</html>`;
}
