// The edits of the tool shosa_edit (src/textEdits.ts), after Lily#'s lilysharp_proposeScore: all made or
// none, and what is wrong said for the model to mend.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { transformSync } from 'esbuild';

const ts = readFileSync(new URL('../src/textEdits.ts', import.meta.url), 'utf8');
const { code } = transformSync(ts, { loader: 'ts', format: 'esm' });
const { applyEdits } = await import('data:text/javascript,' + encodeURIComponent(code));

const dance = 'section A {\n  solo {\n    rightArm { down fwd@sharp:2 down }\n    leftArm  { down fwd@sharp:2 down }\n  }\n}\n';

test('an edit found once is made, and several are made together', () => {
  const made = applyEdits(dance, [
    { old: 'rightArm { down fwd@sharp:2 down }', new: 'rightArm { down fwd:2@sharp down }' },
    { old: 'section A', new: 'section Verse' },
  ]);
  assert.equal(made.text, dance.replace('rightArm { down fwd@sharp:2', 'rightArm { down fwd:2@sharp').replace('section A', 'section Verse'));
});

test('the same fix on many lines is one edit with all', () => {
  const made = applyEdits(dance, [{ old: 'fwd@sharp:2', new: 'fwd:2@sharp', all: true }]);
  assert.equal(made.text, dance.replaceAll('fwd@sharp:2', 'fwd:2@sharp'));
});

test('an old found more than once without all, or not at all, changes nothing and says why', () => {
  assert.match(applyEdits(dance, [{ old: 'fwd@sharp:2', new: 'x' }]).error, /is in the file 2 times.*"all": true/);
  assert.match(applyEdits(dance, [{ old: 'section A', new: 'B' }, { old: 'kick', new: 'x' }]).error, /^Edit 2: its "old" is not in the file/);
  assert.match(applyEdits(dance, [{ old: '  ', new: 'x' }]).error, /empty "old"/);
  assert.match(applyEdits(dance, [{ old: 'section A' }]).error, /needs both "old" and "new"/);
});

test('edits that overlap are refused', () => {
  assert.match(applyEdits(dance, [{ old: 'section A {', new: 'x' }, { old: 'A {\n  solo', new: 'y' }]).error, /Edits 1 and 2 overlap/);
});

test('an old written with \\n finds a file with \\r\\n, and the new keeps the file\'s newlines', () => {
  const crlf = dance.replace(/\n/g, '\r\n');
  const made = applyEdits(crlf, [{ old: '  solo {\n    rightArm', new: '  solo {\n    // the right\n    rightArm' }]);
  assert.ok(made.text.includes('  solo {\r\n    // the right\r\n    rightArm'));
  assert.ok(!/[^\r]\n/.test(made.text));
});

test('an old with spaces round it the file does not have is found trimmed', () => {
  const made = applyEdits(dance, [{ old: '\n    leftArm  { down fwd@sharp:2 down }   \n\n', new: ' leftArm  { down }' }]);
  assert.ok(made.text.includes('    leftArm  { down }\n'));
});
