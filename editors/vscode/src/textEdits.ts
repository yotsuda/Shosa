// The edits of the tool shosa_edit (tools.ts), after Lily#'s lilysharp_proposeScore (aiTransformCore.ts
// applyEdits): each `old` copied from the file, with its `new`. All of them are made or none, and what
// is wrong comes back for the model to mend: an `old` not in the file, one found more than once without
// `all`, edits that overlap. No vscode here, so that it is tested on its own (test/textEdits.test.mjs).

export interface TextEdit { old: string; new: string; all?: boolean }

/** `old` written with the newlines of the text it is looked for in (a model writes \n; a file may have \r\n). */
function sameNewlines(s: string, like: string): string {
  const lf = s.replace(/\r\n/g, '\n');
  return like.includes('\r\n') ? lf.replace(/\n/g, '\r\n') : lf;
}

function occurrences(text: string, part: string): number[] {
  const at: number[] = [];
  for (let i = text.indexOf(part); i >= 0; i = text.indexOf(part, i + part.length)) {
    at.push(i);
  }
  return at;
}

/** The text with the edits made, or what is wrong with them (and nothing made). */
export function applyEdits(text: string, edits: TextEdit[]): { text: string } | { error: string } {
  const spans: { lo: number; hi: number; text: string; n: number }[] = [];
  for (let i = 0; i < edits.length; i++) {
    const n = i + 1;
    if (typeof edits[i]?.old !== 'string' || typeof edits[i]?.new !== 'string') {
      return { error: `Edit ${n} needs both "old" and "new", as text.` };
    }
    let old = sameNewlines(edits[i].old, text);
    let neu = sameNewlines(edits[i].new, text);
    if (old.trim().length === 0) {
      return { error: `Edit ${n} has an empty "old". Copy the text it replaces; to insert, copy a line next to the place and repeat it in "new" with the insertion.` };
    }
    let at = occurrences(text, old);
    if (at.length === 0 && old.trim() !== old) {
      old = old.trim();
      neu = neu.trim();
      at = occurrences(text, old);
    }
    if (at.length === 0) {
      return { error: `Edit ${n}: its "old" is not in the file. Copy it exactly from the file as it stands now, every character and space.` };
    }
    if (at.length > 1 && !edits[i].all) {
      return { error: `Edit ${n}: its "old" is in the file ${at.length} times. Give more of the text around it to make it one, or "all": true to change every one.` };
    }
    if (old !== neu) {
      for (const lo of at) {
        spans.push({ lo, hi: lo + old.length, text: neu, n });
      }
    }
  }
  spans.sort((a, b) => a.lo - b.lo);
  for (let i = 1; i < spans.length; i++) {
    if (spans[i].lo < spans[i - 1].hi) {
      return { error: spans[i].n === spans[i - 1].n
        ? `Edit ${spans[i].n}: the places of its "old" overlap one another. Give a longer "old".`
        : `Edits ${spans[i - 1].n} and ${spans[i].n} overlap. Make them one edit.` };
    }
  }
  let out = '';
  let from = 0;
  for (const span of spans) {
    out += text.slice(from, span.lo) + span.text;
    from = span.hi;
  }
  return { text: out + text.slice(from) };
}
