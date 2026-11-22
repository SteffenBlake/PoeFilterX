using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class ColorFeatures
{
    public static JsonArray Colors(SyntaxTree tree)
    {
        var result = new JsonArray();
        if (!Path.GetExtension(tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        foreach (var command in SyntaxFacts.Commands(tree.Root))
        {
            var parsed = StyleOperationsCodec.Parse(command.Name.ToLowerInvariant(),
                command.Arguments.Select(argument => argument.Value).ToArray());
            if (parsed is not StyleOperations style || command.Arguments.IsEmpty)
            {
                continue;
            }

            var color = command.Name.ToLowerInvariant() switch
            {
                "border-color" => style.BorderColor,
                "font-color" => style.FontColor,
                "bg-color" => style.BackgroundColor,
                _ => null
            };
            if (color is null)
            {
                continue;
            }

            var span = new SourceSpan(command.Arguments[0].Span.Start,
                command.Arguments[^1].Span.End - command.Arguments[0].Span.Start);
            result.Add((JsonNode)new JsonObject
            {
                ["range"] = ProtocolJson.Range(tree.Source, span),
                ["color"] = new JsonObject
                {
                    ["red"] = (color.Red ?? 0) / 255d,
                    ["green"] = (color.Green ?? 0) / 255d,
                    ["blue"] = (color.Blue ?? 0) / 255d,
                    ["alpha"] = (color.Alpha ?? 255) / 255d
                }
            });
        }

        return result;
    }

    internal static readonly string[] sourceArray = ["red", "green", "blue", "alpha"];

    public static JsonArray Presentations(SyntaxTree tree, JsonElement parameters)
    {
        var color = parameters.Property("color");
        var channels = sourceArray.Select(name => Channel(color.Property(name))).ToArray();
        var range = parameters.Property("range");
        var start = ProtocolJson.Offset(tree.Source, range.Property("start"));
        var end = ProtocolJson.Offset(tree.Source, range.Property("end"));
        var value = new ColorValue(channels[0], channels[1], channels[2], channels[3]);
        var span = new SourceSpan(start, Math.Max(0, end - start));
        var original = tree.Source.Slice(span).ToString();
        var label = NamedColors.Values.TryGetValue(original, out var named) && named == value
            ? original
            : NamedColors.GetName(value) ?? (channels[3] == 255
                ? $"0x{channels[0]:X2}{channels[1]:X2}{channels[2]:X2}"
                : $"0x{channels[3]:X2}{channels[0]:X2}{channels[1]:X2}{channels[2]:X2}");

        return [with(new JsonObject
        {
            ["label"] = label,
            ["textEdit"] = ProtocolJson.Edit(tree.Source, span, label)
        })];
    }

    private static int Channel(JsonElement value)
    {
        if (!value.TryGetDouble(out var channel) || !double.IsFinite(channel) || channel < 0 || channel > 1)
        {
            throw ProtocolJson.InvalidParameters("Color channels must be between zero and one.");
        }

        return (int)Math.Round(channel * 255);
    }
}
