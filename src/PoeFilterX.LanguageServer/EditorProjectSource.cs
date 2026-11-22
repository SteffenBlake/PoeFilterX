using System.Text;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal sealed class EditorProjectSource : IProjectSyntaxSource
{
    private sealed record CachedSyntax(long Revision, Result<SyntaxTree> Parsed);

    private readonly Dictionary<string, CachedSyntax> cache = [with(PathComparer)];

    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public Dictionary<string, EditorDocument> Documents { get; } = [with(PathComparer)];

    public Stream OpenRead(string absolutePath)
    {
        return Documents.TryGetValue(absolutePath, out var document)
            ? new MemoryStream(Encoding.UTF8.GetBytes(document.Source.Text), writable: false)
            : File.OpenRead(absolutePath);
    }

    public Result<SyntaxTree> Parse(string absolutePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revision = Documents.TryGetValue(absolutePath, out var document)
            ? document.Revision : File.GetLastWriteTimeUtc(absolutePath).Ticks;
        if (cache.TryGetValue(absolutePath, out var cached) && cached.Revision == revision)
        {
            return cached.Parsed;
        }

        using var stream = OpenRead(absolutePath);
        var parsed = ProjectSyntaxParser.Parse(stream, absolutePath, cancellationToken);
        cache[absolutePath] = new CachedSyntax(revision, parsed);

        return parsed;
    }

    public void Invalidate(string path) => cache.Remove(path);

    public SourceText ReadText(string path)
    {
        return Documents.TryGetValue(path, out var document) ? document.Source : new(path, File.ReadAllText(path));
    }
}

internal sealed record EditorDocument(SourceText Source, int Version, long Revision);
