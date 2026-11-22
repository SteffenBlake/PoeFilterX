import * as path from 'node:path';
import * as vscode from 'vscode';

type EntrypointItem = vscode.QuickPickItem & (
    { readonly action: 'automatic' | 'browse' } | { readonly action: 'select'; readonly uri: vscode.Uri }
);

export async function selectEntrypoint(): Promise<void> {
    const folders = vscode.workspace.workspaceFolders;
    if (!folders?.length) {
        await vscode.window.showInformationMessage('Open a workspace folder to select a PoeFilterX entrypoint.');
        return;
    }

    const settings = vscode.workspace.getConfiguration('poefilterx');
    const selected = settings.get<readonly string[]>('entrypoints', []);
    const cancellation = new vscode.CancellationTokenSource();
    try {
        const candidates = findEntrypoints(cancellation.token).then(files => items(files, selected));
        const choice = await vscode.window.showQuickPick(candidates, {
            title: 'Select PoeFilterX entrypoint',
            placeHolder: 'Choose an entrypoint, or return to automatic discovery',
            matchOnDescription: true
        });
        if (!choice) {
            return;
        }

        if (choice.action === 'automatic') {
            await settings.update('entrypoints', [], vscode.ConfigurationTarget.Workspace);
            return;
        }

        const uri = choice.action === 'select' ? choice.uri : await browse(folders[0].uri);
        if (uri) {
            const entry = folders.length === 1 ? vscode.workspace.asRelativePath(uri, false) : uri.toString();
            await settings.update('entrypoints', [entry], vscode.ConfigurationTarget.Workspace);
        }
    } finally {
        cancellation.cancel();
        cancellation.dispose();
    }
}

export function configuredEntrypoints(): vscode.Uri[] {
    const roots = vscode.workspace.workspaceFolders ?? [];
    const entries = vscode.workspace.getConfiguration('poefilterx').get<string[]>('entrypoints', []);
    return entries.flatMap(entry => {
        if (entry.startsWith('file:')) {
            return [vscode.Uri.parse(entry)];
        }
        if (path.isAbsolute(entry)) {
            return [vscode.Uri.file(entry)];
        }
        return roots.length === 1 ? [vscode.Uri.joinPath(roots[0].uri, entry)] : [];
    });
}

export async function findEntrypoints(token?: vscode.CancellationToken): Promise<vscode.Uri[]> {
    const files = await vscode.workspace.findFiles(
        '**/{entrypoint.filterx,*.entrypoint.filterx}',
        '**/{.git,node_modules,bin,obj}/**',
        undefined,
        token
    );
    const all = [...files, ...configuredEntrypoints()];
    return [...new Map(all.map(uri => [uri.toString(), uri])).values()];
}

function items(files: readonly vscode.Uri[], selected: readonly string[]): EntrypointItem[] {
    const candidates: EntrypointItem[] = [...files]
        .sort((left, right) => left.toString().localeCompare(right.toString()))
        .map(uri => ({
            action: 'select',
            uri,
            label: path.basename(uri.fsPath),
            description: vscode.workspace.asRelativePath(uri, true),
            detail: selected.includes(uri.toString()) || selected.includes(vscode.workspace.asRelativePath(uri, false))
                ? 'Selected entrypoint' : undefined
        }));

    return [
        {
            action: 'automatic',
            label: '$(search) Automatic discovery',
            description: selected.length === 0 ? 'Current mode' : undefined
        },
        ...candidates,
        { action: 'browse', label: '$(folder-opened) Browse for an entrypoint…' }
    ];
}

async function browse(root: vscode.Uri): Promise<vscode.Uri | undefined> {
    const files = await vscode.window.showOpenDialog({
        title: 'Select PoeFilterX entrypoint',
        defaultUri: root,
        filters: { 'PoeFilterX filters': ['filterx'] },
        canSelectMany: false,
        canSelectFolders: false,
        openLabel: 'Select entrypoint'
    });
    return files?.[0];
}
