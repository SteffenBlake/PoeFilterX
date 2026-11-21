using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class StyleIndex
{
    private sealed record Symbols(ImmutableArray<StyleOccurrence> Items);

    private static readonly ConditionalWeakTable<ProjectSnapshot, Symbols> Cache = [];

    public static ImmutableArray<StyleOccurrence> Create(ProjectSnapshot project)
    {
        return Cache.GetValue(project, Build).Items;
    }

    private static Symbols Build(ProjectSnapshot project)
    {
        var items = ImmutableArray.CreateBuilder<StyleOccurrence>();
        foreach (var tree in project.SyntaxTrees.Values)
        {
            foreach (var style in tree.Root.Statements.OfType<StyleSyntax>().Where(style => style.Name.Length != 0))
            {
                items.Add(new(style.Name, tree.Source, style.NameSpan, true));
            }

            var filter = Path.GetExtension(tree.Source.Path).Equals(".filterx", StringComparison.OrdinalIgnoreCase);
            var commands = filter ? SyntaxFacts.Commands(tree.Root).Where(command => command.Name == "Style") : [];
            foreach (var command in commands)
            {
                foreach (var argument in command.Arguments)
                {
                    items.Add(new(argument.Value, tree.Source, argument.Span, false));
                }
            }
        }

        return new(items.ToImmutable());
    }

    public static StyleOccurrence? At(EditorContext context, int offset)
    {
        return Create(context.Project).FirstOrDefault(item =>
            EditorProjectSource.PathComparer.Equals(item.Source.Path, context.Tree.Source.Path)
            && SyntaxFacts.Contains(item.Span, offset));
    }
}

internal sealed record StyleOccurrence(string Name, SourceText Source, SourceSpan Span, bool Declaration);
