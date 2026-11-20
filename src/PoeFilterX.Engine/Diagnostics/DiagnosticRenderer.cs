using System.Collections.Immutable;
using System.Globalization;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Diagnostics;

/// <summary>Renders diagnostics without classifying errors or rewriting parser messages.</summary>
public static class DiagnosticRenderer
{
    public static void Write(
        ProjectSnapshot project,
        ImmutableArray<SyntaxDiagnostic> diagnostics,
        TextWriter writer
    )
    {
        foreach (var report in ProjectDiagnostics.CreateReports(project, diagnostics))
        {
            WriteReport(project, report, writer);
        }
    }

    private static void WriteReport(
        ProjectSnapshot project,
        DiagnosticReport report,
        TextWriter writer
    )
    {
        var diagnostic = report.Diagnostic;
        project.SyntaxTrees.TryGetValue(diagnostic.Path, out var tree);
        var location = tree is null ? diagnostic.Path
            : Location(diagnostic.Path, tree.Source.GetPosition(diagnostic.Span.Start));
        writer.WriteLine($"{location}: error {diagnostic.Code}: {diagnostic.Message}");
        if (tree is not null)
        {
            WriteExcerpt(tree.Source, diagnostic.Span, writer);
        }

        foreach (var frame in report.ImportTrace)
        {
            var source = project.SyntaxTrees[frame.Path].Source;
            var importedAt = source.GetPosition(frame.Span.Start);
            writer.WriteLine($"{Location(frame.Path, importedAt)}: note: {frame.Message}");
        }
    }

    private static string Location(string path, SourcePosition position)
    {
        return $"{path}({position.Line + 1},{position.Character + 1})";
    }

    private static void WriteExcerpt(
        SourceText source,
        SourceSpan span,
        TextWriter writer
    )
    {
        var position = source.GetPosition(span.Start);
        var lineSpan = source.GetLineSpan(position.Line);
        var line = source.Slice(lineSpan);
        var prefix = line[..position.Character].ToString().Replace("\t", "    ", StringComparison.Ordinal);
        var width = Math.Max(1, Math.Min(span.Length, line.Length - position.Character));
        var marked = line.Slice(position.Character, Math.Min(width, line.Length - position.Character))
            .ToString().Replace("\t", "    ", StringComparison.Ordinal);
        var number = (position.Line + 1).ToString(CultureInfo.InvariantCulture);
        var displayLine = line.ToString().Replace("\t", "    ", StringComparison.Ordinal);
        var underline = new string(' ', prefix.Length) + new string('^', Math.Max(1, marked.Length));
        var gutter = new string(' ', number.Length);

        writer.WriteLine($" {number} | {displayLine}");
        writer.WriteLine($" {gutter} | {underline}");
    }
}
