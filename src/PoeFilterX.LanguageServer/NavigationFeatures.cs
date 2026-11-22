using System.Text.Json.Nodes;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Packages;

namespace PoeFilterX.LanguageServer;

internal static class NavigationFeatures
{
    public static JsonArray Definitions(EditorContext context, int offset)
    {
        var result = new JsonArray();
        var symbol = StyleIndex.At(context, offset);
        if (symbol is not null)
        {
            foreach (var item in StyleIndex.Create(context.Project).Where(item => item.Declaration && item.Name == symbol.Name))
            {
                result.Add((JsonNode)ProtocolJson.Location(item.Source, item.Span));
            }
        }

        foreach (var import in context.Project.Imports.Where(import =>
            EditorProjectSource.PathComparer.Equals(import.SourcePath, context.Tree.Source.Path)
            && SyntaxFacts.Contains(import.Syntax.Path.Span, offset)))
        {
            result.Add((JsonNode)new JsonObject
            {
                ["uri"] = ProtocolJson.FileUri(import.TargetPath),
                ["range"] = ProtocolJson.Range(new(import.TargetPath, ""), default)
            });
        }

        return result;
    }

    public static JsonArray References(
        EditorContext context,
        int offset,
        bool includeDeclaration
    )
    {
        var result = new JsonArray();
        var symbol = StyleIndex.At(context, offset);
        if (symbol is null)
        {
            return result;
        }

        foreach (var item in StyleIndex.Create(context.Project).Where(item =>
            item.Name == symbol.Name && (includeDeclaration || !item.Declaration)))
        {
            result.Add((JsonNode)ProtocolJson.Location(item.Source, item.Span));
        }

        return result;
    }

    public static JsonNode? Hover(EditorContext context, int offset)
    {
        var symbol = StyleIndex.At(context, offset);
        if (symbol is not null)
        {
            var files = StyleIndex.Create(context.Project).Where(item => item.Declaration && item.Name == symbol.Name)
                .Select(item => Path.GetFileName(item.Source.Path)).Distinct();
            return HoverText(
                $"Style {symbol.Name}\nDeclared in: {string.Join(", ", files)}",
                context.Tree.Source,
                symbol.Span
            );
        }

        var command = SyntaxFacts.Commands(context.Tree.Root).FirstOrDefault(command =>
            SyntaxFacts.Contains(command.NameSpan, offset));
        if (command is null)
        {
            return null;
        }

        var styles = Path.GetExtension(context.Tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase);
        var operations = styles ? StyleOperationsCodec.Operations : FilterOperationsCodec.Operations;
        var comparison = styles ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var operation = operations.FirstOrDefault(item => item.Name.Equals(command.Name, comparison));

        return operation is null ? null : HoverText(
            CompletionFeatures.Describe(operation), context.Tree.Source, command.NameSpan
        );
    }

    private static JsonObject HoverText(
        string text,
        SourceText source,
        SourceSpan span
    )
    {
        return new JsonObject
        {
            ["contents"] = new JsonObject { ["kind"] = "plaintext", ["value"] = text },
            ["range"] = ProtocolJson.Range(source, span)
        };
    }

    public static JsonNode? PrepareRename(EditorContext context, int offset)
    {
        var symbol = StyleIndex.At(context, offset);
        return symbol is null || IsPackageStyle(context, symbol.Name) ? null : new JsonObject
        {
            ["range"] = ProtocolJson.Range(symbol.Source, symbol.Span),
            ["placeholder"] = symbol.Name
        };
    }

    public static JsonNode? Rename(
        EditorContext context,
        int offset,
        string name
    )
    {
        using var reader = new StringReader(name + " {}");
        var parsed = FssParserService.Parse(reader, "rename.fss");
        if (parsed is not SyntaxTree { Root.Statements: [StyleSyntax style] } || style.Name != name)
        {
            throw ProtocolJson.InvalidParameters("The new name must be a single valid style name.");
        }

        var symbol = StyleIndex.At(context, offset);
        if (symbol is null)
        {
            return null;
        }

        if (IsPackageStyle(context, symbol.Name))
        {
            throw ProtocolJson.InvalidParameters("Styles declared in restored packages cannot be renamed. Change the source repository and update the package instead.");
        }

        var index = StyleIndex.Create(context.Project);
        if (name != symbol.Name && index.Any(item => item.Name == name))
        {
            throw ProtocolJson.InvalidParameters("A style with that name already exists in this project.");
        }

        var changes = new JsonObject();
        foreach (var group in index.Where(item => item.Name == symbol.Name).GroupBy(item => item.Source.Path))
        {
            var edits = new JsonArray();
            foreach (var item in group)
            {
                edits.Add((JsonNode)ProtocolJson.Edit(item.Source, item.Span, name));
            }

            changes[ProtocolJson.FileUri(group.Key)] = edits;
        }

        return new JsonObject { ["changes"] = changes };
    }

    private static bool IsPackageStyle(EditorContext context, string name)
    {
        return StyleIndex.Create(context.Project).Any(item => item.Name == name
            && context.Project.Packages.References.Any(package => package.Root is { } root && PackagePaths.IsWithin(item.Source.Path, root)));
    }

    public static JsonArray DocumentHighlights(EditorContext context, int offset)
    {
        var result = new JsonArray();
        var symbol = StyleIndex.At(context, offset);
        if (symbol is null)
        {
            return result;
        }

        foreach (var item in StyleIndex.Create(context.Project).Where(item => item.Name == symbol.Name
            && EditorProjectSource.PathComparer.Equals(item.Source.Path, context.Tree.Source.Path)))
        {
            result.Add((JsonNode)new JsonObject
            {
                ["range"] = ProtocolJson.Range(item.Source, item.Span),
                ["kind"] = item.Declaration ? 3 : 2
            });
        }

        return result;
    }

    public static JsonArray Links(EditorContext context)
    {
        var result = new JsonArray();
        foreach (var import in context.Project.Imports.Where(import =>
            EditorProjectSource.PathComparer.Equals(import.SourcePath, context.Tree.Source.Path)))
        {
            result.Add((JsonNode)new JsonObject
            {
                ["range"] = ProtocolJson.Range(context.Tree.Source, import.Syntax.Path.Span),
                ["target"] = ProtocolJson.FileUri(import.TargetPath)
            });
        }

        return result;
    }
}
