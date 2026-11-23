using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.LanguageServer;

public partial class LanguageServerTests
{
    [Test]
    public async Task Unrestored_packages_offer_aliases_and_a_restore_action_without_running_Git()
    {
        using var workspace = new TestWorkspace();
        const string text = "import theme from \"https://packages.invalid/theme.git\";\nusing @theme;\n{ Class Currency; Style accent; }";
        var path = workspace.PathFor("entrypoint.filterx");
        workspace.Write("entrypoint.filterx", text);
        var environment = new Dictionary<string, string>
        {
            ["POEFILTERX_PACKAGE_CACHE"] = workspace.PathFor("cache"),
            ["PATH"] = workspace.PathFor("no-git")
        };
        using var process = LspClient.StartProcess(workspace.Root, environment);
        await using var client = new LspClient(process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, path, text);
        var notification = await client.NotificationAsync("textDocument/publishDiagnostics");
        var diagnostics = notification.GetProperty("diagnostics").EnumerateArray().ToArray();
        var restoreWarning = diagnostics.Single(diagnostic => diagnostic.GetProperty("code").GetString() == "PFX4003");
        var completion = Result(await client.RequestAsync("textDocument/completion", At(path, 1, 10)));
        var actions = Result(await client.RequestAsync("textDocument/codeAction", new JsonObject
        {
            ["textDocument"] = Document(path),
            ["range"] = new JsonObject { ["start"] = Position(0, 0), ["end"] = Position(0, 20) },
            ["context"] = new JsonObject { ["diagnostics"] = new JsonArray() }
        }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restoreWarning.GetProperty("severity").GetInt32(), Is.EqualTo(2));
            Assert.That(diagnostics.Any(diagnostic => diagnostic.GetProperty("message").GetString()!.Contains("undeclared", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(completion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()), Does.Contain("@theme/"));
            Assert.That(actions[0].GetProperty("command").GetProperty("command").GetString(), Is.EqualTo("poefilterx.restore"));
            Assert.That(Directory.Exists(workspace.PathFor("cache")), Is.False);
        }

        await client.ExitAsync();
    }

    private static readonly string[] expected = ["accent", "base"];
    private static readonly string[] expectedArray = ["base", "second"];

    [Test]
    public async Task Explicit_restore_and_terminal_update_refresh_package_files_styles_and_diagnostics()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("styles/main.fss", "using nested.fss; base { show: true; }");
        repository.Write("styles/nested.fss", "accent { font-size: 40; }");
        repository.Write("styles/excluded.fss", "excluded { show: true; }");
        await repository.CommitAsync();
        using var workspace = new TestWorkspace();
        var text = $"import theme from \"{repository.Url}\";\nusing @theme/styles/main.fss;\n{{ Class Currency; Style accent; }}";
        var path = workspace.PathFor("entrypoint.filterx");
        workspace.Write("entrypoint.filterx", text);
        var environment = new Dictionary<string, string> { ["POEFILTERX_PACKAGE_CACHE"] = workspace.PathFor("cache") };
        using var process = LspClient.StartProcess(workspace.Root, environment);
        await using var client = new LspClient(process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, path, text);
        var before = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(before.GetProperty("diagnostics")[0].GetProperty("severity").GetInt32(), Is.EqualTo(2));
        Result(await client.RequestAsync("workspace/executeCommand", new JsonObject
        {
            ["command"] = "poefilterx.restore",
            ["arguments"] = new JsonArray(new Uri(path).AbsoluteUri)
        }));
        var after = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(after.GetProperty("diagnostics").GetArrayLength(), Is.Zero, after.ToString());
        var styles = Result(await client.RequestAsync("textDocument/completion", At(path, 2, 25)));
        var files = Result(await client.RequestAsync("textDocument/completion", At(path, 1, 20)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(styles.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()), Is.EquivalentTo(expected));
            Assert.That(files.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()), Does.Contain("@theme/styles/nested.fss"));
        }

        repository.Write("styles/nested.fss", "second { show: true; }");
        await repository.CommitAsync();
        var updated = await CliRunner.RunAsync(workspace.Root, ["restore", "--update"], environment: environment);
        Assert.That(updated.ExitCode, Is.Zero, updated.StandardError);
        // No didChangeWatchedFiles notification: terminal restores must refresh an idle editor too.
        var refreshed = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(refreshed.GetProperty("diagnostics")[0].GetProperty("message").GetString(), Does.Contain("accent"));
        var completion = Result(await client.RequestAsync("textDocument/completion", At(path, 2, 25)));
        Assert.That(completion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()), Is.EquivalentTo(expectedArray));
        await client.ExitAsync();
    }

    [Test]
    public async Task Local_package_overrides_refresh_external_files_and_use_unsaved_checkout_documents()
    {
        using var workspace = new TestWorkspace();
        using var checkout = new TestWorkspace();
        const string repository = "https://github.com/UserName/my-filter-style";
        const string text = "import theme from \"https://github.com/UserName/my-filter-style\";\nusing @theme/main.fss;\n{ Class Currency; Style accent; }";
        var path = workspace.PathFor("custom.filterx");
        workspace.Write("custom.filterx", text);
        workspace.Write(".poefilterx.json", """{"entrypoint":"custom.filterx"}""");
        workspace.Write(".poefilterx.dev.json", JsonSerializer.Serialize(new
        {
            packageOverrides = new Dictionary<string, string> { [repository] = checkout.Root }
        }));
        checkout.Write("main.fss", "accent { font-size: 40; }");
        var environment = new Dictionary<string, string> { ["PATH"] = workspace.PathFor("no-git") };
        using var process = LspClient.StartProcess(workspace.Root, environment, "--profile", "dev");
        await using var client = new LspClient(process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, path, text);
        var initial = Result(await client.RequestAsync("textDocument/completion", At(path, 2, 25)));
        Assert.That(initial.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Does.Contain("accent"));

        // An external save must refresh an idle editor without a client file watcher.
        checkout.Write("main.fss", "replacement { font-size: 44; }");
        var changed = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(changed.GetProperty("diagnostics")[0].GetProperty("message").GetString(), Does.Contain("accent"));

        // The original checkout URI must supply unsaved content to the importing project.
        await OpenAsync(client, checkout.PathFor("main.fss"), "accent { font-size: 42; }");
        var context = Result(await client.RequestAsync("poefilterx/projectContext", new JsonObject
        {
            ["textDocument"] = Document(checkout.PathFor("main.fss"))
        }));
        Assert.That(context.GetProperty("entrypoint").GetString(), Is.EqualTo(new Uri(path).AbsoluteUri));
        var completion = Result(await client.RequestAsync("textDocument/completion", At(path, 2, 25)));
        Assert.That(completion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Does.Contain("accent").And.Not.Contain("replacement"));
        await client.ExitAsync();
    }

    [Test]
    public async Task Selected_json_profile_controls_entrypoint_and_package_override_context()
    {
        using var workspace = new TestWorkspace();
        using var checkout = new TestWorkspace();
        workspace.Write(".poefilterx.json", """{"entrypoint":"base.filterx"}""");
        workspace.Write(".poefilterx.dev.json", JsonSerializer.Serialize(new
        {
            entrypoint = "profile/custom.filterx",
            packageOverrides = new Dictionary<string, string> { ["https://github.com/u/p"] = checkout.Root }
        }));
        workspace.Write("base.filterx", "using base.fss; using child.filterx;");
        workspace.Write("base.fss", "base { show: true; }");
        workspace.Write("profile/custom.filterx", "import theme from \"https://github.com/u/p\"; using @theme/main.fss; using ../child.filterx;");
        workspace.Write("child.filterx", "Style ");
        checkout.Write("main.fss", "local { show: true; }");
        workspace.Write(".poefilterx.other.json", "{}");
        var environment = new Dictionary<string, string> { ["POEFILTERX_PROFILE"] = "other" };
        using var process = LspClient.StartProcess(workspace.Root, environment);
        await using var client = new LspClient(process, process.StandardInput.BaseStream, process.StandardOutput.BaseStream);
        await InitializeAsync(client, workspace, ".poefilterx.dev.json");
        await OpenAsync(client, workspace.PathFor("child.filterx"), "Style ");
        var completion = Result(await client.RequestAsync("textDocument/completion", At(workspace.PathFor("child.filterx"), 0, 6)));
        Assert.That(completion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Does.Contain("local").And.Not.Contain("base"));
        var context = Result(await client.RequestAsync("poefilterx/projectContext", new JsonObject
        {
            ["textDocument"] = Document(workspace.PathFor(".poefilterx.dev.json"))
        }));
        Assert.That(context.GetProperty("entrypoint").GetString(), Is.EqualTo(new Uri(workspace.PathFor("profile/custom.filterx")).AbsoluteUri));
        await OpenAsync(client, checkout.PathFor("main.fss"), "unsaved { show: true; }");
        var changed = Result(await client.RequestAsync("textDocument/completion", At(workspace.PathFor("child.filterx"), 0, 6)));
        Assert.That(changed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("label").GetString()),
            Does.Contain("unsaved").And.Not.Contain("local"));
        await client.ExitAsync();
    }
}
