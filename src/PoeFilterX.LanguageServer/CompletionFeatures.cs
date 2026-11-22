using System.Text.Json.Nodes;
using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Packages;

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
            var import = tree.Root.Statements.OfType<ImportSyntax>().FirstOrDefault(node =>
                node.Path.IsQuoted && SyntaxFacts.Contains(node.Path.Span, offset));
            var pathSpan = import is null ? word : new SourceSpan(import.Path.Span.Start + 1, import.Path.Span.Length - 2);
            AddImportPaths(items, context, pathSpan);
        }
        else
        {
            AddOperations(items, context, head, word, offset);
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
        SourceSpan word,
        int offset
    )
    {
        var tree = context.Tree;
        var styles = Path.GetExtension(tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase);
        var operations = styles ? StyleOperationsCodec.Operations : FilterOperationsCodec.Operations;
        var operation = operations.FirstOrDefault(item => styles
            ? item.Name.Equals(head, StringComparison.OrdinalIgnoreCase) : item.Name == head);
        if (operation is not null && word.Start > FindHeadStart(tree, word.Start))
        {
            if (!styles && operation.Name is "Class" or "BaseType")
            {
                AddItemNames(items, context, operation.Name, offset);
            }
            else
            {
                AddValues(items, operation, tree.Source, word);
            }

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
        if (!styles && EditorProjectSource.PathComparer.Equals(tree.Source.Path, context.Project.EntryPoint))
        {
            items.Add((JsonNode)Item(
                "import", CompletionKind.Keyword, "Declare a Git package: import alias from \"URL\";", tree.Source, word
            ));
        }
    }

    private static void AddItemNames(
        JsonArray items,
        EditorContext context,
        string operation,
        int offset
    )
    {
        var span = ItemValueSpan(context.Tree, offset);
        var prefix = span.Start < context.Tree.Source.Text.Length && context.Tree.Source.Text[span.Start] == '"' ? "\"" : "";
        var values = operation == "Class"
            ? ItemCompletionData.ItemClassSuggestions
            : ItemCompletionData.BaseTypes;
        foreach (var value in values)
        {
            var item = Item(value.Name, CompletionKind.Value, value.SearchTags, context.Tree.Source, span);
            item["labelDetails"] = new JsonObject { ["description"] = value.SearchTags };
            item["filterText"] = prefix + value.Name + " " + value.SearchTags;
            item["textEdit"] = ProtocolJson.Edit(context.Tree.Source, span, QuoteItemName(value.Name));
            items.Add((JsonNode)item);
        }
    }

    private static SourceSpan ItemValueSpan(SyntaxTree tree, int offset)
    {
        var head = FindHeadStart(tree, offset);
        var tokens = tree.Tokens.Where(token => !token.IsTrivia && token.Span.Start > head && token.Span.Start <= offset);
        var start = SyntaxFacts.Word(tree.Source, offset).Start;
        foreach (var token in tokens)
        {
            if (token.Kind is "STRING" or "UNTERMINATED_STRING" or "INVALID_STRING")
            {
                if (SyntaxFacts.Contains(token.Span, offset))
                {
                    return token.Span;
                }

                start = token.Span.End;
            }
            else if (token.Kind == "OPERATOR" || tree.Source.Slice(token.Span).ToString().Contains('%'))
            {
                start = token.Span.End;
            }
            else
            {
                start = Math.Min(start, token.Span.Start);
            }
        }

        while (start < offset && char.IsWhiteSpace(tree.Source.Text[start]))
        {
            start++;
        }

        var end = SyntaxFacts.Word(tree.Source, offset).End;
        return new SourceSpan(start, end - start);
    }

    private static string QuoteItemName(string name)
    {
        return "\"" + name.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
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

        if (operation.Reader == "ReadColor")
        {
            values = values.Concat(NamedColors.Values.Keys.Order(StringComparer.Ordinal));
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

    private static void AddImportPaths(
        JsonArray items,
        EditorContext context,
        SourceSpan word
    )
    {
        var tree = context.Tree;
        var raw = tree.Source.Slice(word).ToString();
        var slash = raw.LastIndexOf('/');
        var prefix = slash < 0 ? "" : raw[..(slash + 1)];
        if ((raw.StartsWith('@') && slash < 0) || raw.Length == 0)
        {
            foreach (var package in context.Project.Packages.References)
            {
                items.Add((JsonNode)Item("@" + package.Alias + "/", CompletionKind.Folder,
                    package.Root is null ? "Package (restore required)" : "Imported package", tree.Source, word));
            }
        }

        if (raw.StartsWith('@') && slash < 0)
        {
            return;
        }

        var resolution = PackagePaths.Resolve(prefix + "_", tree.Source.Path, context.Project.Packages);
        if (resolution is not string targetFile)
        {
            return;
        }

        var target = Path.GetDirectoryName(targetFile)!;
        if (!Directory.Exists(target))
        {
            return;
        }

        var styles = Path.GetExtension(tree.Source.Path).Equals(".fss", StringComparison.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFileSystemEntries(target).Order(StringComparer.Ordinal))
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var folder = Directory.Exists(path);
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (!folder && extension != ".fss" && (styles || extension is not (".filterx" or ".json")))
            {
                continue;
            }

            var label = prefix + Path.GetFileName(path) + (folder ? "/" : "");
            var item = Item(label, folder ? CompletionKind.Folder : CompletionKind.File, "Import", tree.Source, word);
            var quoted = word.Start > 0 && tree.Source.Text[word.Start - 1] == '"';
            if (!quoted && label.Any(character => char.IsWhiteSpace(character) || character is '#' or ';' or '"'))
            {
                var escaped = label.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
                item["textEdit"] = ProtocolJson.Edit(tree.Source, word, "\"" + escaped + "\"");
            }

            items.Add((JsonNode)item);
        }
    }

    public static string Describe(OperationDescriptor operation)
    {
        var expected = operation.Reader switch
        {
            "ReadColor" => "CSS color name, RGB, RGBA, 0xRRGGBB, or 0xAARRGGBB",
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

internal enum CompletionKind { Class = 7, Property = 10, Value = 12, Keyword = 14, File = 17, Folder = 19, Variable = 6 }
