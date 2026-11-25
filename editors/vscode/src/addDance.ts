import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import type { LanguageClient } from 'vscode-languageclient/node';
import { danceRequest, danceSkeleton } from './danceText';
import { modelsOnOffer } from './models';

// Shosa: Add a Dance… — from a Lily# song to a dance of it, the first step of the way from a song to
// a video (DESIGN.md 123.3). On a .lys (the Explorer's right-click, the editor's title menu, the
// palette): the song's score if it has several, a character for each part that sings, and what the
// dance should be like. It writes SONG.shosa beside the song — the `music` line, a dancer for each
// singing part with `sings`, the score — opens it with the preview, and asks Copilot's chat to
// choreograph it, the song read with the tool shosa_song and the words placed as the grammar says.

interface SongAnswer { text: string; scores: string[]; parts: string[]; error: string | null }

/** SONG.shosa beside the song, or SONG-2.shosa … when that is taken: a dance already there is never written over. */
function freeName(folder: string, stem: string): string {
  for (let n = 1; ; n++) {
    const file = path.join(folder, `${stem}${n === 1 ? '' : '-' + n}.shosa`);
    if (!fs.existsSync(file)) {
      return file;
    }
  }
}

/** A song whose folder was opened for Add a Dance… (the window reloads): the command goes on with it after the reload. */
const pendingKey = 'shosa.addDance.pending';
/** How long such a song is waited for: a reload takes seconds, and a stale one must not start the command a day later. */
const pendingFor = 2 * 60 * 1000;

/**
 * Copilot's chat asks before every edit of a file outside the open folders ("Allow Edits in <folder> in this
 * Session" …), which stops the choreography at its first edit (the user's report, 2026-11-26). So a song outside
 * them is offered its folder first: added to the folders open (no reload), or opened when none is (the window
 * reloads, and the command goes on afterwards). 'here': go on in this window; 'reloading': the folder is being
 * opened; 'cancel': the question was closed, and nothing is done.
 */
async function songFolderOpen(context: vscode.ExtensionContext, song: vscode.Uri): Promise<'here' | 'reloading' | 'cancel'> {
  if (vscode.workspace.getWorkspaceFolder(song)) {
    return 'here';
  }
  const folder = vscode.Uri.file(path.dirname(song.fsPath));
  const name = path.basename(folder.fsPath);
  const folders = vscode.workspace.workspaceFolders ?? [];
  const open = folders.length > 0 ? `Add "${name}" to the Workspace` : `Open "${name}"`;
  const anyway = 'Continue Without';
  const answer = await vscode.window.showInformationMessage(
    `The song's folder "${name}" is not open in VS Code, so Copilot's chat would ask before each edit of the dance it writes there.`,
    { modal: true, detail: folders.length > 0 ? 'Adding it does not reload the window.' : 'Opening it reloads the window; Add a Dance… then goes on.' },
    open, anyway);
  if (answer === anyway) {
    return 'here';
  }
  if (answer !== open) {
    return 'cancel';
  }
  if (folders.length > 0) {
    // Added at the end: the first folder stays, so the extensions are not restarted.
    vscode.workspace.updateWorkspaceFolders(folders.length, 0, { uri: folder });
    return 'here';
  }
  await context.globalState.update(pendingKey, { song: song.toString(), at: Date.now() });
  await vscode.commands.executeCommand('vscode.openFolder', folder, { forceReuseWindow: true });
  return 'reloading';
}

export function registerAddDance(context: vscode.ExtensionContext, client: LanguageClient): void {
  registerCommand(context, client);
  // After the reload that opened a song's folder: go on with that song (the command is registered by now).
  const pending = context.globalState.get<{ song: string; at: number }>(pendingKey);
  if (pending) {
    void context.globalState.update(pendingKey, undefined);
    const song = vscode.Uri.parse(pending.song);
    if (Date.now() - pending.at < pendingFor && vscode.workspace.getWorkspaceFolder(song)) {
      void vscode.commands.executeCommand('shosa.addDance', song);
    }
  }
}

function registerCommand(context: vscode.ExtensionContext, client: LanguageClient): void {
  context.subscriptions.push(vscode.commands.registerCommand('shosa.addDance', async (given?: vscode.Uri) => {
    // The song: the one right-clicked, else the .lys being edited, else one picked.
    let song = given instanceof vscode.Uri ? given : undefined;
    const active = vscode.window.activeTextEditor?.document;
    if (!song && active && /\.lys$/i.test(active.uri.path)) {
      song = active.uri;
    }
    if (!song) {
      const picked = await vscode.window.showOpenDialog({ title: 'Add a Dance to a Song', filters: { 'Lily# song': ['lys'] }, canSelectMany: false });
      song = picked?.[0];
    }
    if (!song || song.scheme !== 'file') {
      return;
    }
    if (await songFolderOpen(context, song) !== 'here') {
      return;
    }
    const open = vscode.workspace.textDocuments.find(d => d.uri.toString() === song!.toString());
    if (open?.isDirty) {
      await open.save();   // the dance reads the song from disk, as the compiler does
    }

    const ask = (score?: string) => client.sendRequest<SongAnswer>('shosa/songReport', { uri: song!.toString(), score });
    let answer = await ask();
    if (answer.error) {
      void vscode.window.showErrorMessage(`Shosa: ${answer.error}`);
      return;
    }

    // Which of the song's scores (a song with a score per language, a karaoke one …).
    let score: string | undefined;
    if (answer.scores.length > 1 || (answer.scores.length === 1 && answer.scores[0] !== '')) {
      const picked = await vscode.window.showQuickPick(
        answer.scores.map(s => ({ label: s === '' ? '(the unnamed score)' : s, score: s })),
        { title: 'Add a Dance — the song\'s score to dance to', ignoreFocusOut: true });
      if (!picked) {
        return;
      }
      score = picked.score === '' ? undefined : picked.score;
      answer = await ask(score);
      if (answer.error) {
        void vscode.window.showErrorMessage(`Shosa: ${answer.error}`);
        return;
      }
    }

    // A dancer for each part that sings; one, singing nothing, for a song without words.
    const folder = path.dirname(song.fsPath);
    const offer = await modelsOnOffer(song);
    const dancers: { part: string; model?: string }[] = (answer.parts.length > 0 ? answer.parts : ['']).map(part => ({ part }));
    if (offer && offer.models.length > 0) {
      for (const dancer of dancers) {
        const none = { label: '$(person) None', description: 'drawn as a figure', name: undefined as string | undefined };
        const picked = await vscode.window.showQuickPick(
          [none, ...offer.models.map(m => ({ label: path.basename(m.name), description: m.name, name: m.name as string | undefined }))],
          { title: dancer.part ? `Add a Dance — the character who sings ${dancer.part}` : 'Add a Dance — the character', ignoreFocusOut: true });
        if (!picked) {
          return;
        }
        dancer.model = picked.name;
      }
    }

    const wanted = await vscode.window.showInputBox({
      title: 'Add a Dance — what the dance should be like',
      prompt: 'Copilot\'s chat choreographs it to the words of the song, its thinking in view. Pick a strong model in the chat\'s model menu.',
      placeHolder: 'e.g. a cheerful idol dance, small steps in the verses, the chorus with big arms and a clap',
      ignoreFocusOut: true,
    });
    if (wanted === undefined) {
      return;
    }

    const songName = path.basename(song.fsPath);
    const dance = freeName(folder, path.basename(song.fsPath, path.extname(song.fsPath)));
    fs.writeFileSync(dance, danceSkeleton(songName, score, dancers), 'utf8');
    const document = await vscode.workspace.openTextDocument(dance);
    await vscode.window.showTextDocument(document);
    await vscode.commands.executeCommand('shosa.openPreview');

    const query = danceRequest(dance, song.fsPath, score, answer.parts, wanted.trim() || 'a dance that suits the song');
    try {
      await vscode.commands.executeCommand('workbench.action.chat.open', { query, mode: 'agent' });
    } catch {
      try {
        await vscode.commands.executeCommand('workbench.action.chat.open', query);
      } catch (err) {
        void vscode.window.showErrorMessage(`Shosa: Copilot's chat could not be opened (${err}). The dance is begun in ${path.basename(dance)}; ask an assistant to choreograph it.`);
      }
    }
  }));
}
