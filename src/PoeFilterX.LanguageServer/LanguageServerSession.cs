using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;
using StreamJsonRpc;

namespace PoeFilterX.LanguageServer;

internal sealed class LanguageServerSession(JsonRpc rpc) : IDisposable
{
    private enum Lifecycle { Created, Initializing, Ready, Shutdown }

    private readonly EditorWorkspace workspace = new();
    private readonly CodeActionFeatures codeActions = new([]);
    private readonly HashSet<string> published = [with(EditorProjectSource.PathComparer)];
    private readonly HashSet<string> discoveryWarnings = [with(StringComparer.Ordinal)];
    private readonly TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource lifetime = new();
    private static readonly IReadOnlyDictionary<string, Type> DiagnosticTypes = new Dictionary<string, Type>
    {
        ["uri"] = typeof(string),
        ["diagnostics"] = typeof(JsonArray),
        ["version"] = typeof(int?)
    };
    private long diagnosticRevision;
    private Lifecycle state;
    private Process? parent;
    private bool watchFiles;

    public Task<int> Exit => exit.Task;

    public void Register()
    {
        Add("initialize", (Func<JsonElement, JsonNode>)Initialize);
        Add("initialized", (Func<JsonElement, Task>)InitializedAsync);
        Add("shutdown", (Func<JsonNode?>)Shutdown);
        Add("exit", (Action)ExitServer);
        Add("$/setTrace", (Action<JsonElement>)(_ => { }));
        Add("textDocument/didOpen", (Func<JsonElement, Task>)OpenAsync);
        Add("textDocument/didChange", (Func<JsonElement, Task>)ChangeAsync);
        Add("textDocument/didClose", (Func<JsonElement, Task>)CloseAsync);
        Add("textDocument/didSave", (Func<JsonElement, Task>)SaveAsync);
        Add("workspace/didChangeWatchedFiles", (Func<JsonElement, Task>)WatchedFilesAsync);
        Add("workspace/didChangeConfiguration", (Func<JsonElement, Task>)ConfigurationAsync);
        Add("workspace/didChangeWorkspaceFolders", (Func<JsonElement, Task>)FoldersAsync);
        Request("textDocument/completion", At(CompletionFeatures.Complete));
        Request("textDocument/hover", At(NavigationFeatures.Hover));
        Request("textDocument/signatureHelp", At(CompletionFeatures.Signature));
        Request("textDocument/definition", At(NavigationFeatures.Definitions));
        Request("textDocument/references", References);
        Request("textDocument/prepareRename", At(NavigationFeatures.PrepareRename));
        Request("textDocument/rename", Rename);
        Request("textDocument/documentHighlight", At(NavigationFeatures.DocumentHighlights));
        Request("textDocument/documentSymbol",
            (parameters, token) => OutlineFeatures.Symbols(Context(parameters, token).Tree)
        );
        Request("textDocument/foldingRange",
            (parameters, token) => OutlineFeatures.Folding(Context(parameters, token).Tree)
        );
        Request("textDocument/documentLink",
            (parameters, token) => NavigationFeatures.Links(Context(parameters, token))
        );
        Request("textDocument/semanticTokens/full",
            (parameters, token) => SemanticFeatures.Tokens(Context(parameters, token).Tree)
        );
        Request("textDocument/semanticTokens/range", SemanticRange);
        Request("textDocument/formatting", Format);
        Request("textDocument/selectionRange", Selections);
        Request("textDocument/documentColor",
            (parameters, token) => ColorFeatures.Colors(Context(parameters, token).Tree)
        );
        Request("textDocument/colorPresentation",
            (parameters, token) => ColorFeatures.Presentations(Context(parameters, token).Tree, parameters)
        );
        Request("textDocument/codeAction", Actions);
        Request("workspace/symbol", WorkspaceSymbols);
        Request("poefilterx/projectContext", ProjectContext);
    }

    private void Add(string name, Delegate handler)
    {
        rpc.AddLocalRpcMethod(handler.Method, handler.Target,
            new JsonRpcMethodAttribute(name) { UseSingleObjectParameterDeserialization = true });
    }

    private void Request(string name, Func<JsonElement, CancellationToken, JsonNode?> handler)
    {
        // Synchronous handlers preserve the dispatcher's document-change ordering.
        Func<JsonElement, CancellationToken, JsonNode?> guarded = (parameters, cancellationToken) =>
        {
            EnsureReady();
            cancellationToken.ThrowIfCancellationRequested();
            return handler(parameters, cancellationToken);
        };
        Add(name, guarded);
    }

    private void EnsureReady()
    {
        if (state != Lifecycle.Ready)
        {
            throw new LocalRpcException(state == Lifecycle.Created ? "Server is not initialized." : "Server is not running.")
            {
                ErrorCode = state == Lifecycle.Created ? -32002 : -32600
            };
        }
    }

    private JsonNode Initialize(JsonElement parameters)
    {
        if (state != Lifecycle.Created)
        {
            throw ProtocolJson.InvalidParameters("The server has already been initialized.");
        }

        workspace.Configure(parameters);
        watchFiles = parameters.Property("capabilities").Property("workspace")
            .Property("didChangeWatchedFiles").Property("dynamicRegistration").ValueKind == JsonValueKind.True;
        state = Lifecycle.Initializing;
        WatchParent(parameters.Property("processId").Number());

        return new JsonObject
        {
            ["serverInfo"] = new JsonObject { ["name"] = "PoeFilterX", ["version"] = "1.0.0" },
            ["capabilities"] = new JsonObject
            {
                ["positionEncoding"] = "utf-16",
                ["textDocumentSync"] = new JsonObject { ["openClose"] = true, ["change"] = 2, ["save"] = true },
                ["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray(" ", "%", "/", "\"") },
                ["hoverProvider"] = true,
                ["signatureHelpProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray(" ", ":") },
                ["definitionProvider"] = true,
                ["referencesProvider"] = true,
                ["renameProvider"] = new JsonObject { ["prepareProvider"] = true },
                ["documentHighlightProvider"] = true,
                ["documentSymbolProvider"] = true,
                ["workspaceSymbolProvider"] = true,
                ["foldingRangeProvider"] = true,
                ["selectionRangeProvider"] = true,
                ["documentLinkProvider"] = new JsonObject { ["resolveProvider"] = false },
                ["documentFormattingProvider"] = true,
                ["colorProvider"] = true,
                ["codeActionProvider"] = true,
                ["semanticTokensProvider"] = new JsonObject
                {
                    ["legend"] = new JsonObject { ["tokenTypes"] = SemanticFeatures.TokenTypes(), ["tokenModifiers"] = new JsonArray() },
                    ["full"] = true,
                    ["range"] = true
                },
                ["workspace"] = new JsonObject
                {
                    ["workspaceFolders"] = new JsonObject { ["supported"] = true, ["changeNotifications"] = true }
                }
            }
        };
    }

    private void WatchParent(int processId)
    {
        if (processId <= 0 || processId == Environment.ProcessId)
        {
            return;
        }

        try
        {
            parent = Process.GetProcessById(processId);
            _ = WaitForParentAsync(parent);
        }
        catch (ArgumentException)
        {
            exit.TrySetResult(0);
        }
    }

    private async Task WaitForParentAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(lifetime.Token).ConfigureAwait(false);
            exit.TrySetResult(0);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The language server connection ended before its parent process.
        }
    }

    private async Task InitializedAsync(JsonElement _)
    {
        if (state != Lifecycle.Initializing)
        {
            throw ProtocolJson.InvalidParameters("Expected initialize before initialized.");
        }

        state = Lifecycle.Ready;
        if (watchFiles)
        {
            await RegisterWatchersAsync().ConfigureAwait(false);
        }
    }

    private async Task RegisterWatchersAsync()
    {
        var watchers = new JsonArray();
        foreach (var glob in new[] { "**/*.filterx", "**/*.fss", "**/*.json" })
        {
            watchers.Add((JsonNode)new JsonObject { ["globPattern"] = glob });
        }

        var registration = new JsonObject
        {
            ["id"] = "poefilterx-files",
            ["method"] = "workspace/didChangeWatchedFiles",
            ["registerOptions"] = new JsonObject { ["watchers"] = watchers }
        };
        var arguments = new Dictionary<string, object?> { ["registrations"] = new JsonArray(registration) };
        var types = new Dictionary<string, Type> { ["registrations"] = typeof(JsonArray) };
        try
        {
            await rpc.InvokeWithParameterObjectAsync<JsonElement>(
                "client/registerCapability", arguments, types, lifetime.Token
            ).ConfigureAwait(false);
        }
        catch (RemoteInvocationException)
        {
            // Disk stamps still invalidate cached projects if the client declines file watching.
        }
    }

    private JsonNode? Shutdown()
    {
        EnsureReady();
        state = Lifecycle.Shutdown;
        return null;
    }

    private void ExitServer() => exit.TrySetResult(state == Lifecycle.Shutdown ? 0 : 1);

    private Task OpenAsync(JsonElement parameters)
    {
        EnsureReady();
        workspace.Open(parameters.Property("textDocument"));
        return RefreshAsync();
    }

    private Task ChangeAsync(JsonElement parameters)
    {
        EnsureReady();
        workspace.Change(parameters);
        return RefreshAsync();
    }

    private Task CloseAsync(JsonElement parameters)
    {
        EnsureReady();
        workspace.Close(ProtocolJson.DocumentPath(parameters));
        return RefreshAsync();
    }

    private Task SaveAsync(JsonElement parameters)
    {
        EnsureReady();
        workspace.Invalidate(ProtocolJson.DocumentPath(parameters));
        return RefreshAsync();
    }

    private Task ConfigurationAsync(JsonElement parameters)
    {
        EnsureReady();
        var settings = parameters.Property("settings");
        settings = settings.Property("poefilterx").ValueKind == JsonValueKind.Object
            ? settings.Property("poefilterx") : settings;
        if (settings.Property("entrypoints").ValueKind != JsonValueKind.Undefined)
        {
            workspace.ConfigureEntrypoints(settings);
        }
        return RefreshAsync();
    }

    private Task FoldersAsync(JsonElement parameters)
    {
        EnsureReady();
        workspace.ChangeFolders(parameters.Property("event"));
        return RefreshAsync();
    }

    private Task WatchedFilesAsync(JsonElement parameters)
    {
        EnsureReady();
        foreach (var change in parameters.Property("changes").EnumerateArray())
        {
            workspace.Invalidate(ProtocolJson.FilePath(change.Property("uri").Text()));
        }

        return RefreshAsync();
    }

    private EditorContext Context(JsonElement parameters, CancellationToken cancellationToken)
    {
        return workspace.Context(ProtocolJson.DocumentPath(parameters), cancellationToken);
    }

    private JsonNode ProjectContext(JsonElement parameters, CancellationToken cancellationToken)
    {
        var context = Context(parameters, cancellationToken);

        return new JsonObject
        {
            ["entrypoint"] = context.HasEntrypoint ? ProtocolJson.FileUri(context.Project.EntryPoint) : null,
            ["warning"] = context.DiscoveryWarning
        };
    }

    private Func<JsonElement, CancellationToken, JsonNode?> At(Func<EditorContext, int, JsonNode?> handler)
    {
        return (parameters, cancellationToken) =>
        {
            var context = Context(parameters, cancellationToken);
            return handler(
                context,
                ProtocolJson.Offset(context.Tree.Source, parameters.Property("position"))
            );
        };
    }

    private JsonNode References(JsonElement parameters, CancellationToken cancellationToken)
    {
        var context = Context(parameters, cancellationToken);
        var offset = ProtocolJson.Offset(context.Tree.Source, parameters.Property("position"));

        return NavigationFeatures.References(context, offset,
            parameters.Property("context").Property("includeDeclaration").ValueKind == JsonValueKind.True);
    }

    private JsonNode? Rename(JsonElement parameters, CancellationToken cancellationToken)
    {
        var context = Context(parameters, cancellationToken);
        var offset = ProtocolJson.Offset(context.Tree.Source, parameters.Property("position"));

        return NavigationFeatures.Rename(context, offset, parameters.Property("newName").Text());
    }

    private JsonNode Format(JsonElement parameters, CancellationToken token)
    {
        var options = parameters.Property("options");
        return FormattingFeatures.Format(Context(parameters, token).Tree, options.Property("tabSize").Number(4),
            options.Property("insertSpaces").ValueKind != JsonValueKind.False);
    }

    private JsonNode SemanticRange(JsonElement parameters, CancellationToken token)
    {
        var context = Context(parameters, token);
        return SemanticFeatures.Tokens(
            context.Tree,
            RequestedRange(context.Tree.Source, parameters.Property("range"))
        );
    }

    private JsonNode Selections(JsonElement parameters, CancellationToken token)
    {
        var tree = Context(parameters, token).Tree;
        var result = new JsonArray();
        foreach (var position in parameters.Property("positions").EnumerateArray())
        {
            result.Add((JsonNode)OutlineFeatures.Selection(tree, ProtocolJson.Offset(tree.Source, position)));
        }

        return result;
    }

    private JsonNode Actions(JsonElement parameters, CancellationToken token)
    {
        var context = Context(parameters, token);
        return codeActions.GetActions(
            context,
            RequestedRange(context.Tree.Source, parameters.Property("range")),
            token
        );
    }

    private static SourceSpan RequestedRange(SourceText source, JsonElement range)
    {
        var start = ProtocolJson.Offset(source, range.Property("start"));
        var end = ProtocolJson.Offset(source, range.Property("end"));
        if (end < start)
        {
            throw ProtocolJson.InvalidParameters("Range ends before it starts.");
        }

        return new(start, end - start);
    }

    private JsonNode WorkspaceSymbols(JsonElement parameters, CancellationToken token)
    {
        var query = parameters.Property("query").Text();
        var result = new JsonArray();
        var seen = new HashSet<(string Path, int Offset)>();
        foreach (var project in workspace.AllProjects(token))
        {
            foreach (var symbol in StyleIndex.Create(project).Where(item => item.Declaration
                && item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                if (seen.Add((symbol.Source.Path, symbol.Span.Start)))
                {
                    result.Add((JsonNode)new JsonObject
                    {
                        ["name"] = symbol.Name,
                        ["kind"] = 5,
                        ["location"] = ProtocolJson.Location(symbol.Source, symbol.Span)
                    });
                }
            }
        }

        return result;
    }

    private static bool IsLanguageFile(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() is ".filterx" or ".fss";
    }

    private async Task RefreshAsync()
    {
        var currentRevision = Interlocked.Increment(ref diagnosticRevision);
        var diagnostics = new Dictionary<string, JsonArray>(EditorProjectSource.PathComparer);
        var entries = new HashSet<string>(EditorProjectSource.PathComparer);
        var warnings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in workspace.Documents.Keys.Where(IsLanguageFile).ToArray())
        {
            var context = workspace.Context(path, lifetime.Token);
            if (context.DiscoveryWarning is { } warning)
            {
                warnings.Add(warning);
            }

            if (entries.Add(context.Project.EntryPoint))
            {
                CollectDiagnostics(context, diagnostics);
            }
        }

        var versions = workspace.Documents.ToDictionary(pair => pair.Key, pair => pair.Value.Version,
            EditorProjectSource.PathComparer);
        var paths = published.Concat(diagnostics.Keys).Distinct(EditorProjectSource.PathComparer).ToArray();
        published.Clear();
        published.UnionWith(diagnostics.Keys);
        var newWarnings = warnings.Except(discoveryWarnings).ToArray();
        discoveryWarnings.Clear();
        discoveryWarnings.UnionWith(warnings);
        foreach (var warning in newWarnings)
        {
            var notification = new Dictionary<string, object?> { ["type"] = 2, ["message"] = warning };
            var types = new Dictionary<string, Type> { ["type"] = typeof(int), ["message"] = typeof(string) };
            await rpc.NotifyWithParameterObjectAsync("window/showMessage", notification, types).ConfigureAwait(false);
        }

        foreach (var path in paths)
        {
            if (currentRevision != Volatile.Read(ref diagnosticRevision))
            {
                return;
            }

            var notification = new Dictionary<string, object?>
            {
                ["uri"] = ProtocolJson.FileUri(path),
                ["diagnostics"] = diagnostics.GetValueOrDefault(path) ?? [],
                ["version"] = versions.TryGetValue(path, out var version) ? version : null
            };
            await rpc.NotifyWithParameterObjectAsync(
                "textDocument/publishDiagnostics", notification, DiagnosticTypes
            ).ConfigureAwait(false);
        }
    }

    private void CollectDiagnostics(EditorContext context, Dictionary<string, JsonArray> result)
    {
        var diagnostics = context.HasEntrypoint ? EditorWorkspace.Diagnostics(context.Project) : context.Tree.Diagnostics;
        foreach (var diagnostic in diagnostics)
        {
            var path = string.IsNullOrEmpty(diagnostic.Path) ? context.Project.EntryPoint : diagnostic.Path;
            if (!result.TryGetValue(path, out var items))
            {
                items = [];
                result.Add(path, items);
            }

            var source = context.Project.SyntaxTrees.TryGetValue(path, out var tree) ? tree.Source : ReadDiagnosticSource(path);
            var related = new JsonArray();
            var pending = new Queue<string>();
            var visited = new HashSet<string>(EditorProjectSource.PathComparer);
            pending.Enqueue(path);
            while (pending.TryDequeue(out var target))
            {
                if (!visited.Add(target))
                {
                    continue;
                }

                AddImportFrames(context, target, related, pending);
            }

            items.Add((JsonNode)new JsonObject
            {
                ["range"] = ProtocolJson.Range(source, diagnostic.Span),
                ["severity"] = 1,
                ["code"] = diagnostic.Code,
                ["source"] = "poefilterx",
                ["message"] = diagnostic.Message,
                ["relatedInformation"] = related
            });
        }
    }

    private static void AddImportFrames(
        EditorContext context,
        string target,
        JsonArray related,
        Queue<string> pending
    )
    {
        foreach (var import in context.Project.Imports.Where(import =>
            EditorProjectSource.PathComparer.Equals(import.TargetPath, target)))
        {
            var importer = context.Project.SyntaxTrees[import.SourcePath].Source;
            related.Add((JsonNode)new JsonObject
            {
                ["location"] = ProtocolJson.Location(importer, import.Syntax.Path.Span),
                ["message"] = "Imported here"
            });
            pending.Enqueue(import.SourcePath);
        }
    }

    private SourceText ReadDiagnosticSource(string path)
    {
        try
        {
            return workspace.ReadText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(path, "");
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        parent?.Dispose();
    }
}
