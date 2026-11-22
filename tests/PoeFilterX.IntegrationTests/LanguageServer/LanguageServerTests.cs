using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.LanguageServer;

[Parallelizable(ParallelScope.All)]
public partial class LanguageServerTests
{
    private static JsonObject Document(string path) => new() { ["uri"] = new Uri(path).AbsoluteUri };

    private static JsonObject Position(int line, int character) => new() { ["line"] = line, ["character"] = character };

    private static JsonObject At(
        string path,
        int line,
        int character
    )
    {
        return new JsonObject { ["textDocument"] = Document(path), ["position"] = Position(line, character) };
    }

    private static async Task InitializeAsync(
        LspClient client,
        TestWorkspace workspace,
        params string[] entrypoints
    )
    {
        await InitializeAsync(client, workspace, [], entrypoints);
    }

    private static async Task InitializeAsync(
        LspClient client,
        TestWorkspace workspace,
        JsonObject capabilities,
        params string[] entrypoints
    )
    {
        var entries = new JsonArray();
        foreach (var entry in entrypoints)
        {
            entries.Add(entry);
        }

        var response = await client.RequestAsync("initialize", new JsonObject
        {
            ["processId"] = Environment.ProcessId,
            ["capabilities"] = capabilities,
            ["workspaceFolders"] = new JsonArray(new JsonObject
            {
                ["uri"] = new Uri(workspace.Root).AbsoluteUri,
                ["name"] = "test"
            }),
            ["initializationOptions"] = new JsonObject { ["entrypoints"] = entries }
        });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.TryGetProperty("error", out _), Is.False, response.ToString());
            Assert.That(response.GetProperty("result").GetProperty("capabilities").GetProperty("positionEncoding").GetString(),
                Is.EqualTo("utf-16"));
        }
        await client.NotifyAsync("initialized", new JsonObject());
    }

    private static Task OpenAsync(
        LspClient client,
        string path,
        string text
    )
    {
        return client.NotifyAsync("textDocument/didOpen", new JsonObject
        {
            ["textDocument"] = new JsonObject
            {
                ["uri"] = new Uri(path).AbsoluteUri,
                ["languageId"] = Path.GetExtension(path) == ".fss" ? "fss" : "filterx",
                ["version"] = 1,
                ["text"] = text
            }
        });
    }

    private static JsonElement Result(JsonElement response)
    {
        Assert.That(response.TryGetProperty("error", out _), Is.False, response.ToString());
        return response.GetProperty("result");
    }

    [Test]
    public async Task Styles_follow_transitive_imports_unsaved_edits_and_the_selected_entrypoint()
    {
        using var workspace = new TestWorkspace();
        const string main = "using styles/main.fss;\n{ Style accent; }";
        workspace.Write("entrypoint.filterx", main);
        workspace.Write("other.filterx", "using excluded.fss;");
        workspace.Write("excluded.fss", "excluded { show: true; }");
        workspace.Write("styles/main.fss", "using nested.fss; base { show: true; }");
        workspace.Write("styles/nested.fss", "accent { font-size: 40; }");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace, new JsonObject
        {
            ["workspace"] = new JsonObject
            {
                ["didChangeWatchedFiles"] = new JsonObject { ["dynamicRegistration"] = true }
            }
        }, "entrypoint.filterx");
        var registration = await client.NotificationAsync("client/registerCapability");
        Assert.That(registration.GetProperty("registrations")[0].GetProperty("method").GetString(),
            Is.EqualTo("workspace/didChangeWatchedFiles"));
        await OpenAsync(client, workspace.PathFor("entrypoint.filterx"), main);
        var request = At(workspace.PathFor("entrypoint.filterx"), 1, 10);
        var completion = Result(await client.RequestAsync("textDocument/completion", request));
        var labels = completion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString());
        Assert.That(labels, Is.EquivalentTo(["accent", "base"]));

        await OpenAsync(client, workspace.PathFor("styles/nested.fss"), "new-style { font-size: 40; }");
        var updated = Result(await client.RequestAsync("textDocument/completion", At(workspace.PathFor("entrypoint.filterx"), 1, 10)));
        Assert.That(updated.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Is.EquivalentTo(["new-style", "base"]));
        await client.NotifyAsync("textDocument/didClose", new JsonObject { ["textDocument"] = Document(workspace.PathFor("styles/nested.fss")) });
        var restored = Result(await client.RequestAsync("textDocument/completion", At(workspace.PathFor("entrypoint.filterx"), 1, 10)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
                    Does.Contain("accent").And.Not.Contain("excluded"));
            workspace.Write("styles/nested.fss", "external { font-size: 40; }");
            await client.NotifyAsync("workspace/didChangeWatchedFiles", new JsonObject
            {
                ["changes"] = new JsonArray(new JsonObject
                {
                    ["uri"] = new Uri(workspace.PathFor("styles/nested.fss")).AbsoluteUri,
                    ["type"] = 2
                })
            });
            await client.NotifyAsync("textDocument/didChange", new JsonObject
            {
                ["textDocument"] = new JsonObject
                {
                    ["uri"] = new Uri(workspace.PathFor("entrypoint.filterx")).AbsoluteUri,
                    ["version"] = 2
                },
                ["contentChanges"] = new JsonArray(new JsonObject { ["text"] = "using styles/main.fss;\nStyle " })
            });
            var incomplete = Result(await client.RequestAsync("textDocument/completion", At(workspace.PathFor("entrypoint.filterx"), 1, 6)));
            Assert.That(incomplete.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
                Is.EquivalentTo(["external", "base"]), "Completion must survive incomplete syntax and refresh disk imports.");
            Assert.That(await client.ExitAsync(), Is.Zero);
            Assert.That(File.Exists(workspace.PathFor("entrypoint.filter")), Is.False);
        }
    }

    [Test]
    public async Task Navigation_rename_and_symbols_share_the_compiler_import_graph()
    {
        using var workspace = new TestWorkspace();
        const string main = "using main.fss;\n{ Style accent; }";
        workspace.Write("entrypoint.filterx", main);
        workspace.Write("main.fss", "accent { font-size: 40; }");
        await using var client = LspClient.Start(workspace.Root, "--stdio");
        await InitializeAsync(client, workspace);
        await OpenAsync(client, workspace.PathFor("entrypoint.filterx"), main);
        var definitions = Result(await client.RequestAsync("textDocument/definition", At(workspace.PathFor("entrypoint.filterx"), 1, 10)));
        Assert.That(definitions[0].GetProperty("uri").GetString(), Is.EqualTo(new Uri(workspace.PathFor("main.fss")).AbsoluteUri));
        var referencesRequest = At(workspace.PathFor("entrypoint.filterx"), 1, 10);
        referencesRequest["context"] = new JsonObject { ["includeDeclaration"] = true };
        Assert.That(Result(await client.RequestAsync("textDocument/references", referencesRequest)).GetArrayLength(), Is.EqualTo(2));
        var rename = At(workspace.PathFor("entrypoint.filterx"), 1, 10);
        rename["newName"] = "renamed";
        var changes = Result(await client.RequestAsync("textDocument/rename", rename)).GetProperty("changes");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes.EnumerateObject().Count(), Is.EqualTo(2));
            Assert.That(changes.EnumerateObject().SelectMany(file => file.Value.EnumerateArray())
                .Select(edit => edit.GetProperty("newText").GetString()), Is.All.EqualTo("renamed"));
        }
        var hover = Result(await client.RequestAsync("textDocument/hover", At(workspace.PathFor("entrypoint.filterx"), 1, 10)));
        Assert.That(hover.GetProperty("contents").GetProperty("value").GetString(), Does.Contain("main.fss"));
        var prepared = Result(await client.RequestAsync("textDocument/prepareRename", At(workspace.PathFor("entrypoint.filterx"), 1, 10)));
        Assert.That(prepared.GetProperty("placeholder").GetString(), Is.EqualTo("accent"));
        var invalidRename = At(workspace.PathFor("entrypoint.filterx"), 1, 10);
        invalidRename["newName"] = "two names";
        var rejected = await client.RequestAsync("textDocument/rename", invalidRename);
        Assert.That(rejected.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
        var symbols = Result(await client.RequestAsync("workspace/symbol", new JsonObject { ["query"] = "accent" }));
        Assert.That(symbols.GetArrayLength(), Is.EqualTo(1));
        var links = Result(await client.RequestAsync("textDocument/documentLink", new JsonObject { ["textDocument"] = Document(workspace.PathFor("entrypoint.filterx")) }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(links.GetArrayLength(), Is.EqualTo(1));
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Diagnostics_use_unsaved_incremental_changes_and_clear_when_fixed()
    {
        using var workspace = new TestWorkspace();
        const string text = "using main.fss;\n# 😀 comment\n{ Style accent; ItemLevel bananas; }";
        workspace.Write("entrypoint.filterx", text);
        workspace.Write("main.fss", "accent { font-size: 40; }");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, workspace.PathFor("entrypoint.filterx"), text);
        var diagnostic = await client.NotificationAsync("textDocument/publishDiagnostics");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostic.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("PFX3003"));
            Assert.That(diagnostic.GetProperty("diagnostics")[0].GetProperty("range").GetProperty("start").GetProperty("line").GetInt32(), Is.EqualTo(2));
            Assert.That(diagnostic.GetProperty("version").GetInt32(), Is.EqualTo(1));
        }

        await client.NotifyAsync("textDocument/didChange", new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = new Uri(workspace.PathFor("entrypoint.filterx")).AbsoluteUri, ["version"] = 2 },
            ["contentChanges"] = new JsonArray(new JsonObject
            {
                ["range"] = new JsonObject { ["start"] = Position(2, 26), ["end"] = Position(2, 33) },
                ["text"] = "80"
            })
        });
        var cleared = await client.NotificationAsync("textDocument/publishDiagnostics");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared.GetProperty("diagnostics").GetArrayLength(), Is.Zero, cleared.ToString());
            Assert.That(cleared.GetProperty("version").GetInt32(), Is.EqualTo(2));
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Highlighting_formatting_colors_and_extensible_code_actions_are_available()
    {
        using var workspace = new TestWorkspace();
        const string text = "/* 😀 */accent{font-color:0xFF0000;font-size:40;}";
        workspace.Write("main.fss", text);
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, workspace.PathFor("main.fss"), text);
        JsonObject Doc() => new() { ["textDocument"] = Document(workspace.PathFor("main.fss")) };
        var tokens = Result(await client.RequestAsync("textDocument/semanticTokens/full", Doc()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tokens.GetProperty("data").GetArrayLength(), Is.Positive.And.MultipleOf(5));
            Assert.That(tokens.GetProperty("data")[2].GetInt32(), Is.EqualTo(8), "Token lengths must use UTF-16 code units.");
        }
        var colors = Result(await client.RequestAsync("textDocument/documentColor", Doc()));
        Assert.That(colors[0].GetProperty("color").GetProperty("red").GetDouble(), Is.EqualTo(1));
        var presentation = Doc();
        presentation["color"] = JsonNode.Parse(colors[0].GetProperty("color").GetRawText());
        presentation["range"] = JsonNode.Parse(colors[0].GetProperty("range").GetRawText());
        var presentations = Result(await client.RequestAsync("textDocument/colorPresentation", presentation));
        Assert.That(presentations[0].GetProperty("label").GetString(), Is.EqualTo("red"));
        var signature = Result(await client.RequestAsync("textDocument/signatureHelp",
            At(workspace.PathFor("main.fss"), 0, text.IndexOf("40", StringComparison.Ordinal))));
        Assert.That(signature.GetProperty("signatures")[0].GetProperty("label").GetString(),
            Does.Contain("font-size").And.Contain("1").And.Contain("45"));
        var format = Doc();
        format["options"] = new JsonObject { ["tabSize"] = 2, ["insertSpaces"] = true };
        var edits = Result(await client.RequestAsync("textDocument/formatting", format));
        var formatted = edits[0].GetProperty("newText").GetString()!;
        Assert.That(formatted, Does.Contain("/* 😀 */").And.Contain("\n  font-color: 0xFF0000;\n"));
        await client.NotifyAsync("textDocument/didChange", new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = new Uri(workspace.PathFor("main.fss")).AbsoluteUri, ["version"] = 2 },
            ["contentChanges"] = new JsonArray(new JsonObject { ["text"] = formatted })
        });
        var repeated = Doc();
        repeated["options"] = new JsonObject { ["tabSize"] = 2, ["insertSpaces"] = true };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Result(await client.RequestAsync("textDocument/formatting", repeated)).GetArrayLength(), Is.Zero);
            Assert.That(Result(await client.RequestAsync("textDocument/foldingRange", Doc())).GetArrayLength(), Is.Positive);
            Assert.That(Result(await client.RequestAsync("textDocument/documentSymbol", Doc())).GetArrayLength(), Is.EqualTo(1));
        }
        var actions = Doc();
        actions["range"] = new JsonObject { ["start"] = Position(0, 0), ["end"] = Position(0, 0) };
        actions["context"] = new JsonObject { ["diagnostics"] = new JsonArray() };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Result(await client.RequestAsync("textDocument/codeAction", actions)).GetArrayLength(), Is.Zero);
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Socket_transport_connects_to_the_editor_and_keeps_stdout_empty()
    {
        using var workspace = new TestWorkspace();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var process = LspClient.StartProcess(workspace.Root, "--socket", ((IPEndPoint)listener.LocalEndpoint).Port.ToString());
        using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await using var client = new LspClient(process, connection.GetStream(), connection.GetStream());
        await InitializeAsync(client, workspace);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await client.ExitAsync(), Is.Zero);
            Assert.That(await process.StandardOutput.ReadToEndAsync(), Is.Empty);
        }
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task Pipe_transport_uses_the_editors_unix_socket()
    {
        using var workspace = new TestWorkspace();
        var socketPath = workspace.PathFor("lsp.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(1);
        var process = LspClient.StartProcess(workspace.Root, "--pipe", socketPath);
        using var connection = await listener.AcceptAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await using var stream = new NetworkStream(connection, ownsSocket: false);
        await using var client = new LspClient(process, stream, stream);
        await InitializeAsync(client, workspace);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await client.ExitAsync(), Is.Zero);
            Assert.That(await process.StandardOutput.ReadToEndAsync(), Is.Empty);
        }
    }

    [Test]
    public async Task Discovery_uses_the_nearest_entrypoint_and_reports_ambiguity_until_configured()
    {
        using var workspace = new TestWorkspace();
        const string child = "Style ";
        workspace.Write("entrypoint.filterx", "using outer.fss; using project/rules/child.filterx;");
        workspace.Write("outer.fss", "outer { font-size: 40; }");
        workspace.Write("project/game.entrypoint.filterx", "using local.fss; using rules/child.filterx;");
        workspace.Write("project/local.fss", "local { font-size: 40; }");
        workspace.Write("project/rules/child.filterx", child);
        workspace.Write("project/rules/irrelevant.filterx", "UnknownCommand;");
        var path = workspace.PathFor("project/rules/child.filterx");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, path, child);
        var projectContext = new JsonObject { ["textDocument"] = Document(path) };
        var automaticContext = Result(await client.RequestAsync("poefilterx/projectContext", projectContext));
        Assert.That(automaticContext.GetProperty("entrypoint").GetString(),
            Is.EqualTo(new Uri(workspace.PathFor("project/game.entrypoint.filterx")).AbsoluteUri));
        var discovered = Result(await client.RequestAsync("textDocument/completion", At(path, 0, 6)));
        Assert.That(discovered.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Is.EqualTo(["local"]));

        var unrelated = workspace.PathFor("project/rules/unincluded.filterx");
        await OpenAsync(client, unrelated, child);
        var excluded = Result(await client.RequestAsync("textDocument/completion", At(unrelated, 0, 6)));
        Assert.That(excluded.GetProperty("items").GetArrayLength(), Is.Zero);
        workspace.Write("project/entrypoint.filterx", "using outer.fss;");
        await client.NotifyAsync("workspace/didChangeWatchedFiles", new JsonObject
        {
            ["changes"] = new JsonArray(new JsonObject
            {
                ["uri"] = new Uri(workspace.PathFor("project/entrypoint.filterx")).AbsoluteUri,
                ["type"] = 1
            })
        });
        var warning = await client.NotificationAsync("window/showMessage");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(warning.GetProperty("type").GetInt32(), Is.EqualTo(2));
            Assert.That(warning.GetProperty("message").GetString(), Does.Contain("game.entrypoint.filterx")
                .And.Contain("poefilterx.entrypoints"));
        }

        var ambiguous = Result(await client.RequestAsync("textDocument/completion", At(path, 0, 6)));
        Assert.That(ambiguous.GetProperty("items").GetArrayLength(), Is.Zero);
        var ambiguousContext = Result(await client.RequestAsync("poefilterx/projectContext", projectContext.DeepClone()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ambiguousContext.GetProperty("entrypoint").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(ambiguousContext.GetProperty("warning").GetString(), Does.Contain("Multiple"));
        }
        var tokens = Result(await client.RequestAsync("textDocument/semanticTokens/full",
            new JsonObject { ["textDocument"] = Document(path) }));
        Assert.That(tokens.GetProperty("data").GetArrayLength(), Is.Positive);
        await client.NotifyAsync("workspace/didChangeConfiguration", new JsonObject
        {
            ["settings"] = new JsonObject
            {
                ["poefilterx"] = new JsonObject { ["entrypoints"] = new JsonArray("entrypoint.filterx") }
            }
        });
        var selected = Result(await client.RequestAsync("textDocument/completion", At(path, 0, 6)));
        var selectedContext = Result(await client.RequestAsync("poefilterx/projectContext", projectContext.DeepClone()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(selectedContext.GetProperty("entrypoint").GetString(),
                Is.EqualTo(new Uri(workspace.PathFor("entrypoint.filterx")).AbsoluteUri));
            Assert.That(selected.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
                    Is.EqualTo(["outer"]));
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Discovery_includes_the_containing_workspace_root_and_never_searches_above_it(bool multipleRoots)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("entrypoint.filterx", "using outside.fss; using nested/rules/child.filterx;");
        workspace.Write("outside.fss", "outside { font-size: 40; }");
        workspace.Write("nested/rules/child.filterx", "Style ");
        var root = workspace.PathFor("nested");
        var path = workspace.PathFor("nested/rules/child.filterx");
        var folders = new JsonArray(new JsonObject { ["uri"] = new Uri(root).AbsoluteUri, ["name"] = "nested" });
        if (multipleRoots)
        {
            folders.Add((JsonNode)new JsonObject { ["uri"] = new Uri(workspace.Root).AbsoluteUri, ["name"] = "outer" });
        }

        await using var client = LspClient.Start(workspace.Root);
        Result(await client.RequestAsync("initialize", new JsonObject { ["workspaceFolders"] = folders }));
        await client.NotifyAsync("initialized", new JsonObject());
        await OpenAsync(client, path, "Style ");
        var truncated = Result(await client.RequestAsync("textDocument/completion", At(path, 0, 6)));
        Assert.That(truncated.GetProperty("items").GetArrayLength(), Is.Zero,
            "An entrypoint above the nearest workspace root must never be discovered.");

        workspace.Write("nested/entrypoint.filterx", "using inside.fss; using rules/child.filterx;");
        workspace.Write("nested/inside.fss", "inside { font-size: 40; }");
        var included = Result(await client.RequestAsync("textDocument/completion", At(path, 0, 6)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(included.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
                    Is.EqualTo(["inside"]), "The workspace root itself must be searched.");
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Exit_without_shutdown_returns_failure_and_uninitialized_requests_are_rejected()
    {
        using var workspace = new TestWorkspace();
        await using var client = LspClient.Start(workspace.Root);
        var response = await client.RequestAsync("textDocument/completion", At(workspace.PathFor("entrypoint.filterx"), 0, 0));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32002));
            Assert.That(await client.ExitAsync(shutdown: false), Is.EqualTo(1));
        }
    }
}
