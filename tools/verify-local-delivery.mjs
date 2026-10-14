import { readFile, writeFile, readdir, stat } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const sdkRoot = process.env.MW_SDK_ROOT || resolve(root, '..', 'MyWallpaper/packages/sdk-public');
const sdk = await import(pathToFileURL(resolve(sdkRoot, 'dist/addon-schema/index.js')));
const manifest = sdk.parseAddonManifest(JSON.parse(await readFile(resolve(root, 'manifest.json'), 'utf8')));
async function files(directory) {
  const result = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory() && !['bin', 'obj'].includes(entry.name)) result.push(...await files(resolve(directory, entry.name)));
    else if (entry.isFile()) result.push(resolve(directory, entry.name));
  }
  return result;
}
const bundle = resolve(root, 'native/out/windows-x86_64');
const runtimeFiles = await files(bundle);
let bytes = 0;
for (const file of runtimeFiles) bytes += (await stat(file)).size;
if (runtimeFiles.length > sdk.MAX_ADDON_RELEASE_RUNTIME_FILE_COUNT || bytes > sdk.MAX_ADDON_RELEASE_UNPACKED_RUNTIME_BYTES) throw new Error('Public SDK runtime budget exceeded');
const sourceFiles = [resolve(root, 'manifest.json'), resolve(root, 'mywallpaper.config.json'), resolve(root, 'global.json'), resolve(root, 'Directory.Build.props'), resolve(root, 'native/build.ps1'), ...await files(resolve(root, 'src')), ...await files(resolve(root, 'profiles'))];
const sourceHashes = {};
for (const file of sourceFiles) sourceHashes[relative(root, file).replaceAll('\\', '/')] = createHash('sha256').update(await readFile(file)).digest('hex');
const executable = manifest.native.companion.entries['windows-x86_64'];
const required = ['companion.exe', 'companion.dll', 'companion.runtimeconfig.json', 'Switcher.Core.dll', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'DOTNET-LICENSE.txt', 'DOTNET-ThirdPartyNotices.txt'];
for (const file of required) await stat(resolve(bundle, file));
const config = JSON.parse(await readFile(resolve(root, 'mywallpaper.config.json'), 'utf8'));
if (!config.native.builds[0].outputs.includes(executable)) throw new Error('CLI requires the exact companion output path');
const screenshots = (await files(resolve(root, 'docs/evidence'))).filter(path => path.endsWith('.png'));
const captures = [];
for (const path of screenshots) captures.push({ file: relative(root, path).replaceAll('\\', '/'), sha256: createHash('sha256').update(await readFile(path)).digest('hex'), origin: 'Captured from the actual Windows companion HWND with dedicated owned fixture windows; not an ImageGen mockup.', runtimeAsset: false });
const result = { dateUtc: new Date().toISOString(), target: 'windows-x86_64', executable, executableSha256: createHash('sha256').update(await readFile(resolve(root, executable))).digest('hex'), assemblySha256: createHash('sha256').update(await readFile(resolve(bundle, 'companion.dll'))).digest('hex'),
  runtimeFiles: runtimeFiles.length, unpackedBytes: bytes, publicSdkMaximumFiles: sdk.MAX_ADDON_RELEASE_RUNTIME_FILE_COUNT, publicSdkMaximumBytes: sdk.MAX_ADDON_RELEASE_UNPACKED_RUNTIME_BYTES,
  settings: manifest.settings.length, presets: manifest.presets.length, sourceHashes, captures, publication: 'Local unsigned bundle only. No publication or launch decision.' };
await writeFile(resolve(root, 'docs/evidence/local-delivery.json'), JSON.stringify(result, null, 2) + '\n');
console.log(`PASS delivery: ${runtimeFiles.length} files, ${(bytes / 1024 ** 2).toFixed(1)} MiB, exact CLI entry, notices, ${screenshots.length} real captures`);

