namespace PoeFilterX.Engine.Projects;

public sealed class FileProjectSource : IProjectSource
{
    public Stream OpenRead(string absolutePath)
    {
        return new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.SequentialScan
        );
    }
}
