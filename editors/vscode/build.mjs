// Two bundles: the extension (Node, inside VS Code) and the preview (a browser page inside
// a webview). The preview carries three.js with it: a webview cannot load from a CDN.
import { copyFileSync } from 'node:fs';
import { build } from 'esbuild';

await build({
  entryPoints: ['src/extension.ts'],
  bundle: true,
  platform: 'node',
  format: 'cjs',
  external: ['vscode'],
  outfile: 'dist/extension.js',
  sourcemap: true,
});

await build({
  entryPoints: ['src/preview/main.js'],
  bundle: true,
  platform: 'browser',
  format: 'iife',
  outfile: 'dist/preview.js',
  minify: true,
});

// The synthesiser's audio-thread half is a file of its own: the page hands it to audioWorklet.addModule.
copyFileSync('node_modules/spessasynth_lib/dist/spessasynth_processor.min.js', 'dist/spessasynth_processor.min.js');

console.log('built dist/extension.js, dist/preview.js and dist/spessasynth_processor.min.js');
