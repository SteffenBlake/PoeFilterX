using System.Collections.Immutable;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Diagnostics;

public static class ProjectDiagnostics
{
    /// <summary>Attaches a deterministic entrypoint-to-file trace, also usable as LSP related information.</summary>
    public static ImmutableArray<DiagnosticReport> CreateReports(
        ProjectSnapshot project,
        ImmutableArray<SyntaxDiagnostic> diagnostics
    )
    {
        var parents = FindParents(project);
        var reports = ImmutableArray.CreateBuilder<DiagnosticReport>(diagnostics.Length);
        foreach (var diagnostic in diagnostics)
        {
            var trace = ImmutableArray.CreateBuilder<DiagnosticRelatedLocation>();
            var path = diagnostic.Path;
            while (parents.TryGetValue(path, out var import))
            {
                trace.Add(new(import.SourcePath, import.Syntax.Path.Span, $"imported '{path}' here"));
                path = import.SourcePath;
            }

            reports.Add(new(diagnostic, trace.ToImmutable()));
        }

        return reports.MoveToImmutable();
    }

    private static Dictionary<string, ProjectImport> FindParents(ProjectSnapshot project)
    {
        var comparer = project.SyntaxTrees.KeyComparer;
        var parents = new Dictionary<string, ProjectImport>(comparer);
        var visited = new HashSet<string>(comparer) { project.EntryPoint };
        var outgoing = project.Imports.ToLookup(import => import.SourcePath, comparer);
        var pending = new Queue<string>();
        pending.Enqueue(project.EntryPoint);
        while (pending.TryDequeue(out var path))
        {
            foreach (var import in outgoing[path])
            {
                if (!visited.Add(import.TargetPath))
                {
                    continue;
                }

                parents.Add(import.TargetPath, import);
                pending.Enqueue(import.TargetPath);
            }
        }

        return parents;
    }
}
