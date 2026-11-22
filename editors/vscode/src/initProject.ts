import * as vscode from 'vscode';

interface InitOptions {
    readonly name?: string;
    readonly path?: string;
}

export async function initializeProject(
    executable: string,
    options: InitOptions = {}
): Promise<vscode.TaskExecution | undefined> {
    const name = options.name ?? await vscode.window.showInputBox({
        title: 'Initialize PoeFilterX project',
        prompt: 'Project name',
        ignoreFocusOut: true,
        validateInput: value => value.trim() ? undefined : 'Enter a project name.'
    });
    if (name === undefined) {
        return undefined;
    }

    const active = vscode.window.activeTextEditor?.document.uri;
    const folder = active ? vscode.workspace.getWorkspaceFolder(active) : undefined;
    const parent = options.path ? vscode.Uri.file(options.path) : (await vscode.window.showOpenDialog({
        title: 'Choose the parent directory for the new project',
        defaultUri: folder?.uri ?? vscode.workspace.workspaceFolders?.[0]?.uri,
        canSelectFiles: false,
        canSelectFolders: true,
        canSelectMany: false,
        openLabel: 'Create project here'
    }))?.[0];
    if (!parent) {
        return undefined;
    }

    const task = new vscode.Task(
        { type: 'process' },
        vscode.workspace.getWorkspaceFolder(parent) ?? vscode.TaskScope.Global,
        `init ${name}`,
        'PoeFilterX',
        new vscode.ProcessExecution(executable, ['init', '--name', name, '--path', parent.fsPath], {
            cwd: parent.fsPath
        }),
        []
    );
    task.presentationOptions = { reveal: vscode.TaskRevealKind.Always, clear: true };
    return vscode.tasks.executeTask(task);
}
