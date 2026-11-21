using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Projects;

/// <summary>Optional parsed-file cache for hosts that repeatedly load an evolving project.</summary>
public interface IProjectSyntaxSource : IProjectSource
{
    Result<SyntaxTree> Parse(string absolutePath, CancellationToken cancellationToken);
}
