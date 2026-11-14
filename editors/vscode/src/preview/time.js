// Where the beats of a clip are: in seconds (its tempos) and in its bars (its sections and
// times). A clip danced to a song that changes tempo or time on the way has several of each;
// any other has one. The same arithmetic as TempoMap and Bars in Shosa.Core.
//
// One file for both previews: the VS Code webview bundles it, and `shosa preview` writes it
// into the page (Shosa.Core embeds it and drops the `export`). So it imports nothing.

const temposOf = clip => clip.tempos?.length ? clip.tempos : [{ beat: 0, bpm: clip.tempo }];
const metersOf = clip => clip.meters?.length ? clip.meters : [{ beat: 0, beatsPerBar: clip.beatsPerBar }];

// Beats a minute at a beat.
export function tempoAt(clip, beat) {
  let bpm = clip.tempo;
  for (const t of temposOf(clip)) if (t.beat <= beat + 1e-9) bpm = t.bpm;
  return bpm;
}

// The second a beat falls on.
export function secondsAt(clip, beat) {
  const tempos = temposOf(clip);
  let seconds = 0;
  for (let i = 0; i < tempos.length; i++) {
    const next = i + 1 < tempos.length ? tempos[i + 1].beat : Infinity;
    seconds += (Math.min(beat, next) - tempos[i].beat) * 60 / tempos[i].bpm;
    if (beat <= next) break;
  }
  return seconds;
}

// The beat a second falls on.
export function beatAt(clip, seconds) {
  const tempos = temposOf(clip);
  for (let i = 0; i < tempos.length; i++) {
    const span = i + 1 < tempos.length ? (tempos[i + 1].beat - tempos[i].beat) * 60 / tempos[i].bpm : Infinity;
    if (seconds <= span) return tempos[i].beat + seconds * tempos[i].bpm / 60;
    seconds -= span;
  }
  return 0;
}

// The marks (`!hit`) that have just arrived at a beat: each from its moment until `ring`
// seconds after it, so that one is seen in passing and when the clip is stopped on it.
export function marksAt(clip, beat, ring = 0.6) {
  const now = secondsAt(clip, beat);
  return (clip.marks ?? []).filter(m => m.beat <= beat + 1e-9 && now - secondsAt(clip, m.beat) < ring);
}

// What to write for them: each name once, with who it is for when the clip has several dancers.
export function markText(clip, marks) {
  const names = [...new Set(marks.map(m => m.name))];
  const several = (clip.figures?.length ?? 0) > 1;
  return names.map(name => '!' + name + (several ? ' ' + marks.filter(m => m.name === name).map(m => m.dancer).join(', ') : '')).join('   ');
}

// The bar a beat is in and the beat within it (from 0). The bars are each section's own: a
// song's upbeat (`pickup`) puts them off the multiples of the bar, and belongs to the bar
// before (bar 0 at the very start); a new time begins on a bar line.
export function barAt(clip, beat) {
  const meters = metersOf(clip);
  let section = null;
  for (const s of clip.sections ?? []) if (s.t0 <= beat + 1e-9) section = s;
  let line = section ? section.t0 + (section.pickup ?? 0) : 0, bar = section ? section.bar ?? 1 : 1;
  if (beat < line - 1e-9) {
    let perBar = meters[0].beatsPerBar;
    for (const m of meters) if (m.beat <= beat + 1e-9) perBar = m.beatsPerBar;
    return [bar - 1, perBar + beat - line];
  }
  for (let i = 0; i < meters.length; i++) {
    const end = i + 1 < meters.length ? meters[i + 1].beat : Infinity;
    if (end <= line + 1e-9) continue;
    const perBar = Math.max(1, meters[i].beatsPerBar);
    if (beat < end - 1e-9) {
      const whole = Math.floor((beat - line) / perBar + 1e-9);
      return [bar + whole, Math.max(0, beat - line - whole * perBar)];
    }
    bar += Math.ceil((end - line) / perBar - 1e-9);
    line = end;
  }
  return [bar, 0];
}
