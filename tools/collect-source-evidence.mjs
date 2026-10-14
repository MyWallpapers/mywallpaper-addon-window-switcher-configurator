import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const reference = resolve(root, '..', 'MyWallpaper');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const ledger = await readFile(resolve(reference, 'docs/decisions/decision-ledger.json'));
const catalogue = JSON.parse(await readFile(resolve(reference, 'docs/audits/2026-10-13-windhawk-catalogue/catalogue.json'), 'utf8'));
const examined = {};
for (const id of ['alt-tab-per-monitor', 'alt-tab-hide-ghost-windows', 'alt-tab-delayer', 'alt-tab-flip-3d', 'desktop-window-cards', 'simple-window-switcher', 'classic-window-switcher']) {
  const bytes = await readFile(resolve(root, '.research', id + '.cpp'));
  const text = bytes.toString('utf8');
  examined[id] = { examinedSha256: hash(bytes), catalogueEncodingSha256: hash(Buffer.from(text.replaceAll('\r\n', '\n').replaceAll('\n', '\r\n'))), encoding: 'Raw GitHub UTF-8/LF; catalogue hashed CRLF checkout bytes.', declaredLicense: text.match(/@license\s+([^\r\n]+)/)?.[1] || null, copiedCode: false };
}
const files = ['README.md', 'manifest.json', 'src/config.cpp', 'src/taskbar_configurator.cpp'];
const taskbar = {};
for (const file of files) taskbar[file] = hash(await readFile(resolve(root, '..', 'mywallpaper-addon-taskbar-configurator', file)));
const gitHead = path => execFileSync('wsl.exe', ['-d', 'Ubuntu-26.04', '--', 'git', '-C', path, 'rev-parse', 'HEAD'], { encoding: 'utf8', cwd: 'C:/' }).trim();
const evidence = { dateUtc: new Date().toISOString(), myWallpaperHead: gitHead('/home/rayan/projects/MyWallpaper'), ledgerSha256: hash(ledger),
  decisionReferences: ['DEC-0012', 'DEC-0013', 'DEC-0098', 'DEC-0099', 'DEC-0157', 'DEC-0160'], upstreamCommit: catalogue.upstream_commit,
  family: 'A09', mods: catalogue.mods.filter(m => m.family === 'A09').map(m => ({ id: m.id, source: m.source_url, catalogueSha256: m.source_sha256, settings: m.settings_keys, licenseEvidence: m.license_evidence, ...examined[m.id] })),
  taskbarReference: { head: gitHead('/home/rayan/projects/mywallpaper-addon-taskbar-configurator'), definitions: JSON.parse(await readFile(resolve(root, '..', 'mywallpaper-addon-taskbar-configurator/manifest.json'), 'utf8')).settings.length, files: taskbar },
  engineReference: { head: gitHead('/home/rayan/projects/mywallpaper-native-engine'), sdkSha256: hash(await readFile(resolve(root, '..', 'mywallpaper-native-engine/include/mw_native_sdk.h'))) },
  interpretation: 'Implementation evidence only. No shared repository edits, no launch ratification, no third-party mod code incorporated.' };
for (const mod of evidence.mods) if (mod.examinedSha256 && mod.examinedSha256 !== mod.catalogueSha256 && mod.catalogueEncodingSha256 !== mod.catalogueSha256) throw new Error(`Pinned source changed: ${mod.id}`);
await writeFile(resolve(root, 'docs/source-evidence.json'), JSON.stringify(evidence, null, 2) + '\n');
console.log(`PASS: ${evidence.mods.length} A09 catalogue entries; ${Object.keys(examined).length} pinned implementations studied; ledger ${evidence.ledgerSha256}`);

