import * as path from 'path';
import * as vscode from 'vscode';

// The song of a dance, one stem at a time, from the Lily# extension (its exports' renderStems,
// stemsVersion 1: Lily#'s docs/stems-api-design.md; DESIGN.md 98). The preview mixes them.
//
// A dance uses them when it is danced to the song as the song is played: in the song's own order and
// tempo (`music` with no `form` or `tempo` of the dance's own). With a recording the dance keeps time
// with, the stems are asked for on the recording's seconds; without, on the score's.
//
// The extension asks Lily#, hands the page what came back ({ type: 'stems' }) and the bytes of every
// file (the page decodes them), and asks again when Lily# says the song's sound may have changed.

export interface Stem { kind: 'part' | 'voice' | 'recording' | 'recordingPart'; name: string; file: string; at: number }
interface Stems { timeline: 'recording' | 'score'; stems: Stem[]; notes: string[] }
interface StemsApi {
  stemsVersion: number;
  renderStems(lys: vscode.Uri, options?: { score?: string; withoutRecording?: boolean; signal?: AbortSignal;
    onProgress?(p: { stem: string; done: number; total: number }): void }): Promise<Stems>;
  onDidChangeStems: vscode.Event<vscode.Uri>;
  writeAudio(mix: { left: Float32Array; right: Float32Array; sampleRate: number }, options: { format: 'mp3' | 'wav'; path: string }): Promise<void>;
}

/** The song of a clip as the server writes it (MusicIr). */
export interface ClipMusic { file: string; score?: string | null; order?: string[] | null; tempo?: number | null; recording?: { audio: string; offset: number } | null }

/** The key the page finds a clip's stems by: the song as written, its score, and whether the recording is followed. */
export const stemsKey = (music: ClipMusic) => JSON.stringify([music.file, music.score ?? null, !!music.recording]);

/** The stems the page is given: what the API gave, or how far it is, or why there are none. */
interface StemsMessage {
  type: 'stems';
  key: string;
  state: 'loading' | 'ready' | 'none';
  timeline?: 'recording' | 'score';
  stems?: Stem[];
  notes?: string[];
  progress?: string;
  error?: string;
}

export interface StemsHost {
  /** Posts to the preview's page, if there is one. */
  post(message: StemsMessage): void;
  /** Sends the bytes of a file to the page (once per version of it). */
  sendFile(file: string): void;
}

export function createStems(context: vscode.ExtensionContext, host: StemsHost) {
  let api: Promise<StemsApi | undefined> | undefined;
  /** Every song asked for: the last message about it, and how to stop its rendering. */
  const songs = new Map<string, { lys: vscode.Uri; music: ClipMusic; last?: StemsMessage; abort?: AbortController }>();

  const apiOf = (): Promise<StemsApi | undefined> => api ??= (async () => {
    const lily = vscode.extensions.getExtension<Partial<StemsApi>>('yotsuda.lilysharp');
    if (!lily) {
      return undefined;
    }
    try {
      const exports = lily.isActive ? lily.exports : await lily.activate();
      if (exports?.stemsVersion !== 1 || typeof exports.renderStems !== 'function') {
        return undefined;
      }
      context.subscriptions.push(exports.onDidChangeStems!(uri => {
        for (const [key, song] of songs) {
          if (song.lys.toString() === uri.toString()) {
            void render(key);
          }
        }
      }));
      return exports as StemsApi;
    } catch {
      return undefined;
    }
  })();

  const tell = (message: StemsMessage) => {
    const song = songs.get(message.key);
    if (song) {
      song.last = message;
    }
    host.post(message);
    for (const stem of message.stems ?? []) {
      host.sendFile(stem.file);
    }
  };

  const render = async (key: string) => {
    const song = songs.get(key);
    if (!song) {
      return;
    }
    const lily = await apiOf();
    if (!lily) {
      const installed = vscode.extensions.getExtension('yotsuda.lilysharp');
      tell({ type: 'stems', key, state: 'none',
        error: installed ? 'this version of the Lily# extension does not hand out the song\'s parts (update it to mix them)' : 'the Lily# extension is not installed' });
      return;
    }
    song.abort?.abort();
    const abort = song.abort = new AbortController();
    // While it renders, the page goes on with what it had (the stems of before, or the song's notes).
    const keep = song.last?.state === 'ready' ? song.last : undefined;
    if (!keep) {
      tell({ type: 'stems', key, state: 'loading' });
    }
    try {
      const made = await lily.renderStems(song.lys, {
        score: song.music.score ?? undefined,
        withoutRecording: !song.music.recording,
        signal: abort.signal,
        onProgress: p => {
          if (!keep && !abort.signal.aborted) {
            tell({ type: 'stems', key, state: 'loading', progress: `${p.stem} (${p.done} of ${p.total})` });
          }
        },
      });
      if (abort.signal.aborted) {
        return;
      }
      tell({ type: 'stems', key, state: 'ready', timeline: made.timeline, stems: made.stems, notes: made.notes });
    } catch (error) {
      if (!abort.signal.aborted) {
        tell({ type: 'stems', key, state: 'none', error: error instanceof Error ? error.message : String(error) });
      }
    }
  };

  return {
    /** The songs of the clips shown, from the .shosa file `uri`: asked for the first time each is seen. */
    want(uri: vscode.Uri, musics: ClipMusic[]) {
      for (const music of musics) {
        if (music.order?.length || music.tempo != null || uri.scheme !== 'file') {
          continue;   // danced in an order or a tempo of the dance's own: the stems are not on its clock
        }
        const key = stemsKey(music);
        const known = songs.get(key);
        if (known?.last) {
          tell(known.last);   // again, for a page that may be new
          continue;
        }
        if (!known) {
          songs.set(key, { lys: vscode.Uri.file(path.resolve(path.dirname(uri.fsPath), music.file)), music });
          void render(key);
        }
      }
    },

    /** Lily#'s writer of MP3 and WAV, or why there is none. */
    async writeAudio(mix: { left: Float32Array; right: Float32Array; sampleRate: number }, options: { format: 'mp3' | 'wav'; path: string }) {
      const lily = await apiOf();
      if (!lily) {
        throw new Error('the Lily# extension that writes MP3 and WAV is not installed, or is too old');
      }
      await lily.writeAudio(mix, options);
    },

    /** A new page: what it was told is gone. */
    forget() {
      for (const song of songs.values()) {
        song.abort?.abort();
      }
      songs.clear();
    },
  };
}
