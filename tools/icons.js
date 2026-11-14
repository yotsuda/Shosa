// The icons of the family — Lily#, Lily# Sound Font, Calamo, Cantoria, Shosa, Dettato — from one recipe
// (owner's decisions 2026-11-14, DESIGN.md 24.1):
//
//   - a 128x128 dark rounded tile (#2d2d30 -> #1e1e1e), one symbol of Emmentaler in one colour;
//   - every glyph from the SAME cut, emmentaler-14, and untouched: only sized and placed. Lily# sets two
//     glyphs as LilyPond engraves them (a G clef and the F sharp of G major: "Lily", then "#");
//   - six hues 60 degrees apart at one lightness and one strength (OKLCH 0.76, 0.13), so that no two read
//     as the same colour: Shosa 25, Cantoria 85, Calamo 145, Dettato 205, Lily# 265, Sound Font 325.
//
// It writes icon.svg and icon.png (256 px) into each repository beside this one. To run it:
//   npm install --no-save opentype.js@1.3.4 @resvg/resvg-js@2.6.2      (anywhere; NODE_PATH may point at it)
//   node tools/icons.js            write the icons
//   node tools/icons.js --sheet out/icons/final.png      only draw them side by side
const fs = require('fs');
const path = require('path');
const opentype = require('opentype.js');
const { Resvg } = require('@resvg/resvg-js');

const Cut = 14;
const projects = path.resolve(__dirname, '..', '..');
const font = opentype.loadSync(path.join(projects, 'LilySharp', 'editors', 'vscode', 'server', 'Fonts', `emmentaler-${Cut}.otf`));
const glyphs = {};
for (let i = 0; i < font.glyphs.length; i++) glyphs[font.glyphs.get(i).name] = font.glyphs.get(i);

// OKLCH -> sRGB; the chroma is lowered until the colour exists on a screen.
function oklch(L, C, h) {
  for (let c = C; c >= 0; c -= 0.002) {
    const a = c * Math.cos(h * Math.PI / 180), b = c * Math.sin(h * Math.PI / 180);
    const l = (L + 0.3963377774 * a + 0.2158037573 * b) ** 3, m = (L - 0.1055613458 * a - 0.0638541728 * b) ** 3, s = (L - 0.0894841775 * a - 1.2914855480 * b) ** 3;
    const rgb = [4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s];
    if (rgb.every(v => v >= -0.0005 && v <= 1.0005)) {
      return '#' + rgb.map(v => Math.round(255 * (v <= 0.0031308 ? 12.92 * Math.max(0, v) : 1.055 * Math.min(1, v) ** (1 / 2.4) - 0.055)).toString(16).padStart(2, '0')).join('');
    }
  }
}
const colour = hue => oklch(0.76, 0.13, hue);

// Glyphs set at positions in staff spaces (x to the right, y up; a staff space is a quarter of the em),
// then the whole fitted into a box (width, height) in the middle of the tile.
function set(items, box) {
  const all = new opentype.Path();
  for (const [name, x = 0, y = 0] of items) all.commands.push(...glyphs[name].getPath(x * 250, -y * 250, 1000).commands);
  const b = all.getBoundingBox(), k = Math.min(box[0] / (b.x2 - b.x1), box[1] / (b.y2 - b.y1));
  const ox = 64 - (b.x1 + b.x2) / 2 * k, oy = 64 - (b.y1 + b.y2) / 2 * k;
  for (const c of all.commands) {
    for (const key of ['x', 'x1', 'x2']) if (key in c) c[key] = c[key] * k + ox;
    for (const key of ['y', 'y1', 'y2']) if (key in c) c[key] = c[key] * k + oy;
  }
  return all.toPathData(3);
}
const edges = name => { const b = glyphs[name].getPath(0, 0, 1000).getBoundingBox(); return [b.x1 / 250, b.x2 / 250]; };

// The clef stands on the G line (one space under the middle line); the sharp of G major is on the top
// line (two above), half a space after the clef.
const keyOfG = [['clefs.G', 0, -1], ['accidentals.sharp', edges('clefs.G')[1] + 0.5 - edges('accidentals.sharp')[0], 2]];
// The dynamic letters, one after the other as the font spaces them.
const sforzando = [['s'], ['f', glyphs.s.advanceWidth / 250]];

const icons = [
  { name: 'Lily#', folder: 'LilySharp/editors/vscode', hue: 265, d: set(keyOfG, [104, 108]) },
  { name: 'Sound Font', folder: 'LilySharp/editors/vscode-soundfont', hue: 325, d: set(sforzando, [78, 80]) },
  { name: 'Calamo', folder: 'Calamo/extension', hue: 145, d: set([['scripts.ufermata']], [96, 96]) },      // a fermata: an eye, it reads the page
  { name: 'Cantoria', folder: 'Cantoria/editors/vscode', hue: 85, d: set([['clefs.C']], [96, 96]) },
  { name: 'Shosa', folder: 'Shosa/editors/vscode', hue: 25, d: set([['scripts.turn']], [88, 96]) },        // a turn: a word of the language, and a movement
  { name: 'Dettato', folder: 'Dettato', hue: 205, d: set([['clefs.F']], [80, 86]) },                        // an F clef: an ear, it listens
];

const tile = id => `<defs><linearGradient id="${id}" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#2d2d30"/><stop offset="100%" stop-color="#1e1e1e"/></linearGradient></defs>\n  <rect width="128" height="128" rx="16" fill="url(#${id})"/>`;
const svgOf = icon => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128">\n  ${tile('bg')}\n  <path fill="${colour(icon.hue)}" d="${icon.d}"/>\n</svg>\n`;

const sheetAt = process.argv.indexOf('--sheet');
if (sheetAt >= 0) {
  // Side by side on VS Code's dark and light grounds: large, and at the sizes of a list (28) and a tab (20).
  const W = 1500, H = 170;
  let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" font-family="Segoe UI, sans-serif"><rect width="${W / 2}" height="${H}" fill="#252526"/><rect x="${W / 2}" width="${W / 2}" height="${H}" fill="#f3f3f3"/>`;
  [0, W / 2].forEach((x0, side) => icons.forEach((icon, i) => {
    const body = `${tile(`g${side}_${i}`)}<path fill="${colour(icon.hue)}" d="${icon.d}"/>`, x = x0 + 16 + i * 90;
    svg += `<g transform="translate(${x},14) scale(0.64)">${body}</g><text x="${x}" y="114" fill="${side ? '#333' : '#ccc'}" font-size="11">${icon.name}</text><text x="${x}" y="128" fill="${side ? '#333' : '#ccc'}" font-size="11">${colour(icon.hue)}</text>`;
    svg += `<g transform="translate(${x0 + 562 + i * 31},40) scale(${28 / 128})">${body}</g><g transform="translate(${x0 + 562 + i * 31},80) scale(${20 / 128})">${body}</g>`;
  }));
  fs.writeFileSync(process.argv[sheetAt + 1], new Resvg(svg + '</svg>', { font: { loadSystemFonts: true } }).render().asPng());
} else {
  for (const icon of icons) {
    const folder = path.join(projects, icon.folder), svg = svgOf(icon);
    fs.writeFileSync(path.join(folder, 'icon.svg'), svg);
    fs.writeFileSync(path.join(folder, 'icon.png'), new Resvg(svg, { fitTo: { mode: 'width', value: 256 } }).render().asPng());
    console.log(`${icon.name}  ${colour(icon.hue)}  ${path.join(folder, 'icon.svg')}`);
  }
}
