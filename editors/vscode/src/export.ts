import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import type { LanguageClient } from 'vscode-languageclient/node';

// The preview's two Export buttons, and the commands that press them.
//
//   Model   the score shown as a file for an engine: glTF (.glb), VRM Animation (.vrma) or BVH
//           (.bvh), chosen by the type picked in the save dialog. The language server writes
//           them (shosa/export) from the text as the editor has it — what `shosa gltf|vrma|bvh`
//           writes: a file per figure where the format holds one, FILE.events.json beside it.
//   MP4     the score shown as a video: the preview films its own view and encodes it in the
//           page (preview/video.js: H.264 and AAC by the editor's own encoders, so nothing has
//           to be installed), with the song as the preview plays it (preview/songaudio.js).
//
// The preview says which score it shows ({ type: 'export' }); this file asks where to write,
// and for a video answers { type: 'record' } and waits for { type: 'recorded' }.

export interface ExportHost {
  client: LanguageClient;
  panel(): vscode.WebviewPanel | undefined;
  /** The document the preview shows. */
  uri(): string | undefined;
  /** Opens the preview if need be; resolves once its page has the movement. */
  ready(): Promise<boolean>;
}

interface PreviewMessage {
  type: string;
  what?: 'model' | 'video';
  id?: string;
  score?: string;
  data?: ArrayBuffer | Uint8Array;
  silent?: string;
  error?: string;
  stopped?: boolean;
}
interface ExportedFile { figure: string | null; kind: 'model' | 'events'; data: string }

const modelTypes: Record<string, string[]> = {
  'glTF binary, for Godot, Blender, Unity, the web (*.glb)': ['glb'],
  'VRM Animation, for any VRM model (*.vrma)': ['vrma'],
  'BVH, for Blender and motion tools (*.bvh)': ['bvh'],
};

export function registerExport(context: vscode.ExtensionContext, host: ExportHost): (message: PreviewMessage) => boolean {
  let next = 1;
  /** A click made by a command: where to write (no dialog then), and who waits for the files. */
  const asked = new Map<string, { path?: string; done: (files: string[]) => void }>();
  /** Videos being filmed: where each goes. */
  const filming = new Map<string, { target: vscode.Uri; done: (files: string[]) => void }>();

  const document = () => vscode.workspace.textDocuments.find(d => d.uri.toString() === host.uri());

  /** Beside the .shosa file, named as the CLI names it: walk.glb, fighter-Combo.glb. */
  const suggested = (score: string, extension: string): vscode.Uri => {
    const source = document()?.uri;
    const folder = source?.scheme === 'file' ? path.dirname(source.fsPath)
      : vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? os.homedir();
    const stem = source?.scheme === 'file' ? path.basename(source.fsPath, '.shosa') : 'untitled';
    return vscode.Uri.file(path.join(folder, stem + (score ? '-' + score : '') + extension));
  };

  const wrote = (files: string[], note = '') => {
    const names = files.map(file => path.basename(file));
    const open = 'Open', reveal = 'Show in Folder';
    void vscode.window.showInformationMessage(`Shosa: wrote ${names.join(', ')}${note ? ' ' + note : ''}`, open, reveal).then(choice => {
      if (choice === open) {
        void vscode.env.openExternal(vscode.Uri.file(files[0]));
      } else if (choice === reveal) {
        void vscode.commands.executeCommand('revealFileInOS', vscode.Uri.file(files[0]));
      }
    });
  };

  const model = async (score: string, given: string | undefined): Promise<string[]> => {
    const target = given ? vscode.Uri.file(given)
      : await vscode.window.showSaveDialog({ title: 'Export Model', defaultUri: suggested(score, '.glb'), filters: modelTypes });
    if (!target) {
      return [];
    }
    const format = path.extname(target.fsPath).slice(1).toLowerCase();
    if (!['glb', 'vrma', 'bvh'].includes(format)) {
      void vscode.window.showErrorMessage(`Shosa: '${path.basename(target.fsPath)}' is none of .glb, .vrma, .bvh: the name says which file to write.`);
      return [];
    }
    const answer = await host.client.sendRequest<{ problems: string; files: ExportedFile[] } | null>('shosa/export',
      { uri: host.uri(), text: document()?.getText(), score, format });
    if (!answer || answer.files.length === 0) {
      void vscode.window.showErrorMessage('Shosa: there is no movement to export.' + (answer?.problems ? ' ' + answer.problems.split('\n')[0] : ''));
      return [];
    }
    // walk.bvh for one figure; walk.a.bvh, walk.b.bvh for several; walk.events.json beside them.
    const whole = target.fsPath, stem = whole.slice(0, whole.length - path.extname(whole).length);
    const files = answer.files.map(file => {
      const name = file.kind === 'events' ? stem + '.events.json' : stem + (file.figure ? '.' + file.figure : '') + path.extname(whole);
      fs.writeFileSync(name, Buffer.from(file.data, 'base64'));
      return name;
    });
    wrote(files);
    return files;
  };

  const video = async (score: string, given: string | undefined, done: (files: string[]) => void): Promise<void> => {
    const target = given ? vscode.Uri.file(given)
      : await vscode.window.showSaveDialog({ title: 'Export MP4 Video', defaultUri: suggested(score, '.mp4'), filters: { 'MP4 video (*.mp4)': ['mp4'] } });
    const panel = host.panel();
    if (!target || !panel) {
      done([]);
      return;
    }
    const id = 'film' + next++;
    filming.set(id, { target, done });
    void panel.webview.postMessage({ type: 'record', id });
  };

  // The buttons, pressed from the command palette (or a test: `path` then, and no dialog).
  const press = async (what: 'model' | 'video', given?: string): Promise<string[]> => {
    if (!(await host.ready())) {
      void vscode.window.showErrorMessage('Shosa: open a .shosa file to export its movement.');
      return [];
    }
    const id = 'ask' + next++;
    return new Promise<string[]>(resolve => {
      asked.set(id, { path: typeof given === 'string' ? given : undefined, done: resolve });
      void host.panel()?.webview.postMessage({ type: 'export', what, id });
    });
  };
  context.subscriptions.push(
    vscode.commands.registerCommand('shosa.exportModel', (given?: string) => press('model', given)),
    vscode.commands.registerCommand('shosa.exportVideo', (given?: string) => press('video', given)));

  return (message: PreviewMessage): boolean => {
    if (message.type === 'export') {
      const from = message.id ? asked.get(message.id) : undefined;
      if (message.id) {
        asked.delete(message.id);
      }
      const done = from?.done ?? (() => undefined);
      const failed = (error: unknown) => {
        void vscode.window.showErrorMessage(`Shosa: the export failed. ${error instanceof Error ? error.message : String(error)}`);
        done([]);
      };
      if (message.what === 'video') {
        video(message.score ?? '', from?.path, done).catch(failed);
      } else {
        model(message.score ?? '', from?.path).then(done, failed);
      }
      return true;
    }

    if (message.type === 'recorded') {
      // The answer to a 'record' — or to a command's click that the page could not take.
      const film = message.id ? filming.get(message.id) : undefined;
      const from = message.id ? asked.get(message.id) : undefined;
      if (message.id) {
        filming.delete(message.id);
        asked.delete(message.id);
      }
      const done = film?.done ?? from?.done ?? (() => undefined);
      if (message.error || !film || !message.data) {
        if (message.error) {
          void vscode.window.showErrorMessage(`Shosa: no video was written: ${message.error}`);
        }
        done([]);
        return true;
      }
      const bytes = message.data instanceof ArrayBuffer ? Buffer.from(message.data)
        : Buffer.from(message.data.buffer, message.data.byteOffset, message.data.byteLength);
      try {
        fs.writeFileSync(film.target.fsPath, bytes);
        wrote([film.target.fsPath], (message.silent ? `(without its song: ${message.silent}).` : ''));
        done([film.target.fsPath]);
      } catch (error) {
        void vscode.window.showErrorMessage(`Shosa: ${path.basename(film.target.fsPath)} could not be written. ${error instanceof Error ? error.message : String(error)}`);
        done([]);
      }
      return true;
    }
    return false;
  };
}
