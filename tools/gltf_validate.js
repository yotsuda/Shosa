// node validate.js <node_modules dir> file.glb ...   — runs the Khronos glTF Validator over each file.
const path = require('path');
const fs = require('fs');
const validator = require(path.join(process.argv[2], 'gltf-validator'));
(async () => {
  for (const file of process.argv.slice(3)) {
    const report = await validator.validateBytes(new Uint8Array(fs.readFileSync(file)), { uri: path.basename(file), maxIssues: 50 });
    const i = report.issues;
    console.log(`${path.basename(file)}: errors ${i.numErrors}, warnings ${i.numWarnings}, infos ${i.numInfos}, hints ${i.numHints}`);
    const seen = new Map();
    for (const m of i.messages) seen.set(m.code, (seen.get(m.code) || { n: 0, m }));
    for (const m of i.messages) seen.get(m.code).n++;
    for (const [code, { n, m }] of seen) console.log(`  ${['error', 'warning', 'info', 'hint'][m.severity]} ${code} x${n}: ${m.message} (${m.pointer || ''})`);
  }
})();
