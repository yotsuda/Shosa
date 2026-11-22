import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';

// The characters a dancer may be dressed in: the VRM files of the folder the setting
// `shosa.models.folder` names. With the cursor on the file of a dancer line
// (`dancer alice humanoid "girl.vrm"`), Ctrl+Shift+Up/Down puts the one before or after it there,
// and the preview, following the edit, shows the dancer in it.
//
// The key is ours only on that file: the context key `shosa.onModelPath` says so, so elsewhere
// Ctrl+Shift+Up/Down keep whatever VS Code or the user gave them.

const contextKey = 'shosa.onModelPath';
/** How deep below the folder VRM files are looked for (a model's zip is often unpacked into a folder of its own). */
const depth = 3;

/** The quoted file of a dancer line the position is on: the range inside the quotes and the text. */
function modelAt(document: vscode.TextDocument, position: vscode.Position): { range: vscode.Range; file: string } | undefined {
  if (document.languageId !== 'shosa') {
    return undefined;
  }
  const line = document.lineAt(position.line).text;
  if (!/^\s*dancer\b/.test(line)) {
    return undefined;
  }
  // The language has no escapes in strings (Lexer.cs): a string is from a quote to the next.
  const quoted = /"([^"]*)"/g;
  for (let match; (match = quoted.exec(line));) {
    const start = match.index + 1;
    const end = start + match[1].length;
    if (position.character >= match.index && position.character <= end + 1) {
      return { range: new vscode.Range(position.line, start, position.line, end), file: match[1] };
    }
  }
  return undefined;
}

/** The folder of the setting as a full path, or undefined when none is set. `~` is the home folder; a relative path is from the workspace folder. */
function modelsFolder(document: vscode.TextDocument): string | undefined {
  const named = (vscode.workspace.getConfiguration('shosa', document.uri).get<string>('models.folder') ?? '').trim();
  if (!named) {
    return undefined;
  }
  const expanded = named === '~' || /^~[\\/]/.test(named) ? path.join(os.homedir(), named.slice(1)) : named;
  if (path.isAbsolute(expanded)) {
    return expanded;
  }
  const workspace = vscode.workspace.getWorkspaceFolder(document.uri) ?? vscode.workspace.workspaceFolders?.[0];
  return path.resolve(workspace ? workspace.uri.fsPath : path.dirname(document.uri.fsPath), expanded);
}

/** Every .vrm file in the folder and the folders below it, in the order of their paths. */
async function modelFiles(folder: string): Promise<string[]> {
  const found: string[] = [];
  const walk = async (dir: string, level: number) => {
    let entries: fs.Dirent[];
    try {
      entries = await fs.promises.readdir(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      const full = path.join(dir, entry.name);
      if (entry.isFile() && entry.name.toLowerCase().endsWith('.vrm')) {
        found.push(full);
      } else if (entry.isDirectory() && level < depth) {
        await walk(full, level + 1);
      }
    }
  };
  await walk(folder, 0);
  const key = (file: string) => path.relative(folder, file).replace(/\\/g, '/').toLowerCase();
  return found.sort((a, b) => (key(a) < key(b) ? -1 : key(a) > key(b) ? 1 : 0));
}

/** How the file is written in the dance: from the dance's folder when it can be (the way the server reads it), with forward slashes. */
function written(file: string, document: vscode.TextDocument): string {
  const relative = path.relative(path.dirname(document.uri.fsPath), file);
  const usable = relative && !path.isAbsolute(relative);   // another drive gives an absolute path
  return (usable ? relative : file).replace(/\\/g, '/');
}

function samePath(a: string, b: string): boolean {
  return process.platform === 'win32' || process.platform === 'darwin' ? a.toLowerCase() === b.toLowerCase() : a === b;
}

async function step(by: 1 | -1): Promise<void> {
  const editor = vscode.window.activeTextEditor;
  if (!editor) {
    return;
  }
  const document = editor.document;
  const at = modelAt(document, editor.selection.active);
  if (!at) {
    return;
  }
  const folder = modelsFolder(document);
  if (!folder) {
    const choice = await vscode.window.showInformationMessage(
      'Shosa: set the folder of your VRM models (shosa.models.folder) to switch a dancer\'s character with Ctrl+Shift+Up/Down.',
      'Open Settings');
    if (choice) {
      void vscode.commands.executeCommand('workbench.action.openSettings', 'shosa.models.folder');
    }
    return;
  }
  const files = await modelFiles(folder);
  if (files.length === 0) {
    void vscode.window.showWarningMessage(`Shosa: there is no .vrm file in ${folder} (shosa.models.folder).`);
    return;
  }
  // Untitled files have no folder: the path is written in full.
  const current = at.file && !document.isUntitled ? path.resolve(path.dirname(document.uri.fsPath), at.file) : at.file;
  const index = files.findIndex(file => samePath(file, current));
  const next = index < 0 ? (by > 0 ? 0 : files.length - 1) : (index + by + files.length) % files.length;
  const text = document.isUntitled ? files[next].replace(/\\/g, '/') : written(files[next], document);
  const range = at.range;
  // The cursor stays in the string, as far from its end as it was (on the name, usually), so the key can be pressed again.
  const was = Math.min(Math.max(editor.selection.active.character, range.start.character), range.end.character);
  const fromEnd = range.end.character - was;
  const ok = await editor.edit(edit => edit.replace(range, text));
  if (!ok) {
    return;
  }
  const end = range.start.character + text.length;
  const caret = new vscode.Position(range.start.line, Math.max(range.start.character, end - Math.min(fromEnd, text.length)));
  editor.selection = new vscode.Selection(caret, caret);
  vscode.window.setStatusBarMessage(`Shosa: character ${next + 1}/${files.length} — ${path.relative(folder, files[next])}`, 3000);
}

export function registerModels(context: vscode.ExtensionContext): void {
  const update = (editor: vscode.TextEditor | undefined) => {
    const on = !!editor && !!modelAt(editor.document, editor.selection.active);
    void vscode.commands.executeCommand('setContext', contextKey, on);
  };
  update(vscode.window.activeTextEditor);
  context.subscriptions.push(
    vscode.window.onDidChangeTextEditorSelection(event => update(event.textEditor)),
    vscode.window.onDidChangeActiveTextEditor(update),
    vscode.commands.registerCommand('shosa.nextModel', () => step(1)),
    vscode.commands.registerCommand('shosa.previousModel', () => step(-1)));
}
