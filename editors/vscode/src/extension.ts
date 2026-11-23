import * as path from 'node:path';
import * as vscode from 'vscode';
import { LanguageClient, State, TransportKind } from 'vscode-languageclient/node';
import { configuredEntrypoints, findEntrypoints, isProjectConfiguration, selectEntrypoint } from './entrypointPicker';
import { BuildTaskProvider } from './buildTasks';
import { initializeProject } from './initProject';
import { CheckInput, ItemInspector } from './itemInspector';

interface ProjectContext {
    readonly entrypoint: string | null;
    readonly warning: string | null;
}

let extension: PoeFilterXExtension | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    extension = new PoeFilterXExtension(context);
    await extension.restart();
}

export async function deactivate(): Promise<void> {
    await extension?.stop();
    extension = undefined;
}

class PoeFilterXExtension {
    private readonly status = vscode.window.createStatusBarItem('poefilterx.entrypoint', vscode.StatusBarAlignment.Left, 50);
    private readonly output = vscode.window.createOutputChannel('PoeFilterX', { log: true });
    private client: LanguageClient | undefined;
    private transition: Promise<void> = Promise.resolve();
    private statusRevision = 0;
    private stopped = false;
    private readonly serverPath: string;

    constructor(context: vscode.ExtensionContext) {
        this.serverPath = vscode.Uri.joinPath(context.extensionUri, 'server',
            process.platform === 'win32' ? 'PoeFilterX.exe' : 'PoeFilterX').fsPath;
        this.status.name = 'PoeFilterX Project';
        this.status.command = 'poefilterx.selectEntrypoint';
        this.setStatus('Starting…', 'Starting the PoeFilterX language server.');
        this.status.show();
        const watcher = vscode.workspace.createFileSystemWatcher('**/*.{filterx,fss,json}');
        const builds = new BuildTaskProvider(this.serverPath, findEntrypoints);
        const inspector = new ItemInspector(context.extensionUri);
        context.subscriptions.push(
            inspector,
            vscode.commands.registerCommand('poefilterx.checkItem', async (input?: CheckInput) => {
                const entrypoint = await this.resolvedEntrypoint();
                return entrypoint && this.client?.isRunning() ? inspector.show(entrypoint, this.client, input) : undefined;
            }),
            this.status,
            this.output,
            watcher,
            vscode.tasks.registerTaskProvider('poefilterx', builds),
            vscode.commands.registerCommand('poefilterx.init', options => initializeProject(this.serverPath, options)),
            vscode.commands.registerCommand('poefilterx.build', async () => {
                const entrypoint = await this.buildEntrypoint();
                return entrypoint ? vscode.tasks.executeTask(builds.createTask(entrypoint)) : undefined;
            }),
            vscode.commands.registerCommand('poefilterx.restorePackages', async () => {
                const entrypoint = await this.resolvedEntrypoint();
                if (!entrypoint || !this.client?.isRunning()) {
                    return;
                }

                await vscode.workspace.saveAll(false);
                return vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Restoring PoeFilterX packages'
                }, async () => {
                    try {
                        await this.client!.sendRequest('workspace/executeCommand', {
                            command: 'poefilterx.restore', arguments: [entrypoint.toString()]
                        });
                    } catch (error) {
                        void vscode.window.showErrorMessage(`Unable to restore packages: ${String(error)}`);
                    }
                });
            }),
            vscode.commands.registerCommand('poefilterx.selectEntrypoint', selectEntrypoint),
            vscode.commands.registerCommand('poefilterx.restartLanguageServer', () => this.restart()),
            vscode.window.onDidChangeActiveTextEditor(() => { void this.refreshStatus(); }),
            vscode.workspace.onDidChangeConfiguration(event => { void this.configurationChanged(event); }),
            vscode.workspace.onDidChangeWorkspaceFolders(() => { void this.restart(); }),
            watcher.onDidCreate(() => { void this.refreshStatus(); }),
            watcher.onDidChange(() => { void this.refreshStatus(); }),
            watcher.onDidDelete(() => { void this.refreshStatus(); })
        );
    }

    restart(): Promise<void> {
        this.transition = this.transition.then(async () => {
            await this.disposeClient();
            if (this.stopped) {
                return;
            }

            this.setStatus('Starting…', 'Starting the PoeFilterX language server.');
            const settings = vscode.workspace.getConfiguration('poefilterx');
            this.client = new LanguageClient('poefilterx', 'PoeFilterX', {
                command: this.serverPath,
                args: ['language-server', '--stdio'],
                transport: TransportKind.stdio
            }, {
                documentSelector: [{ scheme: 'file', language: 'filterx' }, { scheme: 'file', language: 'fss' }],
                initializationOptions: { entrypoints: settings.get<string[]>('entrypoints', []) },
                outputChannel: this.output
            });
            const client = this.client;
            client.onDidChangeState(event => {
                if (client !== this.client || this.stopped) {
                    return;
                }

                if (event.newState === State.Stopped) {
                    this.setStatus('Server stopped', 'Run PoeFilterX: Restart Language Server to reconnect.', true);
                } else if (event.newState === State.Running) {
                    void this.refreshStatus();
                }
            });
            await this.client.start();
        }).catch(error => {
            this.setStatus('Server unavailable', 'Check the PoeFilterX output for details.', true);
            this.output.error(String(error));
            void vscode.window.showErrorMessage(
                'Unable to start the bundled PoeFilterX language server. See the output for details.',
                'Show Output'
            ).then(action => {
                if (action) {
                    this.output.show();
                }
            });
        });

        return this.transition.then(() => this.refreshStatus());
    }

    async stop(): Promise<void> {
        this.stopped = true;
        this.statusRevision++;
        await this.transition;
        await this.disposeClient();
    }

    private async buildEntrypoint(): Promise<vscode.Uri | undefined> {
        await this.transition;
        const configured = configuredEntrypoints();
        if (configured.length === 1) {
            return configured[0];
        }
        const document = vscode.window.activeTextEditor?.document;
        if (this.client?.isRunning() && document?.uri.scheme === 'file'
            && ['filterx', 'fss'].includes(document.languageId)) {
            const project = await this.client.sendRequest<ProjectContext>('poefilterx/projectContext', {
                textDocument: { uri: document.uri.toString() }
            });
            if (project.entrypoint) {
                return vscode.Uri.parse(project.entrypoint);
            }
        }

        const candidates = await findEntrypoints();
        if (candidates.length === 1) {
            return candidates[0];
        }

        await selectEntrypoint();
        const selected = configuredEntrypoints();
        return selected.length === 1 ? selected[0] : undefined;
    }

    private async resolvedEntrypoint(): Promise<vscode.Uri | undefined> {
        const selected = await this.buildEntrypoint();
        if (!selected || !isProjectConfiguration(selected) || !this.client?.isRunning()) {
            return selected;
        }
        const project = await this.client.sendRequest<ProjectContext>('poefilterx/projectContext', {
            textDocument: { uri: selected.toString() }
        });
        return project.entrypoint ? vscode.Uri.parse(project.entrypoint) : undefined;
    }

    private async disposeClient(): Promise<void> {
        const client = this.client;
        this.client = undefined;
        if (!client) {
            return;
        }

        try {
            await client.dispose();
        } catch (error) {
            // The language client rejects shutdown after a failed start; a restart must still be possible.
            this.output.warn(`Unable to shut down the previous language client: ${String(error)}`);
        }
    }

    private async configurationChanged(event: vscode.ConfigurationChangeEvent): Promise<void> {
        if (!event.affectsConfiguration('poefilterx.entrypoints')) {
            return;
        }

        await this.transition;
        try {
            if (this.client?.isRunning()) {
                await this.client.sendNotification('workspace/didChangeConfiguration', {
                    settings: { poefilterx: { entrypoints: vscode.workspace.getConfiguration('poefilterx').get('entrypoints', []) } }
                });
            }
            await this.refreshStatus();
        } catch (error) {
            this.output.error(`Unable to update entrypoints: ${String(error)}`);
        }
    }

    private async refreshStatus(): Promise<void> {
        const revision = ++this.statusRevision;
        const client = this.client;
        if (this.stopped || !client?.isRunning()) {
            return;
        }

        const document = vscode.window.activeTextEditor?.document;
        if (!document || document.uri.scheme !== 'file' || !['filterx', 'fss'].includes(document.languageId)) {
            const entries = vscode.workspace.getConfiguration('poefilterx').get<string[]>('entrypoints', []);
            const label = entries.length === 1 ? path.basename(entries[0]) : entries.length > 1 ? 'Multiple selected' : 'Automatic';
            this.setStatus(label, 'Select the PoeFilterX project configuration, profile, or entrypoint.');
            return;
        }

        try {
            const project = await client.sendRequest<ProjectContext>('poefilterx/projectContext', {
                textDocument: { uri: document.uri.toString() }
            });
            if (revision !== this.statusRevision || client !== this.client || this.stopped) {
                return;
            }

            const entrypoint = project.entrypoint ? vscode.Uri.parse(project.entrypoint) : undefined;
            this.setStatus(
                configuredEntrypoints().length === 1 ? path.basename(configuredEntrypoints()[0].fsPath)
                    : entrypoint ? path.basename(entrypoint.fsPath) : 'Select…',
                project.warning ?? (entrypoint
                    ? `${vscode.workspace.asRelativePath(entrypoint, true)}\nClick to select a different project.`
                    : 'No entrypoint includes this file. Click to select a project.'),
                project.warning !== null
            );
        } catch (error) {
            if (client === this.client && !this.stopped) {
                this.output.error(`Unable to resolve the active entrypoint: ${String(error)}`);
            }
        }
    }

    private setStatus(label: string, tooltip: string, warning = false): void {
        this.status.text = `${warning ? '$(warning)' : '$(file-code)'} Project: ${label}`;
        this.status.tooltip = tooltip;
        this.status.accessibilityInformation = { label: `PoeFilterX project: ${label}`, role: 'button' };
    }
}
