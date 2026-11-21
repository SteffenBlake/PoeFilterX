using System.Text.Json.Nodes;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class CompletionFeatures
{
    public static JsonObject Complete(EditorContext context, int offset)
    {
        var tree = context.Tree;
        var word = SyntaxFacts.Word(tree.Source, offset);
        var head = SyntaxFacts.StatementHead(tree, offset);
        var items = new JsonArray();
        if (tree.Tokens.Any(token => SyntaxFacts.Contains(token.Span, offset) && token.Kind == "COMMENT"))
        {
            return new JsonObject { ["isIncomplete"] = false, ["items"] = items };
        }

        var filter = Path.GetExtension(tree.Source.Path).Equals(".filterx", StringComparison.OrdinalIgnoreCase);
        if (filter && head == "Style" && word.Start > FindHeadStart(tree, offset))
        {
            foreach (var name in StyleIndex.Create(context.Project).Where(item => item.Declaration)
                .Select(item => item.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                items.Add((JsonNode)Item(
                    name,
                    CompletionKind.Class,
                    "Style in this project's import graph",
                    tree.Source,
                    word
                ));
            }
        }
        else if (head.Equals("using", StringComparison.OrdinalIgnoreCase))
        {
            AddImportPaths(items, tree, word);
        }
        else
        {
            AddOperations(items, context, head, word);
        }

        return new JsonObject { ["isIncomplete"] = false, ["items"] = items };
    }

    private static int FindHeadStart(SyntaxTree tree, int offset)
    {
        return SyntaxFacts.Commands(tree.Root).LastOrDefault(command => command.Span.Start <= offset)?.NameSpan.Start ?? offset;
    }

    private static void AddOperations(
        JsonArray items,
        EditorContext context,
        string head,
        SourceSpan word
    )
    {
        var tree = context.Tree;
        var styles = Path.GetExtension(tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase);
        var operations = styles ? StyleOperationsCodec.Operations : FilterOperationsCodec.Operations;
        var operation = operations.FirstOrDefault(item => styles
            ? item.Name.Equals(head, StringComparison.OrdinalIgnoreCase) : item.Name == head);
        if (operation is not null && word.Start > FindHeadStart(tree, word.Start))
        {
            AddValues(items, operation, tree.Source, word);
            foreach (var name in context.Project.VariableFiles.Values.SelectMany(values => values.Keys).Distinct())
            {
                items.Add((JsonNode)Item(
                    $"%{name}%", CompletionKind.Variable, "Imported variable", tree.Source, word
                ));
            }

            return;
        }

        foreach (var item in operations)
        {
            items.Add((JsonNode)Item(item.Name, CompletionKind.Property, Describe(item), tree.Source, word));
        }

        items.Add((JsonNode)Item("using", CompletionKind.Keyword, "Import a file", tree.Source, word));
    }

    private static void AddValues(
        JsonArray items,
        OperationDescriptor operation,
        SourceText source,
        SourceSpan word
    )
    {
        IEnumerable<string> values = operation.Type == "bool?" ? ["True", "False"] : [];
        if (operation.Reader is "ReadToggle" or "ReadAlert" or "ReadIcon" or "ReadPillar")
        {
            values = values.Concat(["enabled", "disabled"]);
        }

        if (operation.Name is "icon-color" or "pillar-color" or "icon" or "pillar")
        {
            values = values.Concat(Enum.GetNames<FilterColor>());
        }

        if (operation.Name is "Rarity")
        {
            values = ["Normal", "Magic", "Rare", "Unique"];
        }

        if (operation.Name is "icon" or "icon-shape")
        {
            values = values.Concat(Enum.GetNames<MiniMapIconShape>());
        }

        if (operation.Name is "pillar" or "pillar-duration")
        {
            values = values.Concat(["Temporary", "Permanent"]);
        }

        foreach (var value in values.Distinct(StringComparer.Ordinal))
        {
            items.Add((JsonNode)Item(value, CompletionKind.Value, operation.Name, source, word));
        }
    }

    private static void AddImportPaths(JsonArray items, SyntaxTree tree, SourceSpan word)
    {
        var raw = tree.Source.Slice(word).ToString();
        var directory = Path.GetDirectoryName(tree.Source.Path)!;
        var relative = Path.GetDirectoryName(raw);
        var target = string.IsNullOrEmpty(relative) ? directory : Path.GetFullPath(relative, directory);
        if (!Directory.Exists(target))
        {
            return;
        }

        var styles = Path.GetExtension(tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(target).Order(StringComparer.Ordinal))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".fss" && (styles || extension is not (".filterx" or ".json")))
            {
                continue;
            }

            var label = Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/');
            items.Add((JsonNode)Item(label, CompletionKind.File, "Import", tree.Source, word));
        }
    }

    public static string Describe(OperationDescriptor operation)
    {
        var expected = operation.Reader switch
        {
            "ReadColor" => "RGB, RGBA, 0xRRGGBB, or 0xAARRGGBB",
            "ReadColorAlpha" or "ReadColorRed" or "ReadColorGreen" or "ReadColorBlue" => "integer from 0 through 255",
            "ReadToggle" => "enabled or disabled",
            "ReadAlert" => "sound ID, volume, and optional positional; or enabled/disabled",
            "ReadIcon" => "size, color, and shape; or enabled/disabled",
            "ReadPillar" => "color and optional Temporary/Permanent; or enabled/disabled",
            _ => DescribeType(operation)
        };

        return $"{operation.Name}: {expected}";
    }

    private static string DescribeType(OperationDescriptor operation)
    {
        if (operation.Type.Contains("int", StringComparison.Ordinal))
        {
            var comparison = operation.Type.Contains("OperatorValue", StringComparison.Ordinal) ? "optional comparison followed by " : "";
            return $"{comparison}integer from {operation.Minimum} through {operation.Maximum}";
        }

        if (operation.Type.Contains("bool", StringComparison.Ordinal))
        {
            return "True or False";
        }

        return operation.Name == "Style" ? "one or more imported style names" : "one or more values";
    }

    public static JsonNode? Signature(EditorContext context, int offset)
    {
        var name = SyntaxFacts.StatementHead(context.Tree, offset);
        var style = Path.GetExtension(context.Tree.Source.Path)
            .Equals(".fss", StringComparison.OrdinalIgnoreCase);
        var operations = style ? StyleOperationsCodec.Operations : FilterOperationsCodec.Operations;
        var operation = operations.FirstOrDefault(item => item.Name.Equals(name,
            style ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        if (operation is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["signatures"] = new JsonArray(new JsonObject { ["label"] = Describe(operation) }),
            ["activeSignature"] = 0
        };
    }

    private static JsonObject Item(
        string label,
        CompletionKind kind,
        string detail,
        SourceText source,
        SourceSpan word
    )
    {
        return new JsonObject
        {
            ["label"] = label,
            ["kind"] = (int)kind,
            ["detail"] = detail,
            ["textEdit"] = ProtocolJson.Edit(source, word, label)
        };
    }
}

internal enum CompletionKind { Class = 7, Property = 10, Value = 12, Keyword = 14, File = 17, Variable = 6 }
