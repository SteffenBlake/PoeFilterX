import { execFileSync } from 'node:child_process';
import { createVSIX } from '@vscode/vsce';

const target = process.argv[2] ?? `${process.platform}-${process.arch}`;
const revision = process.env.PFX_RELEASE_COMMIT ?? 'main';
execFileSync(process.execPath, ['build-server.mjs', target], { stdio: 'inherit' });
await createVSIX({
    target,
    dependencies: false,
    baseContentUrl: `https://github.com/SteffenBlake/PoeFilterX/blob/${revision}/editors/vscode/`,
    baseImagesUrl: `https://raw.githubusercontent.com/SteffenBlake/PoeFilterX/${revision}/editors/vscode/`,
    preRelease: process.env.PFX_PRERELEASE === 'true',
    packagePath: process.env.PFX_RELEASE_VERSION
        ? `dist/poefilterx-${process.env.PFX_RELEASE_VERSION}-${target}.vsix`
        : `dist/poefilterx-${target}.vsix`
});
