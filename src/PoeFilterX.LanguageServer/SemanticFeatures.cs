using System.Globalization;
using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class SemanticFeatures
{
    public static JsonArray TokenTypes()
    {
        return new(
            "keyword", "class", "property", "string", "number", "comment", "operator", "variable", "enumMember"
        );
    }

    public static JsonObject Tokens(SyntaxTree tree, SourceSpan? requested = null)
    {
        var data = new List<int>(tree.Tokens.Length * 5);
        var names = SyntaxFacts.Commands(tree.Root).Select(command => command.NameSpan.Start).ToHashSet();
        var classes = tree.Root.Statements.OfType<StyleSyntax>().Select(style => style.NameSpan.Start).ToHashSet();
        var references = SyntaxFacts.Commands(tree.Root).Where(command => command.Name == "Style")
            .SelectMany(command => command.Arguments).Select(argument => argument.Span.Start).ToHashSet();
        var previousLine = 0;
        var previousColumn = 0;
        foreach (var token in tree.Tokens.Where(token => token.Span.Length != 0 && token.Kind != "WHITESPACE"))
        {
            if (requested is { } range && (token.Span.End <= range.Start || token.Span.Start >= range.End))
            {
                continue;
            }

            var kind = Kind(tree, token, names, classes, references);
            var start = tree.Source.GetPosition(token.Span.Start);
            var end = tree.Source.GetPosition(token.Span.End);
            for (var line = start.Line; line <= end.Line; line++)
            {
                var lineSpan = tree.Source.GetLineSpan(line);
                var column = line == start.Line ? start.Character : 0;
                var length = (line == end.Line ? end.Character : lineSpan.Length) - column;
                if (length <= 0)
                {
                    continue;
                }

                data.Add(line - previousLine);
                data.Add(line == previousLine ? column - previousColumn : column);
                data.Add(length);
                data.Add((int)kind);
                data.Add(0);
                previousLine = line;
                previousColumn = column;
            }
        }

        return new JsonObject
        {
            ["data"] = JsonValue.Create([.. data], ProtocolJsonContext.Default.Int32Array)
        };
    }

    private static SemanticKind Kind(
        SyntaxTree tree,
        SyntaxToken token,
        HashSet<int> names,
        HashSet<int> classes,
        HashSet<int> references
    )
    {
        if (classes.Contains(token.Span.Start) || references.Contains(token.Span.Start))
        {
            return SemanticKind.Class;
        }

        if (names.Contains(token.Span.Start))
        {
            return SemanticKind.Property;
        }

        if (token.Kind == "ATOM")
        {
            var value = tree.Source.Slice(token.Span);
            if (value.Contains('%'))
            {
                return SemanticKind.Variable;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? SemanticKind.Number : SemanticKind.EnumMember;
        }

        return token.Kind switch
        {
            "USING" or "IMPORT" or "FROM" or "AT" => SemanticKind.Keyword,
            "STRING" or "INVALID_STRING" or "UNTERMINATED_STRING" => SemanticKind.String,
            "COMMENT" => SemanticKind.Comment,
            _ => SemanticKind.Operator
        };
    }
}

internal enum SemanticKind { Keyword, Class, Property, String, Number, Comment, Operator, Variable, EnumMember }
