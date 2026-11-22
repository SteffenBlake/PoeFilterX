using System.Text.Json.Nodes;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.LanguageServer;

public partial class LanguageServerTests
{
    [TestCase("font-color", "ReBeccAPurple", "ReBeccAPurple", 102, 51, 153, 255)]
    [TestCase("border-color", "grey", "grey", 128, 128, 128, 255)]
    [TestCase("bg-color", "transparent", "transparent", 0, 0, 0, 0)]
    [TestCase("bg-color", "black", "0x80010203", 1, 2, 3, 128)]
    [TestCase("font-color", "black", "red", 255, 0, 0, 255)]
    [TestCase("border-color", "black", "0x010203", 1, 2, 3, 255)]
    public async Task Named_colors_support_decorations_completions_and_picker_edits(
        string operation,
        string name,
        string expected,
        int red,
        int green,
        int blue,
        int alpha
    )
    {
        using var workspace = new TestWorkspace();
        var text = $"accent {{ {operation}: {name}; }}";
        var path = workspace.PathFor("main.fss");
        workspace.Write("main.fss", text);
        await using var client = LspClient.Start(workspace.Root);
        await InitializeAsync(client, workspace);
        await OpenAsync(client, path, text);
        var document = new JsonObject { ["textDocument"] = Document(path) };
        var colors = Result(await client.RequestAsync("textDocument/documentColor", document));
        Assert.That(colors.GetArrayLength(), Is.EqualTo(1));
        var start = colors[0].GetProperty("range").GetProperty("start").GetProperty("character").GetInt32();
        var end = colors[0].GetProperty("range").GetProperty("end").GetProperty("character").GetInt32();
        Assert.That(text[start..end], Is.EqualTo(name));
        var color = colors[0].GetProperty("color");
        if (expected == name)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(color.GetProperty("red").GetDouble(), Is.EqualTo(red / 255d));
                Assert.That(color.GetProperty("green").GetDouble(), Is.EqualTo(green / 255d));
                Assert.That(color.GetProperty("blue").GetDouble(), Is.EqualTo(blue / 255d));
                Assert.That(color.GetProperty("alpha").GetDouble(), Is.EqualTo(alpha / 255d));
            }
        }

        var request = (JsonObject)document.DeepClone();
        request["range"] = JsonNode.Parse(colors[0].GetProperty("range").GetRawText());
        request["color"] = new JsonObject
        {
            ["red"] = red / 255d,
            ["green"] = green / 255d,
            ["blue"] = blue / 255d,
            ["alpha"] = alpha / 255d
        };
        var presentations = Result(await client.RequestAsync("textDocument/colorPresentation", request));
        Assert.That(presentations[0].GetProperty("textEdit").GetProperty("newText").GetString(), Is.EqualTo(expected));
        var completion = Result(await client.RequestAsync("textDocument/completion", At(path, 0, start)));
        var labels = completion.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("label").GetString()).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(labels, Does.Contain("rebeccapurple").And.Contain("transparent").And.Contain("grey"));
            Assert.That(await client.ExitAsync(), Is.Zero);
        }
    }
}
