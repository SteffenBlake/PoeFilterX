using System.Text.Json.Nodes;
using PoeFilterX.IntegrationTests.Checking;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.LanguageServer;

public partial class LanguageServerTests
{
    [Test]
    public async Task Inspector_exposes_typed_inputs_from_filter_operation_metadata()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("entrypoint.filterx", "");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        var response = Result(await client.RequestAsync("poefilterx/checkInputs", new JsonObject()));
        var fields = response.EnumerateArray().ToDictionary(field => field.GetProperty("name").GetString()!);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["Corrupted"].GetProperty("kind").GetString(), Is.EqualTo("boolean"));
            Assert.That(fields["ItemLevel"].GetProperty("kind").GetString(), Is.EqualTo("number"));
            Assert.That(fields["Class"].GetProperty("kind").GetString(), Is.EqualTo("text"));
            Assert.That(fields["Rarity"].GetProperty("choices").GetArrayLength(), Is.EqualTo(4));
            Assert.That(fields["HasInfluence"].GetProperty("choices").GetArrayLength(), Is.EqualTo(7));
            Assert.That(fields.ContainsKey("AreaLevel"), Is.False);
            Assert.That(fields.ContainsKey("Style"), Is.False);
        }
    }

    [Test]
    public async Task Inspector_uses_unsaved_styles_and_only_the_selected_execution_tree()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("entrypoint.filterx", "using main.fss; { Class Currency; Style item; }");
        workspace.Write("main.fss", "item { font-size: 20; }");
        workspace.Write("unused.fss", "item { font-size: 44; }");
        await using var client = LspClient.Start(workspace.Root);
        var entrypoint = workspace.PathFor("entrypoint.filterx");
        var style = workspace.PathFor("main.fss");
        await InitializeAsync(client, workspace);
        await OpenAsync(client, style, "item { font-size: 45; show: false; }");
        var response = Result(await client.RequestAsync("poefilterx/check", new JsonObject
        {
            ["textDocument"] = Document(entrypoint),
            ["item"] = CheckCommandTests.Item,
            ["game"] = "Poe1",
            ["areaLevel"] = 83
        }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.GetProperty("diagnostics").GetArrayLength(), Is.Zero, response.ToString());
            Assert.That(response.GetProperty("result").GetProperty("preview").GetProperty("fontSize").GetInt32(), Is.EqualTo(45));
            Assert.That(response.GetProperty("result").GetProperty("preview").GetProperty("hidden").GetBoolean(), Is.True);
        }
    }

    [TestCase("Poe1")]
    [TestCase("Poe2")]
    public async Task Inspector_accepts_unfamiliar_names_and_identity_overrides(string game)
    {
        using var workspace = new TestWorkspace();
        workspace.Write(
            "entrypoint.filterx",
            "using main.fss; { Class == \"Future Class\"; BaseType == \"Future Base\"; Style item; }"
        );
        workspace.Write("main.fss", "item { font-size: 32; }");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        const string item = "Item Class: Future Class\nRarity: Normal\nFuture Base\n--------\nItem Level: 80\n";
        var parameters = new JsonObject
        {
            ["textDocument"] = Document(workspace.PathFor("entrypoint.filterx")),
            ["item"] = item,
            ["game"] = game,
            ["areaLevel"] = 80
        };
        var response = Result(await client.RequestAsync("poefilterx/check", parameters.DeepClone()));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.GetProperty("diagnostics").GetArrayLength(), Is.Zero, response.ToString());
            Assert.That(response.GetProperty("result").GetProperty("status").GetString(), Is.EqualTo("Matched"));
        }

        parameters["item"] = CheckCommandTests.Item;
        parameters["facts"] = new JsonObject { ["Class"] = "Future Class", ["BaseType"] = "Future Base" };
        response = Result(await client.RequestAsync("poefilterx/check", parameters.DeepClone()));
        Assert.That(response.GetProperty("diagnostics").GetArrayLength(), Is.Zero, response.ToString());
        var result = response.GetProperty("result");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GetProperty("status").GetString(), Is.EqualTo("Matched"));
            Assert.That(result.GetProperty("item").GetProperty("baseType").GetString(), Is.EqualTo("Future Base"));
            Assert.That(result.GetProperty("item").GetProperty("itemClass").GetString(), Is.EqualTo("Future Class"));
        }
    }
}
