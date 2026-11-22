import * as path from 'node:path';
import * as vscode from 'vscode';

export interface BuildDefinition extends vscode.TaskDefinition {
    readonly type: 'poefilterx';
    readonly entrypoint: string;
    readonly output?: string;
}

export class BuildTaskProvider implements vscode.TaskProvider {
    constructor(
        private readonly executable: string,
        private readonly findEntrypoints: (token: vscode.CancellationToken) => Promise<vscode.Uri[]>
    ) {}

    async provideTasks(token: vscode.CancellationToken): Promise<vscode.Task[]> {
        const files = await this.findEntrypoints(token);
        return files.map(uri => this.createTask(uri));
    }

    resolveTask(task: vscode.Task): vscode.Task | undefined {
        if (typeof task.definition.entrypoint !== 'string' || !task.definition.entrypoint) {
            return undefined;
        }

        const folder = typeof task.scope === 'object' ? task.scope : undefined;
        const entry = task.definition.entrypoint;
        if (!folder && !path.isAbsolute(entry) && !entry.startsWith('file:')) {
            return undefined;
        }

        const uri = entry.startsWith('file:') ? vscode.Uri.parse(entry)
            : vscode.Uri.file(path.isAbsolute(entry) ? entry : path.resolve(folder?.uri.fsPath ?? '', entry));
        return this.createTask(uri, task.definition as BuildDefinition, task.name, folder);
    }

    createTask(
        entrypoint: vscode.Uri,
        definition?: BuildDefinition,
        name?: string,
        folder = vscode.workspace.getWorkspaceFolder(entrypoint)
    ): vscode.Task {
        const input = folder ? path.relative(folder.uri.fsPath, entrypoint.fsPath) : entrypoint.fsPath;
        definition ??= { type: 'poefilterx', entrypoint: input };
        // Leave task variables to VS Code; relative paths use the task's working directory.
        const inputArgument = definition.entrypoint.startsWith('file:')
            ? vscode.Uri.parse(definition.entrypoint).fsPath : definition.entrypoint;
        const args = ['build', inputArgument];
        if (definition.output) {
            args.push('--output', definition.output);
        }

        const task = new vscode.Task(
            definition,
            folder ?? vscode.TaskScope.Workspace,
            name ?? `build ${input}`,
            'PoeFilterX',
            new vscode.ProcessExecution(this.executable, args, {
                cwd: folder?.uri.fsPath ?? path.dirname(entrypoint.fsPath)
            }),
            '$poefilterx'
        );
        task.group = vscode.TaskGroup.Build;
        task.presentationOptions = { reveal: vscode.TaskRevealKind.Always, clear: true };
        return task;
    }
}
