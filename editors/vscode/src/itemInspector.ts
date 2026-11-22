import { randomBytes } from 'node:crypto';
import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';

export interface CheckInput {
    readonly item: string;
    readonly game: 'Poe1' | 'Poe2';
    readonly areaLevel: number;
    readonly facts?: Record<string, string>;
}

interface SourceLocation {
    readonly path: string;
    readonly line: number;
    readonly column: number;
}

interface CheckResponse {
    readonly schemaVersion: number;
    readonly result: {
        readonly blocks: readonly { readonly location: SourceLocation; readonly trace: readonly SourceLocation[] }[];
        readonly styles: readonly { readonly location: SourceLocation }[];
        readonly frames: readonly { readonly location: SourceLocation }[];
    } | null;
    readonly diagnostics: readonly { readonly message: string; readonly path: string; readonly span: { readonly start: number } }[];
}

export class ItemInspector implements vscode.Disposable {
    private panel: vscode.WebviewPanel | undefined;
    private revision = 0;
    private sources = new Map<string, SourceLocation>();
    private request: vscode.CancellationTokenSource | undefined;
    private response: CheckResponse | undefined;
    private entrypoint: string | undefined;
    private client: LanguageClient | undefined;

    constructor(private readonly extensionUri: vscode.Uri) { }

    async show(entrypoint: vscode.Uri, client: LanguageClient, input?: CheckInput): Promise<CheckResponse | undefined> {
        if (this.panel && this.entrypoint === entrypoint.toString() && this.client === client) {
            this.panel.reveal(vscode.ViewColumn.Beside);
            if (input) {
                await this.panel.webview.postMessage({ type: 'input', input });
                return this.check(this.panel, entrypoint, client, input);
            }
            return undefined;
        }

        this.entrypoint = entrypoint.toString();
        this.client = client;
        this.request?.cancel();
        this.response = undefined;
        this.panel?.dispose();
        const panel = vscode.window.createWebviewPanel('poefilterx.itemInspector', 'PoeFilterX Item Inspector',
            vscode.ViewColumn.Beside, {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [vscode.Uri.joinPath(this.extensionUri, 'media')]
            });
        this.panel = panel;
        panel.webview.html = this.html(panel.webview, entrypoint);
        const events: vscode.Disposable[] = [];
        events.push(panel.onDidDispose(() => {
            events.forEach(event => event.dispose());
            if (this.panel === panel) {
                this.panel = undefined;
                this.request?.cancel();
            }
        }));
        events.push(panel.webview.onDidReceiveMessage(async message => {
            const revision = message.type === 'check' ? this.revision + 1 : this.revision;
            try {
                if (message.type === 'clear') {
                    ++this.revision;
                    this.request?.cancel();
                    this.response = undefined;
                } else if (message.type === 'check') {
                    await this.check(panel, entrypoint, client, message.input);
                } else if (message.type === 'source' && typeof message.key === 'string') {
                    const location = this.sources.get(message.key);
                    if (location) {
                        const document = await vscode.workspace.openTextDocument(vscode.Uri.file(location.path));
                        const position = new vscode.Position(location.line - 1, location.column - 1);
                        await vscode.window.showTextDocument(document, {
                            selection: new vscode.Range(position, position), viewColumn: vscode.ViewColumn.One
                        });
                    }
                } else if (message.type === 'ready') {
                    const fields = await client.sendRequest('poefilterx/checkInputs', {});
                    await panel.webview.postMessage({ type: 'fields', fields });
                    if (input) {
                        await panel.webview.postMessage({ type: 'input', input });
                    }
                    if (this.response) {
                        await panel.webview.postMessage({ type: 'result', response: this.response });
                    }
                }
            } catch (error) {
                if (panel === this.panel && revision === this.revision) {
                    await panel.webview.postMessage({ type: 'error', message: String(error) });
                }
            }
        }));

        return input ? this.check(panel, entrypoint, client, input) : undefined;
    }

    private async check(
        panel: vscode.WebviewPanel,
        entrypoint: vscode.Uri,
        client: LanguageClient,
        input: CheckInput
    ): Promise<CheckResponse> {
        const revision = ++this.revision;
        this.request?.cancel();
        if (typeof input?.item !== 'string' || input.item.length > 1048576
            || !['Poe1', 'Poe2'].includes(input.game) || !Number.isInteger(input.areaLevel)
            || input.areaLevel < 1 || input.areaLevel > 100) {
            throw new Error('Paste an item, select its game, and supply an area level between 1 and 100.');
        }

        this.request?.dispose();
        const cancellation = new vscode.CancellationTokenSource();
        this.request = cancellation;
        const response = await client.sendRequest<CheckResponse>('poefilterx/check', {
            textDocument: { uri: entrypoint.toString() }, ...input
        }, cancellation.token);
        if (response.schemaVersion !== 1) {
            throw new Error('The item inspector response version is unsupported.');
        }

        if (revision === this.revision && panel === this.panel) {
            this.response = response;
            this.sources.clear();
            for (const block of response.result?.blocks ?? []) {
                this.remember(block.location);
                block.trace.forEach(location => this.remember(location));
            }
            response.result?.frames.forEach(frame => this.remember(frame.location));
            response.result?.styles.forEach(style => this.remember(style.location));
            await panel.webview.postMessage({ type: 'result', response });
        }

        return response;
    }

    private remember(location: SourceLocation): void {
        this.sources.set(`${location.path}:${location.line}:${location.column}`, location);
    }

    private html(webview: vscode.Webview, entrypoint: vscode.Uri): string {
        const nonce = randomBytes(16).toString('base64');
        const script = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'media', 'itemInspector.js'));
        const css = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'media', 'itemInspector.css'));
        const icons = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'media', 'minimap-icons'));
        const escaped = vscode.workspace.asRelativePath(entrypoint).replace(/[&<>"']/g, value => `&#${value.charCodeAt(0)};`);

        return `<!DOCTYPE html><html lang="en"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src ${webview.cspSource}; font-src ${webview.cspSource}; style-src ${webview.cspSource} 'nonce-${nonce}'; script-src 'nonce-${nonce}';">
<link rel="stylesheet" href="${css}"><title>Item Inspector</title></head><body>
<h1>Item Inspector</h1><p class="project">${escaped}</p>
<form id="check-form"><span>Game</span>
<fieldset id="game" class="game-switch" aria-label="Game">
<label><input type="radio" name="game" value="Poe1" checked>Path of Exile 1</label>
<label><input type="radio" name="game" value="Poe2">Path of Exile 2</label></fieldset>
<label for="area">Area level</label><input id="area" type="number" min="1" max="100" value="83">
<label for="item">Hover an item in game, press Ctrl+C, then paste it here.</label>
<textarea id="item" rows="12" spellcheck="false" required></textarea>
<details><summary>Item properties</summary><div id="facts" class="facts"></div></details>
<div class="actions"><button id="clear" type="button">Clear Inputs</button><button id="check" type="submit">Check Item</button></div><span id="status" role="status" aria-live="polite"></span></form>
<div id="errors" role="alert"></div><section id="preview-section" hidden>
<div class="ground"><div id="preview-group"><span id="preview"></span>
<img id="preview-icon" data-root="${icons}" alt="Minimap icon" width="32" height="32" hidden></div></div></section>
<section id="results" aria-live="polite"></section>
<script nonce="${nonce}" src="${script}"></script></body></html>`;
    }

    dispose(): void {
        this.panel?.dispose();
        this.request?.dispose();
    }
}
