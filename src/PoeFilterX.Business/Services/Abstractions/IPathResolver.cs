namespace PoeFilterX.Business.Services.Abstractions
{
    public interface IPathResolver
    {
        string ResolvePath(string executingPath, string relativePath);
    }
}