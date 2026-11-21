using System.Text.Json.Nodes;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class OutlineFeatures
{
    public static JsonArray Symbols(SyntaxTree tree)
    {
        var result = new JsonArray();
        foreach (var statement in tree.Root.Statements)
        {
            AddSymbol(result, tree.Source, statement);
        }

        return result;
    }

    private static void AddSymbol(
        JsonArray result,
        SourceText source,
        SyntaxNode node
    )
    {
        var children = new JsonArray();
        var name = node switch
        {
            StyleSyntax style => style.Name,
            CommandSyntax command => command.Name,
            ImportSyntax import => "using " + import.Path.Value,
            FilterBlockSyntax { IsAbstract: true } => "Abstract filter block",
            _ => "Filter block"
        };
        var selection = node switch
        {
            StyleSyntax style => style.NameSpan,
            CommandSyntax command => command.NameSpan,
            ImportSyntax import => import.Path.Span,
            _ => new SourceSpan(node.Span.Start, Math.Min(1, node.Span.Length))
        };
        var statements = node switch
        {
            FilterBlockSyntax block => block.Statements,
            StyleSyntax style => [.. style.Declarations],
            _ => []
        };
        foreach (var child in statements)
        {
            AddSymbol(children, source, child);
        }

        result.Add((JsonNode)new JsonObject
        {
            ["name"] = name,
            ["kind"] = node is StyleSyntax ? 5 : 13,
            ["range"] = ProtocolJson.Range(source, node.Span),
            ["selectionRange"] = ProtocolJson.Range(source, selection),
            ["children"] = children
        });
    }

    public static JsonArray Folding(SyntaxTree tree)
    {
        var result = new JsonArray();
        var spans = SyntaxFacts.Blocks(tree.Root).Select(block => block.Span)
            .Concat(tree.Root.Statements.OfType<StyleSyntax>().Select(style => style.Span))
            .Concat(tree.Tokens.Where(token => token.Kind == "COMMENT").Select(token => token.Span));
        foreach (var span in spans)
        {
            var start = tree.Source.GetPosition(span.Start);
            var end = tree.Source.GetPosition(Math.Clamp(span.End, 0, tree.Source.Text.Length));
            if (end.Line > start.Line)
            {
                result.Add((JsonNode)new JsonObject
                {
                    ["startLine"] = start.Line,
                    ["startCharacter"] = start.Character,
                    ["endLine"] = end.Line,
                    ["endCharacter"] = end.Character
                });
            }
        }

        return result;
    }

    public static JsonObject Selection(SyntaxTree tree, int offset)
    {
        var candidates = SyntaxFacts.Commands(tree.Root).Select(command => command.Span)
            .Concat(SyntaxFacts.Blocks(tree.Root).Select(block => block.Span))
            .Concat(tree.Root.Statements.OfType<StyleSyntax>().Select(style => style.Span))
            .Append(tree.Root.Span).Append(SyntaxFacts.Word(tree.Source, offset))
            .Where(span => SyntaxFacts.Contains(span, offset))
            .Distinct().OrderByDescending(span => span.Length);
        JsonObject? parent = null;
        foreach (var span in candidates)
        {
            var selection = new JsonObject { ["range"] = ProtocolJson.Range(tree.Source, span) };
            if (parent is not null)
            {
                selection["parent"] = parent;
            }

            parent = selection;
        }

        return parent ?? new JsonObject { ["range"] = ProtocolJson.Range(tree.Source, new(offset, 0)) };
    }
}
