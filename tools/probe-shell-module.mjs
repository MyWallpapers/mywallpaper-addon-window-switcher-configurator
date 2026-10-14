import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { dirname, resolve, win32 } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const path = resolve(process.env.SystemRoot || 'C:/Windows', 'System32/twinui.pcshell.dll');
const bytes = await readFile(path);
if (bytes.length > 64 * 1024 * 1024) throw new Error('Module exceeds bounded probe size');
const pe = bytes.readUInt32LE(0x3c), optional = pe + 24;
if (bytes.toString('ascii', pe, pe + 4) !== 'PE\0\0' || bytes.readUInt16LE(pe + 4) !== 0x8664 || bytes.readUInt16LE(optional) !== 0x20b) throw new Error('Expected PE32+ x64');
const debugRva = bytes.readUInt32LE(optional + 112 + 6 * 8), debugSize = bytes.readUInt32LE(optional + 112 + 6 * 8 + 4);
const sectionCount = bytes.readUInt16LE(pe + 6), sectionStart = optional + bytes.readUInt16LE(pe + 20);
function offset(rva) {
  for (let i = 0; i < sectionCount; i++) {
    const s = sectionStart + i * 40, start = bytes.readUInt32LE(s + 12), size = Math.max(bytes.readUInt32LE(s + 8), bytes.readUInt32LE(s + 16));
    if (rva >= start && rva < start + size) return bytes.readUInt32LE(s + 20) + rva - start;
  }
  throw new Error('Debug RVA not mapped to a PE section');
}
let codeView;
const debug = offset(debugRva);
for (let i = 0; i + 28 <= debugSize; i += 28) {
  const entry = debug + i;
  if (bytes.readUInt32LE(entry + 12) !== 2) continue;
  const data = bytes.readUInt32LE(entry + 24), length = bytes.readUInt32LE(entry + 16);
  if (data + length > bytes.length || length < 25 || bytes.toString('ascii', data, data + 4) !== 'RSDS') continue;
  const guid = bytes.subarray(data + 4, data + 20);
  const canonical = Buffer.from([guid[3], guid[2], guid[1], guid[0], guid[5], guid[4], guid[7], guid[6], ...guid.subarray(8)]).toString('hex');
  const end = bytes.indexOf(0, data + 24);
  codeView = { pdbGuid: canonical, pdbAge: bytes.readUInt32LE(data + 20), pdbName: win32.basename(bytes.toString('utf8', data + 24, Math.min(end < 0 ? data + length : end, data + length))) };
  break;
}
if (!codeView) throw new Error('CodeView RSDS identity absent');
const request = { schemaVersion: 1, module: { imageName: 'twinui.pcshell.dll', architecture: 'windows-x86_64', sha256: createHash('sha256').update(bytes).digest('hex'), peTimestamp: bytes.readUInt32LE(pe + 8), ...codeView },
  symbols: ['public: virtual long __cdecl XamlAltTabViewHost::Show(struct IImmersiveMonitor *,enum ALT_TAB_VIEW_FLAGS,struct IApplicationView *)'], timeoutMs: 1500 };
await mkdir(resolve(root, 'docs/evidence'), { recursive: true });
await writeFile(resolve(root, 'docs/evidence/private-symbol-request.json'), JSON.stringify({ dateUtc: new Date().toISOString(), observedModule: path, resolved: false, request }, null, 2) + '\n');
console.log(`PASS: installed module fingerprint and RSDS identity observed; symbol resolution remains unavailable (${codeView.pdbName})`);

