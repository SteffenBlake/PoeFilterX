import { build, context } from 'esbuild';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import * as path from 'node:path';

const lock = JSON.parse(await readFile('package-lock.json', 'utf8'));
const notices = await Promise.all(Object.entries(lock.packages)
    .filter(([directory, info]) => directory && !info.dev)
    .map(async ([directory, info]) => {
        const files = await readdir(directory);
        const license = files.find(file => /^licen[cs]e(?:\.(?:txt|md))?$/i.test(file));
        if (!license) {
            throw new Error(`Missing license for ${directory}`);
        }

        const text = await readFile(path.join(directory, license), 'utf8');
        return `${directory.replace(/^node_modules\//, '')} ${info.version}\n\n${text}`;
    }));
await mkdir('dist', { recursive: true });
await writeFile('dist/THIRD-PARTY-NOTICES.txt', notices.join('\n\n--------------------\n\n'));

const options = {
    entryPoints: ['src/extension.ts'],
    bundle: true,
    platform: 'node',
    target: 'node20',
    format: 'cjs',
    external: ['vscode'],
    outfile: 'dist/extension.js',
    sourcemap: true,
    logLevel: 'info'
};
if (process.argv.includes('--watch')) {
    await (await context(options)).watch();
} else {
    await build(options);
}
