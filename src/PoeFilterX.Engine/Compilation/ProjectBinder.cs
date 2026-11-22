using System.Collections.Immutable;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Compilation;

internal static class ProjectBinder
{
    private sealed record BoundBlock(
        int Parent,
        bool IsAbstract,
        FilterOperations Local,
        string Path,
        SourceSpan Span
    )
    {
        public SourceReference? Import { get; init; }
    }

    // Mutable builders are scoped to a single bind. Neither snapshots nor caller configuration are modified.
    private sealed record BindingState(
        ProjectSnapshot Project,
        IReadOnlyDictionary<string, string>? Configuration,
        Dictionary<(string Path, int Offset), ProjectImport> Imports
    )
    {
        public Dictionary<string, string> Variables { get; } = [with(StringComparer.OrdinalIgnoreCase)];
        public Dictionary<string, List<string>> LoadedVariables { get; } = [with(StringComparer.Ordinal)];
        public List<BoundBlock> Blocks { get; } = [];
        public List<PreparedStyle> Styles { get; } = [];
        public ParseError? Error { get; set; }
    }

    public static Result<PreparedProject> Bind(
        ProjectSnapshot project,
        IReadOnlyDictionary<string, string>? configuration
    )
    {
        if (!project.Diagnostics.IsEmpty)
        {
            return new ParseError(project.Diagnostics);
        }

        var state = new BindingState(
            project,
            configuration,
            project.Imports.ToDictionary(import => (import.SourcePath, import.Syntax.Span.Start))
        );
        if (configuration is not null)
        {
            foreach (var item in configuration)
            {
                state.Variables[item.Key] = item.Value;
            }
        }

        BindFile(state, project.EntryPoint, -1);
        if (state.Error is { } error)
        {
            return error;
        }

        var prepared = ImmutableArray.CreateBuilder<PreparedBlock>(state.Blocks.Count);
        foreach (var block in state.Blocks)
        {
            var inherited = block.Parent < 0 ? new FilterOperations() : prepared[block.Parent].Operations;
            prepared.Add(new PreparedBlock(
                FilterOperationsCodec.Apply(inherited, block.Local),
                block.Parent < 0,
                block.IsAbstract,
                block.Path,
                block.Span
            )
            { Trace = Trace(block, prepared), Local = block.Local, Parent = block.Parent < 0 ? null : prepared[block.Parent] });
        }

        return new PreparedProject(prepared.MoveToImmutable(), [.. state.Styles]);
    }

    private static ImmutableArray<SourceReference> Trace(
        BoundBlock block,
        ImmutableArray<PreparedBlock>.Builder prepared
    )
    {
        var trace = block.Parent < 0 ? []
            : prepared[block.Parent].Trace.Add(new(prepared[block.Parent].Path, prepared[block.Parent].Span));

        return block.Import is { } import ? trace.Add(import) : trace;
    }

    private static void BindFile(
        BindingState state,
        string path,
        int parent
    )
    {
        if (state.Project.VariableFiles.TryGetValue(path, out var data))
        {
            BindVariables(state, data);
            return;
        }

        var tree = state.Project.SyntaxTrees[path];
        if (Path.GetExtension(path).Equals(".fss", StringComparison.OrdinalIgnoreCase))
        {
            BindStyles(state, tree);
            return;
        }

        BindBlock(state, tree, tree.Root.Statements, parent, false, tree.Root.Span);
    }

    private static void BindVariables(
        BindingState state,
        ImmutableDictionary<string, ImmutableArray<string>> data
    )
    {
        foreach (var (key, values) in data)
        {
            if (!state.LoadedVariables.TryGetValue(key, out var existing))
            {
                existing = [];
                state.LoadedVariables.Add(key, existing);
            }

            existing.AddRange(values);
            if (state.Configuration?.ContainsKey(key) != true)
            {
                state.Variables[key] = string.Join(' ', existing.Distinct().Select(value => $"\"{value}\""));
            }
        }
    }

    private static void BindBlock(
        BindingState state,
        SyntaxTree tree,
        ImmutableArray<SyntaxNode> statements,
        int parent,
        bool abstracted,
        SourceSpan span
    )
    {
        var index = state.Blocks.Count;
        state.Blocks.Add(new(parent, abstracted, new(), tree.Source.Path, span));
        var operations = new FilterOperations();
        foreach (var statement in statements)
        {
            if (state.Error is not null)
            {
                return;
            }

            switch (statement)
            {
                case CommandSyntax command:
                    var parsed = BindFilter(state, command);
                    if (parsed is ParseError error)
                    {
                        state.Error = error.At(tree.Source.Path, command.Span);
                        return;
                    }

                    var applied = parsed.Map(
                        operations,
                        static (patch, previous) => FilterOperationsCodec.Apply(previous, patch)
                    );
                    operations = applied switch
                    {
                        FilterOperations value => value,
                        Deprecated<FilterOperations> value => value.Value,
                        _ => throw new InvalidOperationException("A validated operation has no value.")
                    };
                    break;
                case ImportSyntax import:
                    BindImport(state, tree, import, index);
                    break;
                case FilterBlockSyntax child:
                    BindBlock(state, tree, child.Statements, index, child.IsAbstract, child.Span);
                    break;
            }
        }

        state.Blocks[index] = state.Blocks[index] with { Local = operations };
    }

    private static void BindImport(
        BindingState state,
        SyntaxTree tree,
        ImportSyntax import,
        int parent
    )
    {
        var target = state.Imports[(tree.Source.Path, import.Span.Start)].TargetPath;
        var first = state.Blocks.Count;
        BindFile(state, target, parent);
        if (state.Blocks.Count > first)
        {
            state.Blocks[first] = state.Blocks[first] with { Import = new(tree.Source.Path, import.Span) };
        }
    }

    private static Result<FilterOperations> BindFilter(BindingState state, CommandSyntax command)
    {
        return VariableExpansion.Expand(command.Arguments.Select(argument => argument.Value), state.Variables)
            .FlatMap(command.Name, static (arguments, name) => FilterOperationsCodec.Parse(name, arguments));
    }

    private static void BindStyles(BindingState state, SyntaxTree tree)
    {
        foreach (var statement in tree.Root.Statements)
        {
            if (state.Error is not null)
            {
                return;
            }

            if (statement is ImportSyntax import)
            {
                BindImport(state, tree, import, -1);
                continue;
            }

            if (statement is StyleSyntax style)
            {
                BindStyle(state, tree, style);
            }
        }
    }

    private static void BindStyle(
        BindingState state,
        SyntaxTree tree,
        StyleSyntax style
    )
    {
        foreach (var command in style.Declarations)
        {
            var arguments = command.Arguments.Select(argument => argument.Value).ToArray();
            var parsed = StyleOperationsCodec.Parse(command.Name.ToLowerInvariant(), arguments);
            if (parsed is ParseError error)
            {
                state.Error = error.At(tree.Source.Path, command.Span);
                return;
            }

            var patch = parsed switch
            {
                StyleOperations value => value,
                Deprecated<StyleOperations> value => value.Value,
                _ => throw new InvalidOperationException("A validated style has no value.")
            };
            state.Styles.Add(new(style.Name, patch)
            {
                Path = tree.Source.Path,
                Span = command.Span,
                StyleSpan = style.Span,
                Declaration = tree.Source.Slice(command.Span).ToString()
            });
        }
    }
}
