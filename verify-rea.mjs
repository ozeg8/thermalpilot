// Invoke the ORIGINAL REA repository readers against the built EXE.
// This verification does not claim that the complete REA CLI/MCP ran.
import { registerHooks } from 'node:module';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';

const [repoArgument, exeArgument, reportArgument] = process.argv.slice(2);
if (!repoArgument || !exeArgument || !reportArgument) throw new Error('Usage: node verify-rea.mjs REA_REPO EXE OUTPUT_JSON');
const repo = resolve(repoArgument);
registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier.endsWith('.js') && specifier.startsWith('.') && context.parentURL?.startsWith(pathToFileURL(repo).href)) {
      const candidate = new URL(specifier.slice(0, -3) + '.ts', context.parentURL);
      if (existsSync(fileURLToPath(candidate))) return nextResolve(candidate.href, context);
    }
    return nextResolve(specifier, context);
  },
});
const { readPeCoffHeader } = await import(pathToFileURL(resolve(repo, 'src/domain/peCoffHeader.ts')).href);
const { readPeSectionHeader } = await import(pathToFileURL(resolve(repo, 'src/domain/peSectionHeader.ts')).href);
const bytes = readFileSync(resolve(exeArgument));
const offset = bytes.readUInt32LE(60);
if (bytes.toString('ascii', offset, offset + 4) !== 'PE\0\0') throw new Error('Invalid PE signature');
const coff = readPeCoffHeader(bytes.subarray(offset + 4, offset + 24));
if (!coff.ok) throw new Error(coff.error.message);
const sections = [];
for (let i = 0; i < coff.value.sectionCount; i++) {
  const start = offset + 24 + coff.value.optionalHeaderSize + i * 40;
  const section = readPeSectionHeader(bytes.subarray(start, start + 40));
  if (!section.ok) throw new Error(section.error.message);
  if (section.value.offset + section.value.raw_size > bytes.length) throw new Error('Out-of-file section');
  sections.push(section.value);
}
if (readPeCoffHeader(Buffer.alloc(19)).ok || readPeSectionHeader(Buffer.alloc(39)).ok) throw new Error('REA truncation refusal failed');
const result = {
  method: 'Original unmodified REA TypeScript source readers invoked through Node type stripping; not full CLI/MCP Evidence',
  repository: 'https://github.com/morluto/rea',
  repository_path: repo,
  parsers: ['src/domain/peCoffHeader.ts', 'src/domain/peSectionHeader.ts'],
  executable: resolve(exeArgument),
  sha256: createHash('sha256').update(bytes).digest('hex'),
  coff: coff.value, sections,
  passed: coff.value.machine === 0x8664 && sections.some(x => x.name === '.text'),
};
writeFileSync(resolve(reportArgument), JSON.stringify(result, null, 2) + '\n');
if (!result.passed) process.exitCode = 1;
console.log(`REA original readers: ${result.passed ? 'PASS' : 'FAIL'}, ${sections.length} sections, SHA-256 ${result.sha256}`);
