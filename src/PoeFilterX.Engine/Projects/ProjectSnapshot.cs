using System.Collections.Immutable;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Projects;

/// <summary>Parsed files and their ordered import graph, before variable expansion or compilation.</summary>
public sealed record ProjectSnapshot(
    string EntryPoint,
    ImmutableDictionary<string, SyntaxTree> SyntaxTrees,
    ImmutableDictionary<string, ImmutableDictionary<string, ImmutableArray<string>>> VariableFiles,
    ImmutableArray<ProjectImport> Imports,
    ImmutableArray<SyntaxDiagnostic> Diagnostics
);
