// What an edit changed in a file's clips, put into the clips as they were (DESIGN.md 120). The server sends every
// clip whole once, and after each edit a patch (Shosa.Lsp\ClipPatch.cs says its shape): megabytes of frames are not
// sent, parsed and copied again for a word that moved two bars of them. The extension keeps the clips this way for
// the preview it may open later; the preview's page keeps its own.
//
// Nothing that was is changed: a clip, a figure or a list of rows an edit touched is made anew, and everything it
// did not touch is the same object as before. So whatever was worked out for a figure (the arms kept out of a
// character's body at every frame, a juggler's palms) stays good for a figure that is still the same object, and
// for one that is not, the rows that are still the same arrays are the frames it need not work out again.

const ROWS = ['frames', 'turns', 'mouth'];

/** The clips after a patch. `clips` is what the patch was made on (the message's `base`). */
export function applyClipPatch(clips, patch) {
  return patch.clips.map(entry => {
    if (entry.whole) return entry.whole;
    const was = clips[entry.from];
    if (!entry.head && !entry.figures) return was;
    const figures = entry.figures ? was.figures.map((figure, i) => patched(figure, entry.figures[i])) : was.figures;
    // The head is the clip without its figures, whole: what it has no more is gone.
    return entry.head ? { ...entry.head, figures } : { ...was, figures };
  });
}

function patched(was, change) {
  if (!change) return was;
  const figure = change.head ? { ...change.head } : { ...was };
  for (const key of [...ROWS, 'mic', 'juggle']) {
    if (change.head && was[key] !== undefined) figure[key] = was[key];
  }
  for (const key of ROWS) {
    const runs = change.rows && change.rows[key];
    if (!runs) continue;
    const rows = was[key].slice();
    for (const run of runs) {
      for (let i = 1; i < run.length; i++) rows[run[0] + i - 1] = run[i];
    }
    figure[key] = rows;
  }
  for (const key of ['mic', 'juggle']) {
    if (!(key in change)) continue;
    if (change[key] === null) delete figure[key];
    else figure[key] = change[key];
  }
  return figure;
}
