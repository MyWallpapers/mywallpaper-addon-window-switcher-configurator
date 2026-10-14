import { spawn } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mkdir, writeFile, readFile } from 'node:fs/promises';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const executable = process.argv[2] || resolve(root, 'native/out/windows-x86_64/companion.exe');
const timeout = (message, ms = 5000) => new Promise((_, reject) => { const timer = setTimeout(() => reject(new Error(message)), ms); timer.unref(); });
function session() {
  const child = spawn(executable, [], { cwd: root, windowsHide: true, env: { ...process.env, MYWALLPAPER_PROTOCOL: 'process-v2' }, stdio: ['pipe', 'pipe', 'pipe'] });
  let buffer = Buffer.alloc(0), stderr = '', record = [], queued = [], pending = [];
  child.stderr.on('data', bytes => { stderr = (stderr + bytes).slice(-4096); });
  child.stdout.on('data', bytes => {
    buffer = Buffer.concat([buffer, bytes]);
    while (buffer.length >= 4) {
      const header = buffer.readUInt32LE(), kind = header >>> 30, length = header & 0x3fffffff;
      if (length < 1 || length > 1048576) throw new Error('Invalid output chunk');
      if (buffer.length < 4 + length) break;
      if (record.length ? ![2, 3].includes(kind) : ![0, 1].includes(kind)) throw new Error('Output chunk order');
      record.push(buffer.subarray(4, 4 + length)); buffer = buffer.subarray(4 + length);
      if ([0, 3].includes(kind)) {
        const frame = JSON.parse(Buffer.concat(record)); record = [];
        if (pending.length) pending.shift()(frame); else queued.push(frame);
      }
    }
  });
  const exited = new Promise((resolve, reject) => { child.on('exit', (code, signal) => resolve({ code, signal })); child.on('error', reject); });
  const read = async () => Promise.race([queued.length ? Promise.resolve(queued.shift()) : new Promise(resolve => pending.push(resolve)), timeout('Companion frame timed out')]);
  const send = value => {
    const bytes = Buffer.from(JSON.stringify(value));
    for (let offset = 0; offset < bytes.length; offset += 1048576) {
      const payload = bytes.subarray(offset, offset + 1048576);
      const kind = bytes.length <= 1048576 ? 0 : offset === 0 ? 1 : offset + payload.length === bytes.length ? 3 : 2;
      const header = Buffer.alloc(4); header.writeUInt32LE(((kind << 30) | payload.length) >>> 0);
      child.stdin.write(header); child.stdin.write(payload);
    }
  };
  return { child, send, read, exited, stderr: () => stderr };
}
const manifest = JSON.parse(await readFile(resolve(root, 'manifest.json'), 'utf8'));
const init = { type: 'init', v: 5, addonId: 'window-switcher-local-smoke', artifactDigest: 'sha256:' + 'a'.repeat(64), version: manifest.version, targetArchitecture: 'windows-x86_64', scratchDirectory: root, layerSettings: {}, deviceSettings: { enabled: false } };
const message = op => ({ type: 'message', v: 5, surface: 'interface', instanceId: 'fixture:interface:0', displayIndex: 0, payload: { op } });
const results = [];
async function ready(s) { s.send(init); let frame; do { frame = await s.read(); } while (frame.type === 'progress'); if (frame.type !== 'ready' || frame.v !== 5) throw new Error('Ready v5 missing'); }
async function stop(s) { s.send({ type: 'shutdown', v: 5 }); const result = await Promise.race([s.exited, timeout('Shutdown timed out')]); if (result.code !== 0) throw new Error(`Shutdown failed: ${JSON.stringify(result)}`); }
const active = [];
try {
  const s = session(); active.push(s); await ready(s); results.push('init/progress/ready-v5');
  s.send(message('probeTaskView')); const capability = await s.read();
  if (capability.payload.available !== false || capability.payload.reason !== 'private-shell-symbols-unavailable') throw new Error('Native Task View must remain unqualified');
  results.push('Task View capability remains unavailable');
  s.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: { gap: 1000 } });
  if ((await s.read()).payload.kind !== 'window-switcher.settingsRejected') throw new Error('Invalid snapshot did not get rejected');
  results.push('invalid settings rejected without replacing snapshot');
  s.send(message('status')); if ((await s.read()).payload.status.keyboardInstalled) throw new Error('Rejected snapshot changed disabled state');
  s.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: { enabled: true, activationMode: 'shortcut' } });
  s.send(message('status')); if (!(await s.read()).payload.status.keyboardInstalled) throw new Error('Windows keyboard hook not installed');
  results.push('dedicated keyboard hook installed on Windows');
  s.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: { enabled: false } });
  s.send(message('status')); const disabled = (await s.read()).payload.status;
  if (disabled.keyboardInstalled || disabled.pollingEnabled || disabled.livePreviews || disabled.sessionOpen) throw new Error('Disable did not release native resources');
  results.push('disable releases hook/poll/session/thumbnails');
  s.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: { enabled: true, profile: 'paper' } });
  s.send(message('status')); const resumed = (await s.read()).payload.status;
  if (!resumed.keyboardInstalled || !resumed.pollingEnabled) throw new Error('Reactivation did not restart resources');
  s.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: { enabled: false } });
  results.push('reactivation accepts a new profile and restarts resources');
  await stop(s); results.push('graceful shutdown');
  const eof = session(); active.push(eof); await ready(eof); eof.child.stdin.end();
  if ((await Promise.race([eof.exited, timeout('EOF cleanup timed out')])).code !== 0) throw new Error('EOF failed');
  results.push('stdin EOF stops companion');
  const bad = session(); active.push(bad); bad.send({ type: 'settings', v: 5, layerSettings: {}, deviceSettings: {} });
  if ((await bad.read()).type !== 'error') throw new Error('Invalid initial frame accepted');
  await Promise.race([bad.exited, timeout('Malformed init did not exit')]); results.push('invalid init emits protocol error and exits');
  await mkdir(resolve(root, 'docs/evidence'), { recursive: true });
  await writeFile(resolve(root, 'docs/evidence/protocol-result.json'), JSON.stringify({ dateUtc: new Date().toISOString(), executable: 'companion.exe', taskViewCapability: capability.payload, results }, null, 2) + '\n');
  console.log(`PASS Windows process-v2 companion: ${results.length} scenarios`);
} finally { for (const s of active) if (s.child.exitCode === null) s.child.kill(); }

