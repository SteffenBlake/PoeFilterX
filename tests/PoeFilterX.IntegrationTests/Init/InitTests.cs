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
    public async Task Init_creates_project_with_legacy_name_normalization(string input, string name)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init"], input + "\n");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(result.StandardOutput, Does.Contain("Project Name:").And.Contain("Project created"));
            Assert.That(result.StandardOutput, Does.Contain("`poefilterx`"));
            Assert.That(Directory.GetDirectories(workspace.Root).Select(Path.GetFileName), Is.EqualTo([name]));
            Assert.That(Directory.GetFiles(workspace.PathFor(name)).Select(Path.GetFileName),
                Is.EquivalentTo(["entrypoint.filterx", name + ".fss"]));
            Assert.That(workspace.Read($"{name}/entrypoint.filterx"), Does.StartWith($"using {name}.fss;"));
            Assert.That(workspace.Read($"{name}/{name}.fss"), Does.Contain("missed {").And.Contain("base {").And.Contain("hidden {"));
        }
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
        var result = await CliRunner.RunAsync(workspace.Root, ["init"], input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("Invalid project name"));
            Assert.That(Directory.GetFileSystemEntries(workspace.Root), Is.Empty);
        }
    }

    [TestCase("--name")]
    [TestCase("--project-name")]
    public async Task Named_project_skips_prompt_and_builds_without_arguments(string option)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", option, "Example Project"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Not.Contain("Project Name:"));
        }

        var build = await CliRunner.RunAsync(workspace.PathFor("example-project"), []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(build.ExitCode, Is.Zero, build.StandardError);
            Assert.That(TestWorkspace.Normalize(workspace.Read("example-project/entrypoint.filter")),
                Is.EqualTo("""
                    Show
                        Class = "Currency"
                        SetBorderColor 0 0 0 255
                        SetTextColor 255 255 255 255
                        SetBackgroundColor 48 32 21 255
                        SetFontSize 45

                    Show
                        SetBorderColor 0 255 255 255
                        SetTextColor 255 255 0 255
                        SetBackgroundColor 255 20 147 255
                        SetFontSize 45


                    """.Replace("    ", "\t")));
        }

        workspace.Write("example-project/entrypoint.filterx",
            "using example-project.fss;\n{ Style test; Identified True; }");
        workspace.Write("example-project/example-project.fss", "test { font-size: 40; }");
        var styledBuild = await CliRunner.RunAsync(workspace.PathFor("example-project"), []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(styledBuild.ExitCode, Is.Zero, styledBuild.StandardError);
            Assert.That(TestWorkspace.Normalize(workspace.Read("example-project/entrypoint.filter")),
                Is.EqualTo("Show\n\tIdentified True\n\tSetFontSize 40\n\n"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Path_is_the_parent_directory_of_the_named_project(bool absolute)
    {
        using var workspace = new TestWorkspace();
        var parent = absolute ? workspace.PathFor("foo/bar/phi") : "foo/bar/phi";
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--path", parent, "--name", "baz"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Not.Contain("Project Name:"));
            Assert.That(workspace.Read("foo/bar/phi/baz/entrypoint.filterx"), Does.StartWith("using baz.fss;"));
            Assert.That(workspace.Read("foo/bar/phi/baz/baz.fss"), Does.Contain("missed {"));
            Assert.That(Directory.Exists(workspace.PathFor("baz")), Is.False);
        }
    }

    [Test]
    public async Task Path_can_be_supplied_with_an_interactive_name()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--path", "parent"], "Example Project\n");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(workspace.Read("parent/example-project/entrypoint.filterx"),
                Does.StartWith("using example-project.fss;"));
        }
    }

    [TestCase("Example Project")]
    [TestCase("example-project")]
    public async Task Existing_directory_is_preserved(string name)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("example-project/keep.txt", "preserve me");
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--name", name]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("already exists"));
            Assert.That(workspace.Read("example-project/keep.txt"), Is.EqualTo("preserve me"));
            Assert.That(Directory.GetFiles(workspace.PathFor("example-project")), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public async Task Existing_file_is_preserved()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("example-project", "preserve me");
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--name", "Example Project"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("already exists"));
            Assert.That(workspace.Read("example-project"), Is.EqualTo("preserve me"));
        }
    }

    [Test]
    public async Task Repeating_init_preserves_modified_styles()
    {
        using var workspace = new TestWorkspace();
        var first = await CliRunner.RunAsync(workspace.Root, ["init", "--name", "Example Project"]);
        Assert.That(first.ExitCode, Is.Zero, first.StandardError);
        workspace.Write("example-project/example-project.fss", "test { font-size: 40; }");
        var second = await CliRunner.RunAsync(workspace.Root, ["init", "--name", "Example Project"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.ExitCode, Is.EqualTo(1));
            Assert.That(second.StandardError, Does.Contain("already exists"));
            Assert.That(workspace.Read("example-project/example-project.fss"), Is.EqualTo("test { font-size: 40; }"));
        }
    }

    [TestCase("", "Invalid project name")]
    [TestCase("!!!", "Invalid project name")]
    public async Task Invalid_named_project_fails_without_prompting(string name, string message)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--name", name]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardOutput, Does.Not.Contain("Project Name:"));
            Assert.That(result.StandardError, Does.Contain(message));
            Assert.That(Directory.GetFileSystemEntries(workspace.Root), Is.Empty);
        }
    }

    [TestCase("--name")]
    [TestCase("--path")]
    [TestCase("--unknown", "value")]
    [TestCase("positional-name")]
    public async Task Invalid_arguments_fail_without_prompting(params string[] arguments)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", .. arguments]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardOutput, Does.Not.Contain("Project Name:"));
            Assert.That(result.StandardError, Is.Not.Empty);
            Assert.That(Directory.GetFileSystemEntries(workspace.Root), Is.Empty);
        }
    }

    [Test]
    public async Task Init_help_describes_named_options_without_prompting()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--help"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Contain("--name").And.Contain("--path"));
            Assert.That(result.StandardOutput, Does.Not.Contain("Project Name:"));
            Assert.That(Directory.GetFileSystemEntries(workspace.Root), Is.Empty);
        }
    }

    [Test]
    public async Task Global_options_can_precede_init_without_consuming_its_named_options()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root,
            ["--verbosity", "None", "init", "--name=baz", "--path=parent", "--verbosity", "None"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Is.Empty);
            Assert.That(workspace.Read("parent/baz/entrypoint.filterx"), Does.StartWith("using baz.fss;"));
        }
    }

    [Test]
    public async Task Parent_path_that_is_a_file_returns_an_error()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("parent", "preserve me");
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--name", "baz", "--path", "parent"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("PFX4001:"));
            Assert.That(workspace.Read("parent"), Is.EqualTo("preserve me"));
        }
    }

    [Test]
    public async Task Init_respects_log_level()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["init", "--name", "baz", "--verbosity", "None"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Is.Empty);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(workspace.Read("baz/entrypoint.filterx"), Does.StartWith("using baz.fss;"));
        }
    }
}
