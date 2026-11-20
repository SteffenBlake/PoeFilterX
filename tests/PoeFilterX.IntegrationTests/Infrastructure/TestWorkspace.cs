using NUnit.Framework;

namespace PoeFilterX.IntegrationTests.Infrastructure;

internal sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "PoeFilterX integration ü",
        Guid.NewGuid().ToString("N")
    );

    public TestWorkspace()
    {
        Directory.CreateDirectory(Root);
    }

    public static TestWorkspace FromCase(string casePath)
    {
        var workspace = new TestWorkspace();
        var inputDirectory = Path.Combine(casePath, "Input");
        foreach (var source in Directory.GetFiles(inputDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Path.Combine(casePath, "Input"), source);
            var destination = workspace.PathFor(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }

        return workspace;
    }

    public string PathFor(string relative) => Path.Combine(Root, relative);

    public void Write(string relative, string contents)
    {
        var path = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    public string Read(string relative) => File.ReadAllText(PathFor(relative));

    public void AssertExpected(string expectedDirectory)
    {
        var expectedFiles = Directory.GetFiles(expectedDirectory, "*", SearchOption.AllDirectories);
        foreach (var expected in expectedFiles)
        {
            var relative = Path.GetRelativePath(expectedDirectory, expected);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(File.Exists(PathFor(relative)), Is.True, $"Missing output: {relative}");
                Assert.That(
                    Normalize(Read(relative)),
                    Is.EqualTo(Normalize(File.ReadAllText(expected))),
                    relative
                );
            }
        }

        var expectedFilters = expectedFiles
            .Where(path => Path.GetExtension(path) == ".filter")
            .Select(path => Path.GetRelativePath(expectedDirectory, path));
        var actualFilters = Directory.GetFiles(Root, "*.filter", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path));
        Assert.That(actualFilters, Is.EquivalentTo(expectedFilters), "Generated filter file set");
    }

    public void AssertInputsUnchanged(string inputDirectory)
    {
        foreach (var source in Directory.GetFiles(inputDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(inputDirectory, source);
            Assert.That(
                File.ReadAllBytes(PathFor(relative)),
                Is.EqualTo(File.ReadAllBytes(source)),
                relative
            );
        }
    }

    public static string Normalize(string text) => text.Replace("\r\n", "\n");

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
