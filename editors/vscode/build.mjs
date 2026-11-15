// Two bundles: the extension (Node, inside VS Code) and the preview (a browser page inside
// a webview). The preview carries three.js with it: a webview cannot load from a CDN.
import { copyFileSync, writeFileSync } from 'node:fs';
import { build } from 'esbuild';

const extension = await build({
  entryPoints: ['src/extension.ts'],
  bundle: true,
  platform: 'node',
  format: 'cjs',
  external: ['vscode'],
  outfile: 'dist/extension.js',
  sourcemap: true,
  metafile: true,
});

const preview = await build({
  entryPoints: ['src/preview/main.js'],
  bundle: true,
  platform: 'browser',
  format: 'iife',
  outfile: 'dist/preview.js',
  minify: true,
  metafile: true,
});

// The synthesiser's audio-thread half is a file of its own: the page hands it to audioWorklet.addModule.
copyFileSync('node_modules/spessasynth_lib/dist/spessasynth_processor.min.js', 'dist/spessasynth_processor.min.js');

// The packages whose code went into dist, for the third-party notices (tools\third-party-notices.ps1).
// Not shipped itself: tools\pack-extension.ps1 leaves *.packages.json out.
const packages = new Set(['node_modules/spessasynth_lib']);
for (const result of [extension, preview]) {
  for (const input of Object.keys(result.metafile.inputs)) {
    // The innermost package (a nested node_modules is another copy, maybe another version).
    const match = /^(.*node_modules\/(?:@[^/]+\/)?[^/]+)\//.exec(input);
    if (match) packages.add(match[1]);
  }
}
writeFileSync('dist/bundled.packages.json', JSON.stringify([...packages].sort(), null, 2) + '\n');

console.log(`built dist/extension.js, dist/preview.js and dist/spessasynth_processor.min.js (${packages.size} packages bundled)`);
