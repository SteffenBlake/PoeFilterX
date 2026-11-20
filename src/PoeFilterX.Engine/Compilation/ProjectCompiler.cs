using System.Collections.Immutable;
using System.Text;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

public static class ProjectCompiler
{
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
        var written = 0;
        var stylesByName = project.Styles.Select((style, rank) => (Style: style, Rank: rank))
            .ToLookup(item => item.Style.Name, StringComparer.Ordinal);
        var comparer = EqualityComparer<ImmutableArray<string>>.Create(
            static (left, right) => left.AsSpan().SequenceEqual(right.AsSpan()),
            StyleNamesHashCode
        );
        var cachedStyles = new Dictionary<ImmutableArray<string>, StyleOperations>(comparer);
        var conditions = new StringBuilder(512);
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

            if (!cachedStyles.TryGetValue(model.Style, out var style))
            {
                var resolved = ResolveStyles(block, stylesByName);
                if (resolved is ParseError error)
                {
                    return error;
                }

                style = resolved switch
                {
                    StyleOperations value => value,
                    Deprecated<StyleOperations> value => value.Value,
                    _ => throw new InvalidOperationException("A validated style has no value.")
                };
                cachedStyles.Add(model.Style, style);
            }

            conditions.Clear();
            FilterOperationsCodec.Write(conditions, model);
            if (!block.IsRoot && conditions.Length == 0)
            {
                continue;
            }

            actions.Clear();
            StyleOperationsCodec.Write(actions, style);
            if (conditions.Length + actions.Length == 0)
            {
                continue;
            }

            writer.WriteLine(style.Show == false ? "Hide" : "Show");
            writer.Write(conditions);
            writer.Write(actions);
            writer.WriteLine();
            written++;
        }

        return written;
    }

    private static Result<StyleOperations> ResolveStyles(
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

        var result = new StyleOperations();
        foreach (var (style, _) in selected.SelectMany(name => stylesByName[name]).OrderBy(item => item.Rank))
        {
            result = StyleOperationsCodec.Apply(result, style.Operations);
        }

        return result;
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
