using System.Text.Json.Nodes;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.LanguageServer;

public partial class LanguageServerTests
{
    [TestCase("Class Ru|", "Ru", "Rune Daggers", "Item Class poe1")]
    [TestCase("Class poe2|", "poe2", "Rings", "Item Class poe1 poe2")]
    [TestCase("BaseType poe2|", "poe2", "Chaos Orb", "Stackable Currency poe1 poe2")]
    [TestCase("BaseType Belts T3 poe2|", "Belts T3 poe2", "Rawhide Belt", "Belts T3 poe2")]
    [TestCase("Class \"Rune Da|ggers\"", "\"Rune Daggers\"", "Rune Daggers", "Item Class poe1")]
    [TestCase("BaseType == Body Armours Str Dex T1|", "Body Armours Str Dex T1", "Conquest Lamellar", "Body Armours Str Dex T1 poe1")]
    [TestCase("BaseType == \"Conquest La|mellar\"", "\"Conquest Lamellar\"", "Conquest Lamellar", "Body Armours Str Dex T1 poe1")]
    [TestCase("BaseType == \"Conquest La|", "\"Conquest La", "Conquest Lamellar", "Body Armours Str Dex T1 poe1")]
    [TestCase("BaseType == \"Iron Ring\" currency|", "currency", "Chaos Orb", "Stackable Currency poe1 poe2")]
    public async Task Item_completion_exposes_tags_and_replaces_the_value(
        string marked,
        string replaced,
        string label,
        string tags
    )
    {
        using var workspace = new TestWorkspace();
        var offset = marked.IndexOf('|');
        var text = marked.Replace("|", "", StringComparison.Ordinal);
        workspace.Write("entrypoint.filterx", text);
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace, "entrypoint.filterx");
        var result = Result(await client.RequestAsync("textDocument/completion",
            At(workspace.PathFor("entrypoint.filterx"), 0, offset)));
        var item = result.GetProperty("items").EnumerateArray().First(value =>
            value.GetProperty("label").GetString() == label && value.GetProperty("detail").GetString() == tags);
        var edit = item.GetProperty("textEdit");
        var start = edit.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32();
        var end = edit.GetProperty("range").GetProperty("end").GetProperty("character").GetInt32();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.GetProperty("labelDetails").GetProperty("description").GetString(), Is.EqualTo(tags));
            Assert.That(item.GetProperty("filterText").GetString(), Does.Contain(label).And.Contain(tags));
            Assert.That(text[start..end], Is.EqualTo(replaced));
            Assert.That(edit.GetProperty("newText").GetString(), Is.EqualTo("\"" + label + "\""));
        }
    }

    [Test]
    public async Task Unknown_class_and_base_names_are_valid_in_editor_diagnostics()
    {
        using var workspace = new TestWorkspace();
        const string text = "using main.fss; { Class == \"Future Item Class\"; BaseType == \"Future Base\"; Style item; }";
        workspace.Write("entrypoint.filterx", text);
        workspace.Write("main.fss", "item { show: true; }");
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace, "entrypoint.filterx");
        var path = workspace.PathFor("entrypoint.filterx");
        await OpenAsync(client, path, text.Replace("Style item;", "ItemLevel bananas; Style item;", StringComparison.Ordinal));
        var initial = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(initial.GetProperty("diagnostics").GetArrayLength(), Is.GreaterThan(0));
        await client.NotifyAsync("textDocument/didChange", new JsonObject
        {
            ["textDocument"] = new JsonObject { ["uri"] = new Uri(path).AbsoluteUri, ["version"] = 2 },
            ["contentChanges"] = new JsonArray(new JsonObject { ["text"] = text })
        });
        var diagnostic = await client.NotificationAsync("textDocument/publishDiagnostics");
        Assert.That(diagnostic.GetProperty("diagnostics").GetArrayLength(), Is.Zero, diagnostic.ToString());
    }
}
