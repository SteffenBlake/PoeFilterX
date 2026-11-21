using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Parsing;

public static class ProjectSyntaxParser
{
    public static Result<SyntaxTree> Parse(
        Stream stream,
        string path,
        CancellationToken cancellationToken = default
    )
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".filterx" => FilterXParserService.Parse(stream, path, cancellationToken),
            ".fss" => FssParserService.Parse(stream, path, cancellationToken),
            _ => new ParseError("PFX2004", "Expected a .filterx, .fss, or .json file.")
        };
    }
}
