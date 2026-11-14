// Two bundles: the extension (Node, inside VS Code) and the preview (a browser page inside
// a webview). The preview carries three.js with it: a webview cannot load from a CDN.
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

console.log('built dist/extension.js and dist/preview.js');
