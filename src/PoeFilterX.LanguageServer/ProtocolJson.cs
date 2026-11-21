using System.Text.Json;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace PoeFilterX.LanguageServer;

internal static class ProtocolJson
{
    public static JsonElement Property(this JsonElement value, string name)
    {
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property)
            ? property : default;
    }

    public static string Text(this JsonElement value, string fallback = "")
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
    }

    public static int Number(this JsonElement value, int fallback = 0)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : fallback;
    }

    public static string DocumentPath(JsonElement parameters)
    {
        return FilePath(parameters.Property("textDocument").Property("uri").Text());
    }

    public static string FilePath(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return Path.GetFullPath(uri.LocalPath);
        }

        throw InvalidParameters("Expected an absolute file URI.");
    }

    public static string FileUri(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    public static LocalRpcException InvalidParameters(string message)
    {
        return new LocalRpcException(message) { ErrorCode = (int)JsonRpcErrorCode.InvalidParams };
    }

    public static JsonObject Position(SourcePosition position)
    {
        return new JsonObject { ["line"] = position.Line, ["character"] = position.Character };
    }

    public static JsonObject Range(SourceText source, SourceSpan span)
    {
        var start = Math.Clamp(span.Start, 0, source.Text.Length);
        var end = Math.Clamp(span.End, start, source.Text.Length);

        return new JsonObject
        {
            ["start"] = Position(source.GetPosition(start)),
            ["end"] = Position(source.GetPosition(end))
        };
    }

    public static int Offset(SourceText source, JsonElement position)
    {
        var line = position.Property("line").Number(-1);
        var character = position.Property("character").Number(-1);
        if (line < 0 || character < 0 || line > source.GetPosition(source.Text.Length).Line)
        {
            throw InvalidParameters("Position is outside the document.");
        }

        var span = source.GetLineSpan(line);
        return span.Start + Math.Min(character, span.Length);
    }

    public static JsonObject Location(SourceText source, SourceSpan span)
    {
        return new JsonObject { ["uri"] = FileUri(source.Path), ["range"] = Range(source, span) };
    }

    public static JsonObject Edit(
        SourceText source,
        SourceSpan span,
        string text
    )
    {
        return new JsonObject { ["range"] = Range(source, span), ["newText"] = text };
    }
}
