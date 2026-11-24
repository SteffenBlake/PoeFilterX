using System.Text.Json;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Checking;

[Parallelizable(ParallelScope.All)]
public class CheckCommandTests
{
    internal const string Item = "Item Class: Stackable Currency\nRarity: Currency\nChaos Orb\n--------\nStack Size: 10/20\n";

    [Test]
    public async Task Prompts_for_copied_item_and_prints_style_sources_and_overrides()
    {
        using var workspace = Workspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["check"], Item);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Does.Contain("Ctrl+C").And.Contain("paste"));
            Assert.That(result.StandardOutput, Does.Contain("Chaos Orb").And.Contain("overridden by accent"));
            Assert.That(result.StandardOutput, Does.Contain("styles.fss(").And.Contain("Hidden — item name struck through."));
            Assert.That(File.Exists(workspace.PathFor("entrypoint.filter")), Is.False);
        }
    }

    [TestCase("Poe1")]
    [TestCase("Poe2")]
    public async Task Named_item_emits_clean_versioned_json_without_a_prompt(string game)
    {
        using var workspace = Workspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["check", "--item", Item, "--game", game, "--format", "json", "--verbosity", "Trace"]);
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("result").GetProperty("item").GetProperty("game").GetString(), Is.EqualTo(game));
            Assert.That(result.StandardError, Does.Not.Contain("paste"));
            Assert.That(json.RootElement.GetProperty("result").GetProperty("areaLevel").GetInt32(), Is.EqualTo(game == "Poe1" ? 83 : 80));
        }
    }

    [Test]
    public async Task Item_file_area_level_and_project_path_are_respected()
    {
        using var workspace = Workspace();
        workspace.Write("copy.txt", Item);
        var result = await CliRunner.RunAsync(workspace.Root,
            ["check", "entrypoint.filterx", "--item-file", "copy.txt", "--area-level", "20", "--format", "json"]);
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        Assert.That(json.RootElement.GetProperty("result").GetProperty("areaLevel").GetInt32(), Is.EqualTo(20));
    }

    [TestCase("not an item")]
    [TestCase("")]
    public async Task Invalid_item_returns_json_errors_and_a_nonzero_exit_code(string item)
    {
        using var workspace = Workspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["check", "--item", item, "--format", "json"]);
        using var json = JsonDocument.Parse(result.StandardOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("result").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(json.RootElement.GetProperty("diagnostics").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(result.StandardError, Does.Not.Contain("System."));
        }
    }

    [Test]
    public async Task Invalid_fact_is_a_user_error()
    {
        using var workspace = Workspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["check", "--item", Item, "--fact", "Width=abc", "--format", "json"]);
        using var json = JsonDocument.Parse(result.StandardOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString(), Does.Contain("Width"));
        }
    }

    [Test]
    public async Task Oversized_item_files_return_a_structured_error()
    {
        using var workspace = Workspace();
        workspace.Write("huge.txt", new string('x', 1024 * 1024 + 1));
        var result = await CliRunner.RunAsync(workspace.Root, ["check", "--item-file", "huge.txt", "--format", "json"]);
        using var json = JsonDocument.Parse(result.StandardOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString(), Does.Contain("1 MiB"));
        }
    }

    [TestCase("check")]
    [TestCase("build")]
    public async Task Restore_errors_keep_their_source_locations(string command)
    {
        using var workspace = Workspace();
        workspace.Write("entrypoint.filterx", "import missing from \"https://github.com/example/missing-package.git\" at \"0123456789abcdef0123456789abcdef01234567\";\nusing @missing/main.fss;");
        string[] arguments = command == "check" ? ["check", "--item", Item, "--no-restore"] : ["build", "--no-restore"];
        var result = await CliRunner.RunAsync(workspace.Root, arguments, environment: new Dictionary<string, string>
        {
            ["POEFILTERX_PACKAGE_CACHE"] = workspace.PathFor("cache")
        });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("entrypoint.filterx(1,"));
            Assert.That(result.StandardError, Does.Contain("PFX400"));
        }
    }

    [TestCase("Poe1", "Future Currency", "Future Orb")]
    [TestCase("Poe2", "Future Currency", "Future Orb")]
    [TestCase("Poe1", "Boots", "Plate Vest")]
    [TestCase("Poe2", "Future Currency", "Chaos Orb")]
    public async Task Unfamiliar_item_identity_is_accepted_and_matches_rules(
        string game,
        string itemClass,
        string baseType
    )
    {
        using var workspace = Workspace();
        workspace.Write(
            "entrypoint.filterx",
            $"using styles.fss; {{ Class == \"{itemClass}\"; BaseType == \"{baseType}\"; Style base; }}"
        );
        var text = $"Item Class: {itemClass}\nRarity: Currency\n{baseType}\n--------\nStack Size: 1/20\n";
        var result = await CliRunner.RunAsync(
            workspace.Root, ["check", "--item", text, "--game", game, "--format", "json"]
        );
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        var checkedItem = json.RootElement.GetProperty("result");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.RootElement.GetProperty("diagnostics").GetArrayLength(), Is.Zero);
            Assert.That(checkedItem.GetProperty("status").GetString(), Is.EqualTo("Matched"));
            Assert.That(checkedItem.GetProperty("item").GetProperty("baseType").GetString(), Is.EqualTo(baseType));
            Assert.That(checkedItem.GetProperty("item").GetProperty("itemClass").GetString(), Is.EqualTo(itemClass));
        }
    }

    private static TestWorkspace Workspace()
    {
        var workspace = new TestWorkspace();
        workspace.Write("entrypoint.filterx", "using styles.fss; { Class Currency; Style base accent; }");
        workspace.Write("styles.fss", "base { font-color: 10 20 30; font-size: 20; } accent { font-color-red: 200; font-size: 40; show: false; }");

        return workspace;
    }
}
