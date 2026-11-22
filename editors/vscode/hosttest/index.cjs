// Run inside a real VS Code (tools\test-extension-host.ps1): the things only an editor can show.
//
//   - the three language model tools are registered, and answer when invoked the way an
//     assistant invokes them (vscode.lm.invokeTool): the grammar, the check, the sheet as an image
//   - shosa_check reads the document as the editor has it, not as the disk has it
//   - a new terminal finds `shosa` by name (the packaged extension only: it puts server\ on PATH)
//
// What this cannot show: whether a model chooses to call the tools, and what it sees in the image.
//
// The result is written as JSON to the file named by SHOSA_HOSTTEST_OUT; run() rejects if a check fails.

const vscode = require('vscode');
const fs = require('fs');
const os = require('os');
const path = require('path');

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));

async function until(what, test, seconds = 30) {
  for (let i = 0; i < seconds * 4; i++) {
    const value = await test();
    if (value) {
      return value;
    }
    await sleep(250);
  }
  throw new Error(`timed out waiting for ${what}`);
}

const describe = part => {
  if (part instanceof vscode.LanguageModelTextPart) {
    return { kind: 'text', value: part.value };
  }
  // LanguageModelDataPart (after 1.95): data and mimeType.
  if (part && part.data && part.mimeType) {
    return { kind: 'data', mimeType: part.mimeType, bytes: part.data.length, head: Buffer.from(part.data.slice(0, 8)).toString('hex') };
  }
  return { kind: typeof part, keys: part ? Object.keys(part) : [] };
};

async function invoke(name, input) {
  const result = await vscode.lm.invokeTool(name, { input, toolInvocationToken: undefined });
  return result.content.map(describe);
}

exports.run = async function run() {
  const out = process.env.SHOSA_HOSTTEST_OUT;
  const report = { vscode: vscode.version, checks: [], failures: [] };
  const check = (name, ok, detail) => {
    report.checks.push({ name, ok: !!ok, detail });
    if (!ok) {
      report.failures.push(name);
    }
  };

  try {
    const folder = vscode.workspace.workspaceFolders[0].uri.fsPath;
    const file = path.join(folder, 'fighter.shosa');
    const document = await vscode.workspace.openTextDocument(file);
    await vscode.window.showTextDocument(document);

    // Registered: the declarations of package.json, and an implementation behind each.
    const names = ['shosa_grammar', 'shosa_check', 'shosa_sheet'];
    await until('the tools', () => names.every(n => vscode.lm.tools.some(t => t.name === n)));
    check('the three tools are listed', true, vscode.lm.tools.filter(t => t.name.startsWith('shosa_')).map(t => t.name));

    // The grammar. The first call also waits for the language server to start.
    const grammar = await until('the grammar', async () => {
      try {
        return await invoke('shosa_grammar', {});
      } catch (error) {
        report.lastError = String(error);
        return undefined;
      }
    }, 60);
    const grammarText = grammar.map(p => p.value ?? '').join('');
    check('shosa_grammar returns the grammar and how to work here',
      grammarText.includes('A complete file') && grammarText.includes('# Working in this editor'),
      { characters: grammarText.length });

    // The check, by full path and by the path from the workspace folder.
    const checked = (await invoke('shosa_check', { file })).map(p => p.value).join('');
    check('shosa_check: no problems, scores, marks', /fighter\.shosa: no problems/.test(checked) && /^ +Combo: \d/m.test(checked) && /!hit/.test(checked),
      checked.split('\n').slice(0, 4));
    const relative = (await invoke('shosa_check', { file: 'fighter.shosa' })).map(p => p.value).join('');
    check('shosa_check takes a path from the workspace folder', relative === checked);
    // One score, and how the body is turned at a moment (`shosa check --score Combo --at 1.5`).
    const turned = (await invoke('shosa_check', { file, score: 'Combo', at: 1.5 })).map(p => p.value).join('');
    check('shosa_check takes score and at', /^ +Combo at beat 1\.5 = 0\.75 s: hips [\d.]+° (left|right), shoulders/m.test(turned) && !/^ +Idle:/m.test(turned),
      turned.split('\n').filter(line => / at beat /.test(line)));

    // An edit that is not saved: the check must see it.
    const edit = new vscode.WorkspaceEdit();
    edit.insert(document.uri, new vscode.Position(document.lineCount, 0), '\nnonsense here\n');
    await vscode.workspace.applyEdit(edit);
    const dirty = (await invoke('shosa_check', { file })).map(p => p.value).join('');
    check('shosa_check sees an edit that is not saved', document.isDirty && /error SH\d+/.test(dirty) && /Fix every line/.test(dirty),
      dirty.split('\n').slice(0, 2));
    await vscode.commands.executeCommand('workbench.action.files.revert');

    // The sheet: one image per score, or the one asked for.
    const one = await invoke('shosa_sheet', { file, score: 'Combo', view: 'side', step: 0.5 });
    const images = one.filter(p => p.kind === 'data');
    check('shosa_sheet returns one PNG for one score',
      images.length === 1 && images[0].mimeType === 'image/png' && images[0].head === '89504e470d0a1a0a',
      one.map(p => p.kind === 'text' ? p.value.slice(0, 120) : p));
    const all = await invoke('shosa_sheet', { file });
    check('shosa_sheet returns a PNG for every score', all.filter(p => p.kind === 'data').length === 7,
      all.filter(p => p.kind === 'data').length);
    const none = await invoke('shosa_sheet', { file, score: 'Nope' });
    check('shosa_sheet says when there is no such score', none.length === 1 && /no movement to draw/.test(none[0].value), none);

    // The preview's Export buttons, pressed by their commands (a path given: no dialog). The
    // video is filmed and encoded by the preview's page: this shows the editor has the encoders.
    await vscode.window.showTextDocument(document);
    const exports = fs.mkdtempSync(path.join(os.tmpdir(), 'shosa-hosttest-export-'));
    const head = (file, from, to) => fs.existsSync(file) ? fs.readFileSync(file).subarray(from, to).toString('latin1') : '(no file)';
    const glb = await vscode.commands.executeCommand('shosa.exportModel', path.join(exports, 'a.glb'));
    check('Export Model writes a .glb', glb.length === 1 && head(glb[0], 0, 4) === 'glTF', glb);
    const vrma = await vscode.commands.executeCommand('shosa.exportModel', path.join(exports, 'a.vrma'));
    check('Export Model writes a .vrma', vrma.length >= 1 && head(vrma[0], 0, 4) === 'glTF', vrma);
    const bvh = await vscode.commands.executeCommand('shosa.exportModel', path.join(exports, 'a.bvh'));
    check('Export Model writes a .bvh', bvh.length >= 1 && head(bvh[0], 0, 9) === 'HIERARCHY', bvh);
    const started = Date.now();
    const mp4 = await vscode.commands.executeCommand('shosa.exportVideo', path.join(exports, 'a.mp4'));
    const video = mp4.length === 1 ? fs.readFileSync(mp4[0]) : Buffer.alloc(0);
    check('Export MP4 writes an H.264 video', video.length > 20000 && head(mp4[0], 4, 8) === 'ftyp' && video.includes('avc1') && video.includes('moov'),
      { files: mp4, bytes: video.length, seconds: (Date.now() - started) / 1000 });
    // The fighter has no song: the video has the preview's clicks and claps in it (AAC), through this machine's sound font.
    check('Export MP4 of a score without a song has its clicks', video.includes('mp4a'), { bytes: video.length });
    // A dance to a song: the video has the song in it (AAC), rendered through this machine's sound font.
    const dance = path.join(folder, 'dance.shosa');
    if (fs.existsSync(dance)) {
      await vscode.window.showTextDocument(await vscode.workspace.openTextDocument(dance));
      await sleep(3000);   // the preview follows the editor, and asks for the song's notes
      const began = Date.now();
      const sung = await vscode.commands.executeCommand('shosa.exportVideo', path.join(exports, 'dance.mp4'));
      const bytes = sung.length === 1 ? fs.readFileSync(sung[0]) : Buffer.alloc(0);
      check('Export MP4 of a dance has its song', bytes.includes('avc1') && bytes.includes('mp4a'),
        { files: sung, bytes: bytes.length, seconds: (Date.now() - began) / 1000 });
    }
    if (process.env.SHOSA_HOSTTEST_KEEP) {
      fs.cpSync(exports, process.env.SHOSA_HOSTTEST_KEEP, { recursive: true });
    }
    fs.rmSync(exports, { recursive: true, force: true });

    // Ctrl+Shift+Up/Down on the file of a dancer line: the models of shosa.models.folder in turn (models.ts).
    // The commands behind the keys; the files need only be named .vrm.
    const models = path.join(folder, 'models');
    fs.mkdirSync(path.join(models, 'sub'), { recursive: true });
    for (const name of ['a.vrm', 'b.vrm', path.join('sub', 'c.vrm'), 'notes.txt']) {
      fs.writeFileSync(path.join(models, name), '');
    }
    const dancing = path.join(folder, 'dressed.shosa');
    fs.writeFileSync(dancing, 'role solo\ndancer solo humanoid "elsewhere.vrm" on solo\n');
    await vscode.workspace.getConfiguration('shosa').update('models.folder', 'models', vscode.ConfigurationTarget.Global);
    const dressedDoc = await vscode.workspace.openTextDocument(dancing);
    const dressed = await vscode.window.showTextDocument(dressedDoc);
    const onName = new vscode.Position(1, 'dancer solo humanoid "else'.length);
    dressed.selection = new vscode.Selection(onName, onName);
    const named = () => /"([^"]*)"/.exec(dressedDoc.lineAt(1).text)[1];
    const seen = [];
    for (const command of ['shosa.nextModel', 'shosa.nextModel', 'shosa.nextModel', 'shosa.nextModel', 'shosa.previousModel', 'shosa.previousModel']) {
      await vscode.commands.executeCommand(command);
      seen.push(named());
    }
    check('Next/Previous Character go through the models folder', seen.join(' ') === 'models/a.vrm models/b.vrm models/sub/c.vrm models/a.vrm models/sub/c.vrm models/b.vrm', seen);
    check('the cursor stays in the file name', dressed.selection.active.line === 1 && /"[^"]*$/.test(dressedDoc.lineAt(1).text.slice(0, dressed.selection.active.character)),
      dressed.selection.active);
    // Off the file name the commands do nothing (and the keys are not ours there: shosa.onModelPath).
    const before = dressedDoc.getText();
    dressed.selection = new vscode.Selection(new vscode.Position(0, 2), new vscode.Position(0, 2));
    await vscode.commands.executeCommand('shosa.nextModel');
    check('off a dancer\'s file the command changes nothing', dressedDoc.getText() === before);
    await vscode.commands.executeCommand('workbench.action.files.revert');
    await vscode.workspace.getConfiguration('shosa').update('models.folder', undefined, vscode.ConfigurationTarget.Global);
    // The link before the setting's box (its description's last line) runs a command that is there.
    const ownJson = vscode.extensions.all.find(e => e.packageJSON.name === 'shosa').packageJSON;
    const link = /\(command:([\w.]+)\)\*\*$/.exec(ownJson.contributes.configuration.properties['shosa.models.folder'].markdownDescription);
    check('the setting\'s "Choose a folder…" link runs a registered command', !!link && (await vscode.commands.getCommands(true)).includes(link[1]), link && link[1]);

    // A new terminal finds `shosa` by name: only where the extension carries the programs.
    const extension = vscode.extensions.all.find(e => e.packageJSON.name === 'shosa');
    const packaged = fs.existsSync(path.join(extension.extensionPath, 'server', process.platform === 'win32' ? 'shosa.exe' : 'shosa'));
    if (packaged) {
      const answer = path.join(os.tmpdir(), `shosa-hosttest-${process.pid}.txt`);
      fs.rmSync(answer, { force: true });
      const terminal = vscode.window.createTerminal({ name: 'shosa host test' });
      terminal.sendText(`shosa check "${file}" > "${answer}" 2>&1`);
      let said = '';
      try {
        said = await until('the terminal', () => fs.existsSync(answer) && fs.readFileSync(answer).length > 0 && fs.readFileSync(answer).toString('utf8').replace(/\0/g, ''), 40);
      } catch (error) {
        said = String(error);
      }
      check('a new terminal finds shosa by name', /no problems/.test(said), said.split('\n').slice(0, 2));
      terminal.dispose();
      fs.rmSync(answer, { force: true });
    } else {
      report.checks.push({ name: 'a new terminal finds shosa by name', ok: null, detail: 'skipped: this extension carries no server\\shosa (running from the repository)' });
    }
  } catch (error) {
    report.failures.push('threw: ' + (error && error.stack || error));
  }

  if (out) {
    fs.writeFileSync(out, JSON.stringify(report, null, 2));
  }
  if (report.failures.length > 0) {
    throw new Error(report.failures.join('; '));
  }
};
