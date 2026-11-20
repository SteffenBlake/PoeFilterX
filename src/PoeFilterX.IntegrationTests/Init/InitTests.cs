using System.Text.Json;
using NUnit.Framework;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Init;

[Parallelizable(ParallelScope.All)]
public class InitTests
{
    [TestCase("example-project", "example-project")]
    [TestCase("Example Project", "example-project")]
    [TestCase("  Example !! Project  ", "example-project")]
    [TestCase("---Example---Project---", "example-project")]
    [TestCase("Project42", "project42")]
    [TestCase("../Example Project", "example-project")]
    [TestCase("one/two", "one-two")]
    public async Task Init_creates_complete_project_with_normalized_name(string input, string name)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, new[] { "init" }, input + "\n");
        Assert.That(result.ExitCode, Is.Zero);
        Assert.That(result.StandardError, Is.Empty);
        Assert.That(
            result.StandardOutput,
            Does.Contain("Project Name:").And.Contain("Project created")
        );
        Assert.That(
            Directory.GetDirectories(workspace.Root).Select(Path.GetFileName),
            Is.EqualTo(new[] { name })
        );
        var files = Directory.GetFiles(workspace.PathFor(name)).Select(Path.GetFileName);
        Assert.That(
            files,
            Is.EquivalentTo(new[] { name + ".filterx", name + ".fss", "package.json" })
        );
        Assert.That(
            workspace.Read(name + "/" + name + ".filterx"),
            Is.EqualTo($"using {name}.fss;")
        );
        Assert.That(workspace.Read(name + "/" + name + ".fss"), Is.Empty);

        using var package = JsonDocument.Parse(workspace.Read(name + "/package.json"));
        var root = package.RootElement;
        Assert.That(root.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(
            new[] { "name", "version", "description", "main", "author", "scripts" }
        ));
        Assert.That(root.GetProperty("name").GetString(), Is.EqualTo(name));
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("1.0.0"));
        Assert.That(root.GetProperty("description").GetString(), Is.Empty);
        Assert.That(root.GetProperty("main").GetString(), Is.EqualTo(name + ".filterx"));
        Assert.That(root.GetProperty("author").GetProperty("name").GetString(), Is.Empty);
        Assert.That(
            root.GetProperty("scripts").GetProperty("build").GetString(),
            Is.EqualTo("poefilterx build")
        );
    }

    [TestCase("\n")]
    [TestCase("   \n")]
    [TestCase("!!!\n")]
    [TestCase("---\n")]
    [TestCase("日本語\n")]
    [TestCase("")]
    public async Task Invalid_name_or_eof_fails_without_creating_files(string input)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, new[] { "init" }, input);
        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.StandardError, Does.Contain("Invalid project name"));
        Assert.That(Directory.GetFileSystemEntries(workspace.Root), Is.Empty);
    }

    [TestCase("example-project")]
    [TestCase("Example Project")]
    public async Task Existing_directory_is_preserved(string input)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("example-project/keep.txt", "preserve me");
        var result = await CliRunner.RunAsync(workspace.Root, new[] { "init" }, input + "\n");
        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(result.StandardError, Does.Contain("already exists"));
        Assert.That(workspace.Read("example-project/keep.txt"), Is.EqualTo("preserve me"));
        Assert.That(Directory.GetFiles(workspace.PathFor("example-project")).Length, Is.EqualTo(1));
    }

    [Test]
    public async Task Generated_project_matches_expected_files()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(
            workspace.Root,
            new[] { "init" }, "Example Project\n"
        );
        Assert.That(result.ExitCode, Is.Zero);
        workspace.AssertExpected(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Init/Cases/ExampleProject/Expected"
        ));
    }

    [Test]
    public async Task Package_entry_point_exists()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(
            workspace.Root,
            new[] { "init" }, "Example Project\n"
        );
        Assert.That(result.ExitCode, Is.Zero);
        using var package = JsonDocument.Parse(workspace.Read("example-project/package.json"));
        var entryPoint = package.RootElement.GetProperty("main").GetString()!;
        Assert.That(File.Exists(workspace.PathFor("example-project/" + entryPoint)), Is.True);
    }

    [Test]
    public async Task Existing_file_is_preserved()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("example-project", "preserve me");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            new[] { "init" }, "Example Project\n"
        );
        Assert.That(result.ExitCode, Is.Not.Zero);
        Assert.That(result.StandardError, Is.Not.Empty);
        Assert.That(workspace.Read("example-project"), Is.EqualTo("preserve me"));
        Assert.That(Directory.GetDirectories(workspace.Root), Is.Empty);
    }

    [Test]
    public async Task Repeating_init_refuses_to_overwrite_generated_project()
    {
        using var workspace = new TestWorkspace();
        var first = await CliRunner.RunAsync(workspace.Root, new[] { "init" }, "Example Project\n");
        Assert.That(first.ExitCode, Is.Zero);
        workspace.Write("example-project/example-project.fss", "test { font-size: 40; }");
        var second = await CliRunner.RunAsync(
            workspace.Root,
            new[] { "init" }, "Example Project\n"
        );
        Assert.That(second.ExitCode, Is.EqualTo(1));
        Assert.That(second.StandardError, Does.Contain("already exists"));
        Assert.That(
            workspace.Read("example-project/example-project.fss"),
            Is.EqualTo("test { font-size: 40; }")
        );
    }

    [Test]
    public async Task Generated_project_builds_and_its_generated_import_loads_styles()
    {
        using var workspace = new TestWorkspace();
        var init = await CliRunner.RunAsync(workspace.Root, new[] { "init" }, "Example Project\n");
        Assert.That(init.ExitCode, Is.Zero);
        var projectDirectory = workspace.PathFor("example-project");
        var emptyBuild = await CliRunner.RunAsync(projectDirectory, new[] { "build" });
        Assert.That(emptyBuild.ExitCode, Is.Zero);
        Assert.That(workspace.Read("example-project/example-project.filter"), Is.Empty);

        File.AppendAllText(
            workspace.PathFor("example-project/example-project.filterx"),
            "\n{ Style test; Identified True; }"
        );
        workspace.Write("example-project/example-project.fss", "test { font-size: 40; }");
        var build = await CliRunner.RunAsync(projectDirectory, new[] { "build" });
        Assert.That(build.ExitCode, Is.Zero);
        Assert.That(build.StandardError, Is.Empty);
        Assert.That(
            TestWorkspace.Normalize(workspace.Read("example-project/example-project.filter")),
            Is.EqualTo("Show\n\tIdentified True\n\tSetFontSize 40\n\n")
        );
    }
}
