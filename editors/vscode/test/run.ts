import { execFileSync } from 'node:child_process';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import * as os from 'node:os';
import * as path from 'node:path';
import { runTests } from '@vscode/test-electron';

async function main(): Promise<void> {
    const extensionPath = path.resolve(__dirname, '../..');
    execFileSync(process.execPath, [path.join(extensionPath, 'build-server.mjs')], {
        cwd: extensionPath,
        stdio: 'inherit'
    });

    const temporary = await mkdtemp(path.join(os.tmpdir(), 'poefilterx-vscode-'));
    try {
        const workspace = path.join(temporary, 'project');
        await mkdir(path.join(workspace, '.vscode'), { recursive: true });
        await mkdir(path.join(workspace, 'rules'));
        await Promise.all([
            writeFile(path.join(workspace, 'entrypoint.filterx'), 'using primary.fss; using rules/child.filterx;'),
            writeFile(path.join(workspace, 'other.entrypoint.filterx'), 'using secondary.fss; using rules/child.filterx;'),
            writeFile(path.join(workspace, 'primary.fss'), 'primary { font-size: 40; }'),
            writeFile(path.join(workspace, 'secondary.fss'), 'secondary { font-size: 40; }'),
            writeFile(path.join(workspace, 'rules/child.filterx'), 'Style '),
            writeFile(path.join(workspace, '.vscode/settings.json'), JSON.stringify({
                'files.autoSave': 'off',
                'workbench.startupEditor': 'none'
            }))
        ]);
        await runTests({
            extensionDevelopmentPath: extensionPath,
            extensionTestsPath: path.resolve(__dirname, 'suite.js'),
            extensionTestsEnv: {
                ELECTRON_RUN_AS_NODE: undefined,
                PATH: temporary,
                DOTNET_ROOT: temporary,
                DOTNET_ROOT_X64: temporary,
                DOTNET_ROOT_ARM64: temporary
            },
            launchArgs: [
                workspace,
                '--user-data-dir', path.join(temporary, 'user-data'),
                '--extensions-dir', path.join(temporary, 'extensions'),
                '--disable-workspace-trust',
                '--disable-extensions',
                '--skip-welcome',
                '--skip-release-notes',
                '--no-sandbox'
            ]
        });
    } finally {
        await rm(temporary, { recursive: true, force: true });
    }
}

void main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
