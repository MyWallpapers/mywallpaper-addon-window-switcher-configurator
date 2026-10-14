import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const sdk = process.env.MW_SDK_ROOT || resolve(root, '..', 'MyWallpaper', 'packages', 'sdk-public');
const { parseAddonManifest, MAX_ADDON_SETTINGS } = await import(pathToFileURL(resolve(sdk, 'dist/addon-schema/index.js')));
const manifest = parseAddonManifest(JSON.parse(await readFile(resolve(root, 'manifest.json'), 'utf8')));
if (manifest.settings.length > MAX_ADDON_SETTINGS) throw new Error('Settings budget exceeded');
for (const preset of manifest.presets) {
  const profile = JSON.parse(await readFile(resolve(root, 'profiles', preset.id + '.json'), 'utf8'));
  for (const [key, value] of Object.entries(profile)) {
    if (key !== 'layout' && Object.hasOwn(preset.settings, key) && preset.settings[key] !== value) throw new Error(`Preset cannot seed Custom palette: ${preset.id}.${key}`);
  }
}
console.log(`PASS public MyWallpaper SDK: ${manifest.settings.length}/${MAX_ADDON_SETTINGS} definitions`);

