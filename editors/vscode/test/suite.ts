import * as assert from 'node:assert/strict';
import { setTimeout } from 'node:timers/promises';
import * as vscode from 'vscode';

export async function run(): Promise<void> {
    const extension = vscode.extensions.getExtension('SteffenBlake.poefilterx');
    assert.ok(extension, 'The extension must be installed in the development host.');
    await extension.activate();
    const root = vscode.workspace.workspaceFolders?.[0].uri;
    assert.ok(root);
    const uri = vscode.Uri.joinPath(root, 'rules/child.filterx');
    const document = await vscode.workspace.openTextDocument(uri);
    assert.equal(document.languageId, 'filterx');
    await vscode.window.showTextDocument(document);

    await eventually(async () => {
        const tokens = await vscode.commands.executeCommand<vscode.SemanticTokens>(
            'vscode.provideDocumentSemanticTokens', uri
        );
        assert.ok(tokens?.data.length, 'Semantic tokens must come from the real language server.');
    });
    assert.deepEqual(await completions(uri), [], 'Ambiguous projects must not offer styles from an arbitrary entrypoint.');

    // Drive the same native Quick Pick used by the status-bar command.
    await choose(2);
    assert.deepEqual(vscode.workspace.getConfiguration('poefilterx').get('entrypoints'), ['other.entrypoint.filterx']);
    await eventually(async () => {
        assert.deepEqual(await completions(uri), ['secondary']);
    });

    await vscode.commands.executeCommand('poefilterx.restartLanguageServer');
    await eventually(async () => {
        assert.deepEqual(await completions(uri), ['secondary'], 'The saved choice must survive a language-server restart.');
    });

    await choose(0);
    assert.deepEqual(vscode.workspace.getConfiguration('poefilterx').get('entrypoints'), []);
    await eventually(async () => {
        assert.deepEqual(await completions(uri), []);
    });

    await vscode.workspace.fs.delete(vscode.Uri.joinPath(root, 'other.entrypoint.filterx'));
    await eventually(async () => {
        assert.deepEqual(await completions(uri), ['primary'], 'Removing ambiguity must restore automatic discovery.');
    });
    const stylesheet = await vscode.workspace.openTextDocument(vscode.Uri.joinPath(root, 'primary.fss'));
    assert.equal(stylesheet.languageId, 'fss');
    await verifyCompilerCommands(root, document);
    console.log('PoeFilterX extension: native entrypoint picker, persistence, LSP completion, highlighting, and discovery passed.');
}

async function verifyCompilerCommands(root: vscode.Uri, document: vscode.TextDocument): Promise<void> {
    const commands = await vscode.commands.getCommands();
    assert.ok(commands.includes('poefilterx.restorePackages'));
    assert.ok(commands.includes('poefilterx.restore'), 'The LSP restore code action must be connected to executeCommand.');
    await vscode.commands.executeCommand('poefilterx.restorePackages');
    const tasks = await vscode.tasks.fetchTasks({ type: 'poefilterx' });
    const build = tasks.find(task => task.definition.entrypoint === 'entrypoint.filterx');
    assert.ok(build, 'The compiler must contribute a build task without requiring tasks.json.');
    assert.equal(build.group, vscode.TaskGroup.Build);

    await replace(document, '{ Style primary; Identified True; }');
    assert.equal(await taskExit(() => vscode.commands.executeCommand('poefilterx.build')), 0);
    const output = await vscode.workspace.fs.readFile(vscode.Uri.joinPath(root, 'entrypoint.filter'));
    assert.match(Buffer.from(output).toString('utf8'), /SetFontSize 40/);

    await replace(document, '{ Style primary; ItemLevel bananas; }');
    assert.equal(await taskExit(() => vscode.tasks.executeTask(build)), 1);
    await eventually(async () => {
        const errors = vscode.languages.getDiagnostics(document.uri);
        assert.ok(errors.some(error => error.source === 'PoeFilterX build' && error.code === 'PFX3003'),
            'Build diagnostics must appear in Problems with the compiler code and source location.');
    });

    await replace(document, '{ Style primary; Identified True; }');
    await vscode.workspace.fs.writeFile(vscode.Uri.joinPath(root, '.vscode/tasks.json'), Buffer.from(JSON.stringify({
        version: '2.0.0',
        tasks: [{
            label: 'Build filter',
            type: 'poefilterx',
            entrypoint: '${workspaceFolder}/entrypoint.filterx',
            output: '${workspaceFolder}/custom.filter'
        }]
    })));
    let configured: vscode.Task | undefined;
    await eventually(async () => {
        configured = (await vscode.tasks.fetchTasks({ type: 'poefilterx' })).find(task => task.name === 'Build filter');
        assert.ok(configured, 'A configured task must resolve by its label for use as a preLaunchTask.');
    });
    assert.equal(await taskExit(() => vscode.tasks.executeTask(configured!)), 0);
    await vscode.workspace.fs.stat(vscode.Uri.joinPath(root, 'custom.filter'));

    const parent = vscode.Uri.joinPath(root, 'parent folder');
    await vscode.workspace.fs.createDirectory(parent);
    assert.equal(await taskExit(() => vscode.commands.executeCommand('poefilterx.init', {
        name: 'VS Code Demo', path: parent.fsPath
    })), 0);
    const entrypoint = await vscode.workspace.fs.readFile(vscode.Uri.joinPath(parent, 'vs-code-demo/entrypoint.filterx'));
    assert.equal(Buffer.from(entrypoint).toString('utf8'), 'using vs-code-demo.fss;');
    await vscode.workspace.fs.stat(vscode.Uri.joinPath(parent, 'vs-code-demo/vs-code-demo.fss'));
    console.log('PoeFilterX compiler commands: build task, selected entrypoint, Problems diagnostics, and init passed.');
}

async function replace(document: vscode.TextDocument, text: string): Promise<void> {
    const edit = new vscode.WorkspaceEdit();
    edit.replace(document.uri, new vscode.Range(new vscode.Position(0, 0), document.positionAt(document.getText().length)), text);
    assert.ok(await vscode.workspace.applyEdit(edit));
    assert.ok(await document.save());
}

async function taskExit(start: () => Thenable<vscode.TaskExecution | undefined>): Promise<number | undefined> {
    const ended = new Map<vscode.TaskExecution, number | undefined>();
    let execution: vscode.TaskExecution | undefined;
    let finish: (code: number | undefined) => void = () => {};
    const complete = new Promise<number | undefined>(resolve => { finish = resolve; });
    const listener = vscode.tasks.onDidEndTaskProcess(event => {
        ended.set(event.execution, event.exitCode);
        if (event.execution === execution) {
            finish(event.exitCode);
        }
    });
    let timer: NodeJS.Timeout | undefined;
    const timeout = new Promise<never>((_, reject) => {
        timer = globalThis.setTimeout(() => reject(new Error('The compiler task did not finish.')), 15000);
    });
    try {
        execution = await Promise.race([start(), timeout]);
        assert.ok(execution, 'The command must start a compiler task.');
        return ended.has(execution) ? ended.get(execution) : await Promise.race([complete, timeout]);
    } finally {
        globalThis.clearTimeout(timer);
        listener.dispose();
    }
}

async function completions(uri: vscode.Uri): Promise<string[]> {
    const result = await vscode.commands.executeCommand<vscode.CompletionList>(
        'vscode.executeCompletionItemProvider', uri, new vscode.Position(0, 6)
    );
    return result?.items.filter(item => item.kind === vscode.CompletionItemKind.Class)
        .map(item => typeof item.label === 'string' ? item.label : item.label.label).sort() ?? [];
}

async function choose(index: number): Promise<void> {
    const selection = vscode.commands.executeCommand('poefilterx.selectEntrypoint');
    await setTimeout(500);
    for (let item = 0; item < index; item++) {
        await vscode.commands.executeCommand('workbench.action.quickOpenSelectNext');
    }
    await vscode.commands.executeCommand('workbench.action.acceptSelectedQuickOpenItem');
    await selection;
}

async function eventually(assertion: () => Promise<void>): Promise<void> {
    const deadline = Date.now() + 15000;
    while (true) {
        try {
            await assertion();
            return;
        } catch (error) {
            if (Date.now() >= deadline) {
                throw error;
            }
            await setTimeout(100);
        }
    }
}
