using System.Text;
using PoeFilterX.Engine.Projects;

namespace PoeFilterX.Engine.Tests.Projects;

internal sealed class MemoryProjectSource : IProjectSource
{
    private readonly Dictionary<string, string> files = [];

    public Dictionary<string, int> Reads { get; } = [];

    public void Add(string path, string text) => files[Path.GetFullPath(path)] = text;

    public Stream OpenRead(string absolutePath)
    {
        Reads[absolutePath] = Reads.GetValueOrDefault(absolutePath) + 1;
        if (!files.TryGetValue(absolutePath, out var text))
        {
            throw new FileNotFoundException("Missing source.", absolutePath);
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }
}
