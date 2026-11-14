import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { LanguageClient, ServerOptions, TransportKind } from 'vscode-languageclient/node';

// The extension is thin on purpose. The language server knows the language; the preview
// knows how to draw. This file starts the one, opens the other, and carries three kinds of
// message between editor and preview:
//   server  -> preview : the solved movement, after every edit           (shosa/clip)
//   editor  -> preview : where the cursor is, so the preview shows that word's pose
//   preview -> editor  : which words are being performed (to light them up), and which
//                        word to go to when a limb is clicked

interface ClipMessage {
  uri: string;
  version: number;
  clip: unknown;
}

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

  const dll = findServer(context);
  if (!dll) {
    void vscode.window.showErrorMessage(
      'Shosa: the language server (Shosa.Lsp.dll) was not found. Build it with "dotnet build", or set shosa.server.path.');
    return;
  }

  const serverOptions: ServerOptions = { command: 'dotnet', args: [dll], transport: TransportKind.stdio };
  client = new LanguageClient('shosa', 'Shosa', serverOptions, { documentSelector: [{ language: 'shosa' }] });
  client.onNotification('shosa/clip', (message: ClipMessage) => {
    clips.set(message.uri, message);
    if (panel && panelUri === message.uri) {
      void panel.webview.postMessage({ type: 'clip', clip: message.clip });
    }
  });
  await client.start();
  context.subscriptions.push({ dispose: () => void client?.stop() });

  const editorFor = (uri: string | undefined) =>
    vscode.window.visibleTextEditors.find(e => e.document.uri.toString() === uri);

  const show = (uri: string) => {
    panelUri = uri;
    if (!panel) {
      return;
    }
    panel.title = 'Preview ' + path.basename(vscode.Uri.parse(uri).fsPath);
    const known = clips.get(uri);
    if (known) {
      void panel.webview.postMessage({ type: 'clip', clip: known.clip, reset: true });
    }
  };

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

    const dist = vscode.Uri.joinPath(context.extensionUri, 'dist');
    panel = vscode.window.createWebviewPanel('shosa.preview', 'Preview', { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
      { enableScripts: true, retainContextWhenHidden: true, localResourceRoots: [dist] });
    panel.webview.html = previewHtml(panel.webview, vscode.Uri.joinPath(dist, 'preview.js'));
    panelUri = uri;

    panel.onDidDispose(() => {
      editorFor(panelUri)?.setDecorations(performed, []);
      panel = undefined;
      panelUri = undefined;
    });

    panel.webview.onDidReceiveMessage((message: { type: string; spans?: [number, number][]; start?: number; length?: number }) => {
      const editor = editorFor(panelUri);
      switch (message.type) {
        case 'ready':
          if (panelUri) {
            show(panelUri);
          }
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

function findServer(context: vscode.ExtensionContext): string | undefined {
  const configured = vscode.workspace.getConfiguration('shosa').get<string>('server.path') ?? '';
  const candidates = [
    configured,
    path.join(context.extensionPath, 'server', 'Shosa.Lsp.dll'),
    // Running from the repository (F5): the server as `dotnet build` leaves it.
    path.join(context.extensionPath, '..', '..', 'Shosa.Lsp', 'bin', 'Debug', 'net10.0', 'Shosa.Lsp.dll'),
  ];
  return candidates.find(candidate => candidate.length > 0 && fs.existsSync(candidate));
}

function previewHtml(webview: vscode.Webview, script: vscode.Uri): string {
  const nonce = Array.from({ length: 32 }, () => 'abcdefghijklmnopqrstuvwxyz0123456789'[Math.floor(Math.random() * 36)]).join('');
  const csp = `default-src 'none'; script-src 'nonce-${nonce}'; style-src 'unsafe-inline'; img-src ${webview.cspSource} data:;`;
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
