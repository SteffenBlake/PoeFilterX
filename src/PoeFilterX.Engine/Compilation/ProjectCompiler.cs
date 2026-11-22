using System.Collections.Immutable;
using System.Text;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

public static class ProjectCompiler
{
    private sealed record ResolvedStyle(StyleOperations Operations, ImmutableArray<PreparedStyle> Declarations);

    /// <summary>Binds a parsed snapshot without accessing files, environment variables, or the console.</summary>
    public static Result<PreparedProject> Prepare(
        ProjectSnapshot project,
        IReadOnlyDictionary<string, string>? configuration = null
    )
    {
        return ProjectBinder.Bind(project, configuration);
    }

    public static Result<int> Write(PreparedProject project, TextWriter writer)
    {
        var resolved = ResolveRules(project);
        if (resolved is ParseError error)
        {
            return error;
        }

        var rules = (ImmutableArray<CompiledRule>)resolved.Value!;
        var buffer = new StringBuilder(768);
        foreach (var rule in rules)
        {
            writer.WriteLine(rule.Style.Show == false ? "Hide" : "Show");
            buffer.Clear();
            FilterOperationsCodec.Write(buffer, rule.Block.Operations);
            StyleOperationsCodec.Write(buffer, rule.Style);
            writer.Write(buffer);
            writer.WriteLine();
        }

        return rules.Length;
    }

    public static Result<ImmutableArray<CompiledRule>> ResolveRules(PreparedProject project)
    {
        var rules = ImmutableArray.CreateBuilder<CompiledRule>();
        var stylesByName = project.Styles.Select((style, rank) => (Style: style, Rank: rank))
            .ToLookup(item => item.Style.Name, StringComparer.Ordinal);
        var comparer = EqualityComparer<ImmutableArray<string>>.Create(
            static (left, right) => left.AsSpan().SequenceEqual(right.AsSpan()),
            StyleNamesHashCode
        );
        var cachedStyles = new Dictionary<ImmutableArray<string>, ResolvedStyle>(comparer);
        var actions = new StringBuilder(256);
        for (var index = project.Blocks.Length - 1; index >= 0; index--)
        {
            var block = project.Blocks[index];
            var model = block.Operations;
            if (block.IsAbstract || model.HasValue.Any(string.IsNullOrWhiteSpace) || model.IsTrue.Contains(false))
            {
                continue;
            }

            if (model.Style.IsEmpty)
            {
                continue;
            }

            if (!cachedStyles.TryGetValue(model.Style, out var selected))
            {
                var resolved = ResolveStyles(block, stylesByName);
                if (resolved is ParseError error)
                {
                    return error;
                }

                selected = (ResolvedStyle)resolved.Value!;
                cachedStyles.Add(model.Style, selected);
            }

            var hasConditions = FilterOperationsCodec.HasConditions(model);
            if (!block.IsRoot && !hasConditions)
            {
                continue;
            }

            if (!hasConditions)
            {
                actions.Clear();
                StyleOperationsCodec.Write(actions, selected.Operations);
                if (actions.Length == 0)
                {
                    continue;
                }
            }

            rules.Add(new(block, selected.Operations, selected.Declarations));
        }

        return rules.ToImmutable();
    }

    private static Result<ResolvedStyle> ResolveStyles(
        PreparedBlock block,
        ILookup<string, (PreparedStyle Style, int Rank)> stylesByName
    )
    {
        var selected = block.Operations.Style.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var name in selected)
        {
            if (!stylesByName.Contains(name))
            {
                return new ParseError([new SyntaxDiagnostic(
                    DiagnosticCodes.UndeclaredStyle,
                    $"Filter block referenced undeclared style '{name}'",
                    block.Path,
                    block.Span
                )]);
            }
        }

        var declarations = selected.SelectMany(name => stylesByName[name]).OrderBy(item => item.Rank)
            .Select(item => item.Style).ToImmutableArray();
        var result = new StyleOperations();
        foreach (var style in declarations)
        {
            result = StyleOperationsCodec.Apply(result, style.Operations);
        }

        return new ResolvedStyle(result, declarations);
    }

    private static int StyleNamesHashCode(ImmutableArray<string> names)
    {
        var hash = new HashCode();
        foreach (var name in names)
        {
            hash.Add(name, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
