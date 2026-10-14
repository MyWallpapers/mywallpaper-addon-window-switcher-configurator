import { readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const manifest = JSON.parse(await readFile(resolve(root, 'manifest.json'), 'utf8'));
async function sources(dir) {
  let files = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    if (entry.isDirectory() && !['bin', 'obj'].includes(entry.name)) files.push(...await sources(resolve(dir, entry.name)));
    else if (entry.isFile() && entry.name.endsWith('.cs') && entry.name !== 'Settings.cs') files.push(resolve(dir, entry.name));
  }
  return files;
}
const texts = await Promise.all((await sources(resolve(root, 'src'))).map(async path => [relative(root, path).replaceAll('\\', '/'), await readFile(path, 'utf8')]));
const report = manifest.settings.filter(s => s.type !== 'section').map(setting => ({ id: setting.id, consumers: texts.filter(([, text]) => text.includes(`"${setting.id}"`)).map(([path]) => path) }));
const missing = report.filter(r => !r.consumers.length);
if (missing.length) throw new Error(`No implementation consumer: ${missing.map(r => r.id).join(', ')}`);
await writeFile(resolve(root, 'docs/setting-consumers.json'), JSON.stringify({ note: 'Scoped reference evidence, not a runtime-effect certificate.', definitions: manifest.settings.length, values: report }, null, 2) + '\n');
console.log(`PASS: ${report.length} settings traced to implementation consumers`);

