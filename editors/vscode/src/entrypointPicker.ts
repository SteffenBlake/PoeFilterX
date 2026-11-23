import * as path from 'node:path';
import * as vscode from 'vscode';

type EntrypointItem = vscode.QuickPickItem & (
    { readonly action: 'automatic' | 'browse' } | { readonly action: 'select'; readonly uri: vscode.Uri }
);

export async function selectEntrypoint(): Promise<void> {
    const folders = vscode.workspace.workspaceFolders;
    if (!folders?.length) {
        await vscode.window.showInformationMessage('Open a workspace folder to select a PoeFilterX project.');
        return;
    }

    const settings = vscode.workspace.getConfiguration('poefilterx');
    const selected = settings.get<readonly string[]>('entrypoints', []);
    const cancellation = new vscode.CancellationTokenSource();
    try {
        const discovered = findEntrypoints(cancellation.token);
        const candidates = discovered.then(files => items(files, selected));
        const choice = await vscode.window.showQuickPick(candidates, {
            title: 'Select PoeFilterX project',
            placeHolder: 'Choose a project configuration or profile, or use automatic discovery',
            matchOnDescription: true
        });
        if (!choice) {
            return;
        }

        if (choice.action === 'automatic') {
            await settings.update('entrypoints', [], vscode.ConfigurationTarget.Workspace);
            return;
        }

        const files = await discovered;
        const configuration = files.find(isProjectConfiguration);
        const uri = choice.action === 'select' ? choice.uri : await browse(configuration ?? folders[0].uri, !!configuration);
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
    const exclude = '**/{.git,node_modules,bin,obj}/**';
    const configurations = await vscode.workspace.findFiles('**/{.poefilterx.json,.poefilterx.*.json}', exclude, undefined, token);
    const configured = configuredEntrypoints();
    const projects = configurations.concat(configured).filter(isProjectConfiguration);
    const files = projects.length > 0 ? projects : [
        ...await vscode.workspace.findFiles('**/{entrypoint.filterx,*.entrypoint.filterx}', exclude, undefined, token),
        ...configured
    ];
    return [...new Map(files.map(uri => [uri.toString(), uri])).values()];
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
                ? 'Selected project' : undefined
        }));

    return [
        {
            action: 'automatic',
            label: '$(search) Automatic discovery',
            description: selected.length === 0 ? 'Current mode' : undefined
        },
        ...candidates,
        { action: 'browse', label: '$(folder-opened) Browse for a project…' }
    ];
}

export function isProjectConfiguration(uri: vscode.Uri): boolean {
    return /^\.poefilterx(?:\.[A-Za-z0-9_-]+)?\.json$/.test(path.basename(uri.fsPath));
}

async function browse(root: vscode.Uri, preferConfiguration: boolean): Promise<vscode.Uri | undefined> {
    const files = await vscode.window.showOpenDialog({
        title: 'Select PoeFilterX project',
        defaultUri: root,
        filters: preferConfiguration ? { 'PoeFilterX configuration': ['json'] } : { 'PoeFilterX projects': ['filterx', 'json'] },
        canSelectMany: false,
        canSelectFolders: false,
        openLabel: 'Select project'
    });
    const selected = files?.[0];
    if (selected && path.extname(selected.fsPath) === '.json' && !isProjectConfiguration(selected)) {
        await vscode.window.showErrorMessage('Choose .poefilterx.json or .poefilterx.<profile>.json.');
        return undefined;
    }
    return selected;
}
