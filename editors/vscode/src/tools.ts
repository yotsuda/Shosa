import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';
import type { LanguageClient } from 'vscode-languageclient/node';

// Tools for an AI assistant in the editor (Copilot Chat's agent mode, and any other that uses
// VS Code's language model tools). An assistant does not know Shosa: these give it the language,
// the compiler's verdict and the picture of the movement, with nothing for the user to set up.
//
//   shosa_grammar   the whole language, from the compiler in this extension
//   shosa_check     what `shosa check FILE [--score] [--at]` prints: diagnostics, scores, sections,
//                   marks, and how the body is turned at the moments asked for
//   shosa_sheet     what `shosa sheet FILE` draws: a pose per beat, as an image the model looks at
//
// The language server does the work (shosa/grammar, shosa/check, shosa/sheet) and is sent the
// text of the document as the editor has it, so a file the assistant has just edited is read as
// it stands, saved or not. The declarations (names, descriptions for the model, input schemas)
// are in package.json under contributes.languageModelTools.

interface FileInput { file: string }
interface CheckInput extends FileInput { score?: string; at?: string | number }
interface SheetInput extends FileInput { score?: string; step?: number; from?: number | string; to?: number | string; view?: string | number }
interface Sheet { score: string; beats: number; png: string }

const text = (value: string) => new vscode.LanguageModelToolResult([new vscode.LanguageModelTextPart(value)]);

/** After the grammar: the way of working it does not say, since it is this editor's. */
const howToWork = `

---

# Working in this editor

1. Write or change the .shosa file. Start from "A complete file" above; use only words that are above.
2. Call the tool shosa_check with the file. Fix until its first line says "no problems". Take lengths, bars, seconds and the times of marks from its report; do not compute them.
3. Call the tool shosa_sheet with the file and LOOK at the image of every score you changed (left limbs blue, right limbs red). No diagnostics does not mean the right movement. For anything shorter than a beat pass "score" and "step": 0.25; for a punch, a kick or a bow straight ahead pass "view": "side".
4. The user watches the movement in the preview beside the file (the button at the top right of the editor, or the command "Shosa: Open Preview to the Side"). Say so when the file is ready.
5. To write the movement for a game engine, run in the terminal: shosa gltf FILE --combined -o NAME.glb (every score an animation in one file), or shosa vrma FILE / shosa bvh FILE. Never edit those outputs; change the .shosa and write them again.
If a movement the user asks for cannot be said with the words above, say so instead of approximating in silence.
`;

export function registerTools(context: vscode.ExtensionContext, client: LanguageClient): void {
  // VS Code before 1.95, or an editor built on it without the API: there is nothing to register with.
  if (typeof vscode.lm?.registerTool !== 'function') {
    return;
  }

  /** The document a tool was given: a path (in full, or from a workspace folder) or a URI. */
  const open = async (file: string): Promise<vscode.TextDocument> => {
    let uri: vscode.Uri | undefined;
    if (/^(file|untitled|vscode-[a-z-]+):/i.test(file)) {
      uri = vscode.Uri.parse(file);
    } else if (path.isAbsolute(file)) {
      uri = vscode.Uri.file(file);
    } else {
      const folders = vscode.workspace.workspaceFolders ?? [];
      const candidates = folders.map(folder => vscode.Uri.joinPath(folder.uri, file));
      uri = candidates.find(candidate => fs.existsSync(candidate.fsPath)) ?? candidates[0];
    }
    if (!uri) {
      throw new Error(`'${file}' is not a full path and no folder is open: give the full path of the .shosa file.`);
    }
    // The open document if there is one (it may hold edits not saved yet), else the file.
    return vscode.workspace.textDocuments.find(d => d.uri.toString() === uri.toString())
      ?? await vscode.workspace.openTextDocument(uri);
  };

  const name = (file: string) => path.basename(file.replace(/[?#].*$/, ''));

  context.subscriptions.push(vscode.lm.registerTool<Record<string, never>>('shosa_grammar', {
    prepareInvocation: () => ({ invocationMessage: 'Reading the Shosa grammar' }),
    invoke: async () => {
      const grammar = await client.sendRequest<{ text: string }>('shosa/grammar', {});
      return text(grammar.text + howToWork);
    },
  }));

  context.subscriptions.push(vscode.lm.registerTool<CheckInput>('shosa_check', {
    prepareInvocation: options => ({ invocationMessage: `Checking ${name(options.input.file)}` }),
    invoke: async options => {
      const { file, score, at } = options.input;
      const document = await open(file);
      // A model may send one beat as a number: the server reads the command line's text.
      const report = await client.sendRequest<{ text: string } | null>('shosa/check',
        { uri: document.uri.toString(), text: document.getText(), score, at: at === undefined ? undefined : String(at) });
      if (!report) {
        return text('The Shosa language server gave no answer.');
      }
      const clean = report.text.includes(': no problems');
      return text(report.text + (clean
        ? '\n\nNo diagnostics does not mean the right movement: look at it with the tool shosa_sheet.'
        : '\n\nFix every line above that says "error" (the message says what to change) and check again. If a word is refused, it is not in the grammar (tool shosa_grammar): do not try spellings.'));
    },
  }));

  context.subscriptions.push(vscode.lm.registerTool<SheetInput>('shosa_sheet', {
    prepareInvocation: options => ({
      invocationMessage: `Drawing ${name(options.input.file)}${options.input.score ? ` (${options.input.score})` : ''}`,
    }),
    invoke: async options => {
      const { file, score, step, from, to, view } = options.input;
      const document = await open(file);
      const drawn = await client.sendRequest<{ problems: string; sheets: Sheet[] } | null>('shosa/sheet',
        { uri: document.uri.toString(), text: document.getText(), score, step, from, to, view });
      if (!drawn) {
        return text('The Shosa language server gave no answer.');
      }
      if (drawn.sheets.length === 0) {
        return text((drawn.problems ? drawn.problems + '\n\n' : '') + 'There is no movement to draw'
          + (score ? ` in a score named '${score}'` : '') + '. Run the tool shosa_check and fix what it says first.');
      }

      // Each sheet is given to the model as an image, and written to a file the user can open.
      const folder = path.join(os.tmpdir(), 'shosa-sheets');
      fs.mkdirSync(folder, { recursive: true });
      const stem = path.basename(document.uri.fsPath || 'untitled', '.shosa');
      // LanguageModelDataPart came after the tools API (1.95): an editor without it gets the files only.
      const dataPart = (vscode as unknown as { LanguageModelDataPart?: { image(data: Uint8Array, mime: string): unknown } }).LanguageModelDataPart;
      const parts: unknown[] = [];
      if (drawn.problems) {
        parts.push(new vscode.LanguageModelTextPart(drawn.problems));
      }
      for (const sheet of drawn.sheets) {
        const png = Buffer.from(sheet.png, 'base64');
        const saved = path.join(folder, stem + (sheet.score ? '-' + sheet.score : '') + '.png');
        fs.writeFileSync(saved, png);
        parts.push(new vscode.LanguageModelTextPart(
          `${sheet.score ? `score ${sheet.score}` : 'the score'}: ${sheet.beats} beats. The image follows; it is also the file ${saved}`));
        if (dataPart) {
          parts.push(dataPart.image(new Uint8Array(png), 'image/png'));
        }
      }
      parts.push(new vscode.LanguageModelTextPart(
        'How to read a sheet is under "Checking your work" in the grammar: a pose per beat from left to right, the beat under it, '
        + 'left limbs blue and right limbs red, the floor from above below. Compare each pose with what the user asked for.'
        + (dataPart ? '' : ' (This editor cannot hand you the image: open the file, or ask the user to look at the preview.)')));
      return new vscode.LanguageModelToolResult(parts as vscode.LanguageModelTextPart[]);
    },
  }));
}
