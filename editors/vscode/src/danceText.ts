// The texts Add a Dance… writes (addDance.ts), apart from VS Code so the tests read them: the beginning of
// the dance and what the chat is asked (DESIGN.md 123.3).

/** A part's name as a Shosa name: the Lily# part names are words already; anything else is made one. */
export function shosaName(part: string): string {
  const word = part.replace(/[^A-Za-z0-9_]/g, '_');
  return /^[A-Za-z_]/.test(word) ? word : 'd' + word;
}

/**
 * The beginning of the dance: what the song gives and what was chosen, and nothing of the choreography
 * (the sections are the chat's). It compiles as it stands: a section the file does not have is stood through.
 * A dancer of a part sings it; with no part, one dancer `solo` who sings nothing.
 */
export function danceSkeleton(song: string, score: string | undefined, dancers: { part: string; model?: string }[]): string {
  const named = (dancers.length > 0 ? dancers : [{ part: '' }]).map(d => ({ ...d, name: d.part ? shosaName(d.part) : 'solo' }));
  const lines = [
    `// A dance to ${song}. Shosa: Add a Dance… wrote this beginning; the sections are the choreography.`,
    `music "${song}"${score ? ` score ${score}` : ''}`,
    '',
    ...named.map(d => `role ${d.name}`),
  ];
  named.forEach((d, i) => {
    // Side by side, 1.6 m apart, facing the front.
    const x = (i - (named.length - 1) / 2) * 1.6;
    lines.push(`dancer ${d.name} humanoid${d.model ? ` "${d.model}"` : ''}${d.part ? ` sings ${d.part}` : ''} on ${d.name}`
      + (named.length > 1 ? ` at ${Number(x.toFixed(2))} 0` : ''));
  });
  lines.push('', `score { ${named.map(d => `dancer ${d.name}`).join('  ')} }`, '');
  return lines.join('\n');
}

/** What the chat is asked: where the dance and the song are, what to read first, and what the dance should be. */
export function danceRequest(dance: string, songFile: string, score: string | undefined, singing: string[], wanted: string): string {
  return `Choreograph a dance in Shosa to the Lily# song ${songFile}, in the file ${dance} — it is open: write it with the tool shosa_edit. `
    + `It already names the song${singing.length ? ` and a dancer for each part that sings (${singing.join(', ')})` : ' and a dancer'}; `
    + 'keep those lines and write the sections.\n'
    + 'First read the Shosa grammar with the tool shosa_grammar and the choreography guide with the tool shosa_guide, '
    + 'unless you have them in this conversation, '
    + `then read the song with the tool shosa_song (file: ${songFile}${score ? `, score: ${score}` : ''}). `
    + 'Work as the guide says: the plan first (the vocabulary files to use, every section\'s role and energy, the motifs), '
    + 'then the head of the file, then one section a step, each reply short. '
    + 'Write the dance as "Choreographing the words" in the grammar says: a section for each of the song\'s sections, '
    + 'the moves placed on the words by bar and beat. Check until it says no problems, and look at each section with shosa_sheet.\n'
    // A model that met the same error on many lines ran a script in the terminal to fix them all, and the chat
    // stopped to ask the user ("Run in terminal: Fix @sharp/length ordering in shosa file", 2026-11-27).
    // shosa_edit asks nothing (tools.ts), as Lily#'s lilysharp_proposeScore.
    + 'Make every change with shosa_edit ("all": true for the same fix on many lines), '
    + 'not your own tool for editing files, and do not edit the file by running commands or scripts in the terminal: '
    + 'those stop to ask the user.\n'
    + `The dance: ${wanted}`;
}
