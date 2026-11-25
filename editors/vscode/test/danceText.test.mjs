// What Add a Dance… writes (src/danceText.ts): the beginning of a dance to a song, and what the chat is asked
// (DESIGN.md 123.3). The TypeScript is turned into JavaScript here with esbuild, as the build does.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { transformSync } from 'esbuild';

const ts = readFileSync(new URL('../src/danceText.ts', import.meta.url), 'utf8');
const { code } = transformSync(ts, { loader: 'ts', format: 'esm' });
const { danceSkeleton, danceRequest, shosaName } = await import('data:text/javascript,' + encodeURIComponent(code));

test('a dancer for each part that sings, side by side, each singing its part', () => {
  const text = danceSkeleton('hello.lys', undefined, [{ part: 'zunda', model: 'zundamon.vrm' }, { part: 'metan' }]);
  assert.equal(text, [
    '// A dance to hello.lys. Shosa: Add a Dance… wrote this beginning; the sections are the choreography.',
    'music "hello.lys"',
    '',
    'role zunda',
    'role metan',
    'dancer zunda humanoid "zundamon.vrm" sings zunda on zunda at -0.8 0',
    'dancer metan humanoid sings metan on metan at 0.8 0',
    '',
    'score { dancer zunda  dancer metan }',
    '',
  ].join('\n'));
});

test('a song without words has one dancer who sings nothing; the score is named', () => {
  const text = danceSkeleton('bicycle.lys', 'japanese', []);
  assert.match(text, /^music "bicycle\.lys" score japanese$/m);
  assert.match(text, /^dancer solo humanoid on solo$/m);
  assert.match(text, /^score \{ dancer solo \}$/m);
});

test('a part name that is not a Shosa name is made one', () => {
  assert.equal(shosaName('vox'), 'vox');
  assert.equal(shosaName('lead-2'), 'lead_2');
  assert.equal(shosaName('2nd'), 'd2nd');
});

test('the chat is sent to the grammar, the song tool with the score, and the method', () => {
  const asked = danceRequest('C:/d/bicycle.shosa', 'C:/d/bicycle.lys', 'japanese', ['vox'], 'a cheerful dance');
  assert.match(asked, /in the file C:\/d\/bicycle\.shosa/);
  assert.match(asked, /a dancer for each part that sings \(vox\)/);
  assert.match(asked, /shosa_grammar/);
  assert.match(asked, /shosa_song \(file: C:\/d\/bicycle\.lys, score: japanese\)/);
  assert.match(asked, /"Choreographing the words"/);
  assert.match(asked, /shosa_check until it says no problems/);
  assert.match(asked, /The dance: a cheerful dance$/);
});
