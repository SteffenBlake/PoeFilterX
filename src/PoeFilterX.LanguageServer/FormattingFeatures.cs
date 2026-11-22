using System.Text;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class FormattingFeatures
{
    public static JsonArray Format(
        SyntaxTree tree,
        int tabSize,
        bool insertSpaces
    )
    {
        var edits = new JsonArray();
        if (!tree.Diagnostics.IsEmpty)
        {
            return edits;
        }

        var newline = tree.Source.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var indentation = insertSpaces ? new string(' ', Math.Clamp(tabSize, 1, 16)) : "\t";
        var output = new StringBuilder(tree.Source.Text.Length + 64);
        var depth = 0;
        var lineStart = true;
        var previousEnd = 0;
        string? previousKind = null;
        foreach (var token in tree.Tokens.Where(token => token.Span.Length != 0 && token.Kind != "WHITESPACE"))
        {
            var originalGap = tree.Source.Text.AsSpan(previousEnd, token.Span.Start - previousEnd);
            var trailingComment = token.Kind == "COMMENT" && !originalGap.ContainsAny('\r', '\n');
            if (token.Kind is "CLOSE_BRACE" or "CLOSE_BRACKET")
            {
                depth = Math.Max(0, depth - 1);
                NewLine(output, newline, ref lineStart);
            }

            AppendIndent(output, indentation, depth, lineStart);
            var text = tree.Source.Slice(token.Span);
            if (!lineStart && NeedsSpace(token.Kind, previousKind, trailingComment))
            {
                output.Append(' ');
            }

            output.Append(text);
            lineStart = false;
            if (token.Kind is "OPEN_BRACE" or "OPEN_BRACKET")
            {
                depth++;
            }

            if (token.Kind is "SEMICOLON" or "OPEN_BRACE" or "OPEN_BRACKET"
                or "CLOSE_BRACE" or "CLOSE_BRACKET" or "COMMENT")
            {
                NewLine(output, newline, ref lineStart);
            }

            previousKind = token.Kind;
            previousEnd = token.Span.End;
        }

        NewLine(output, newline, ref lineStart);
        var formatted = output.ToString();
        if (formatted != tree.Source.Text)
        {
            edits.Add((JsonNode)ProtocolJson.Edit(tree.Source, new(0, tree.Source.Text.Length), formatted));
        }

        return edits;
    }

    private static bool NeedsSpace(string kind, string? previous, bool trailingComment)
    {
        return kind is not ("COLON" or "SEMICOLON" or "CLOSE_BRACE" or "CLOSE_BRACKET")
            && (previous is not ("OPEN_BRACE" or "OPEN_BRACKET") || trailingComment);
    }

    private static void AppendIndent(
        StringBuilder output,
        string indent,
        int depth,
        bool lineStart
    )
    {
        if (!lineStart)
        {
            return;
        }

        for (var index = 0; index < depth; index++)
        {
            output.Append(indent);
        }
    }

    private static void NewLine(
        StringBuilder output,
        string newline,
        ref bool lineStart
    )
    {
        if (!lineStart)
        {
            output.Append(newline);
            lineStart = true;
        }
    }
}
