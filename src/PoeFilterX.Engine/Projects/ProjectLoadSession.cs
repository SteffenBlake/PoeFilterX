using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Logging;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Projects;

internal sealed class ProjectLoadSession(
    IProjectSource source,
    string projectRoot,
    ILogger logger,
    CancellationToken cancellationToken
)
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Dictionary<string, SyntaxTree> trees = [with(PathComparer)];
    private readonly Dictionary<string, ImmutableDictionary<string, ImmutableArray<string>>> variables = [with(PathComparer)];
    private readonly HashSet<string> active = [with(PathComparer)];
    private readonly HashSet<string> visited = [with(PathComparer)];
    private readonly List<ProjectImport> imports = [];
    private readonly List<SyntaxDiagnostic> diagnostics = [];

    public ProjectSnapshot Load(string entryPoint)
    {
        Visit(entryPoint, entryPoint, default);

        return new ProjectSnapshot(
            entryPoint,
            trees.ToImmutableDictionary(PathComparer),
            variables.ToImmutableDictionary(PathComparer),
            [.. imports],
            [.. diagnostics]
        );
    }

    private void Visit(
        string path,
        string importingPath,
        SourceSpan importSpan
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (active.Contains(path))
        {
            Report("PFX2001", $"Circular Dependency importing '{path}'.", importingPath, importSpan);
            return;
        }

        if (active.Count >= 128)
        {
            Report("PFX2002", "Maximum import depth of 128 exceeded.", importingPath, importSpan);
            return;
        }

        if (!visited.Add(path))
        {
            CompilerLog.CachedFile(logger, path);
            return;
        }

        active.Add(path);
        try
        {
            ReadFile(path);
        }
        catch (IOException exception)
        {
            Report("PFX2003", $"File not found or cannot read '{path}': {exception.Message}", importingPath, importSpan);
        }
        catch (UnauthorizedAccessException exception)
        {
            Report("PFX2003", $"File not found or cannot read '{path}': {exception.Message}", importingPath, importSpan);
        }
        finally
        {
            active.Remove(path);
        }
    }

    private void ReadFile(string path)
    {
        CompilerLog.OpeningFile(logger, path);
        using var stream = source.OpenRead(path);
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            ReadVariables(path, stream);
            return;
        }

        var parsed = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".filterx" => FilterXParserService.Parse(stream, path, cancellationToken),
            ".fss" => FssParserService.Parse(stream, path, cancellationToken),
            _ => new ParseError("PFX2004", "Expected a .filterx, .fss, or .json file.")
        };
        if (parsed is ParseError { PartialTree: null } error)
        {
            diagnostics.AddRange(error.At(path, default).Diagnostics);
            return;
        }

        var tree = parsed switch
        {
            SyntaxTree value => value,
            Deprecated<SyntaxTree> value => value.Value,
            ParseError { PartialTree: { } partial } => partial,
            _ => throw new InvalidOperationException("A parsed file has no syntax tree.")
        };

        CompilerLog.ParsedFile(logger, path, tree.Root.Statements.Length, tree.Diagnostics.Length);
        trees.Add(path, tree);
        diagnostics.AddRange(tree.Diagnostics);
        VisitImports(tree);
    }

    private void VisitImports(SyntaxTree tree)
    {
        var rootImports = tree.Root.Statements.OfType<ImportSyntax>();
        foreach (var import in rootImports)
        {
            VisitImport(tree, import);
        }

        foreach (var block in tree.Root.Statements.OfType<FilterBlockSyntax>())
        {
            ReportNestedImports(tree.Source.Path, block);
        }
    }

    private void VisitImport(SyntaxTree tree, ImportSyntax import)
    {
        var importText = tree.Source.Slice(import.Span);
        if (importText.IsEmpty || importText[^1] != ';')
        {
            return;
        }

        if (tree.Diagnostics.Any(error =>
            error.Span.Start >= import.Span.Start && error.Span.Start < import.Span.End))
        {
            return;
        }

        var from = tree.Source.Path;
        var raw = import.Path.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            Report("PFX2004", "Missing import path.", from, import.Path.Span);
            return;
        }

        try
        {
            var target = ResolvePath(from, raw);
            if (!AllowedImport(from, target))
            {
                var expected = Path.GetExtension(from).Equals(".fss", StringComparison.OrdinalIgnoreCase)
                    ? ".fss"
                    : ".filterx, .fss, or .json";
                Report(
                    "PFX2004",
                    $"Unrecognized file extension in import '{raw}'; expected {expected}.",
                    from,
                    import.Path.Span
                );
                return;
            }

            CompilerLog.ImportResolved(logger, from, target);
            imports.Add(new ProjectImport(from, target, import));
            Visit(target, from, import.Path.Span);
        }
        catch (ArgumentException exception)
        {
            Report("PFX2004", exception.Message, from, import.Path.Span);
        }
    }

    private string ResolvePath(string from, string raw)
    {
        if (raw.StartsWith('@'))
        {
            var relative = raw[1..].TrimStart('/', '\\');

            return Path.GetFullPath(Path.Combine(projectRoot, "node_modules", relative));
        }

        return Path.GetFullPath(raw, Path.GetDirectoryName(from)!);
    }

    private static bool AllowedImport(string from, string target)
    {
        var extension = Path.GetExtension(target).ToLowerInvariant();

        return Path.GetExtension(from).Equals(".fss", StringComparison.OrdinalIgnoreCase)
            ? extension == ".fss"
            : extension is ".filterx" or ".fss" or ".json";
    }

    private void ReportNestedImports(string path, FilterBlockSyntax block)
    {
        foreach (var import in block.Statements.OfType<ImportSyntax>())
        {
            Report("PFX2005", "Unexpected command 'using', imports must appear at the file root.", path, import.Span);
        }

        foreach (var child in block.Statements.OfType<FilterBlockSyntax>())
        {
            ReportNestedImports(path, child);
        }
    }

    private void ReadVariables(string path, Stream stream)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(stream);
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = ParseVariables(document.RootElement);
            if (parsed is ParseError error)
            {
                diagnostics.AddRange(error.At(path, default).Diagnostics);
                return;
            }

            if (parsed is ImmutableDictionary<string, ImmutableArray<string>> values)
            {
                variables.Add(path, values);
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    foreach (var (name, items) in values)
                    {
                        CompilerLog.VariableLoaded(logger, name, items.Length, path);
                    }
                }
            }
        }
        catch (JsonException exception)
        {
            Report("PFX2006", $"JsonException in variable file: {exception.Message}", path, default);
        }
    }

    private static Result<ImmutableDictionary<string, ImmutableArray<string>>> ParseVariables(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Null)
        {
            return ImmutableDictionary<string, ImmutableArray<string>>.Empty;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ParseError("PFX2006", "Expected an object containing arrays of strings.");
        }

        var values = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return new ParseError("PFX2006", $"Variable '{property.Name}' must be an array of strings.");
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                return new ParseError("PFX2006", $"Variable '{property.Name}' must be an array of strings.");
            }

            var elements = property.Value;
            var strings = ImmutableArray.CreateBuilder<string>(elements.GetArrayLength());
            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    return new ParseError("PFX2006", "Variable array members must be strings.");
                }

                strings.Add(element.GetString()!);
            }

            values[property.Name] = strings.MoveToImmutable();
        }

        return values.ToImmutable();
    }

    private void Report(
        string code,
        string message,
        string path,
        SourceSpan span
    )
    {
        diagnostics.Add(new SyntaxDiagnostic(code, message, path, span));
    }
}
