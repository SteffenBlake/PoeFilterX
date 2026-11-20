using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class PositionalBuildTests
{
    private const string Expected = "Show\n\tIdentified True\n\tSetFontSize 40\n\n";

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Command_and_positional_path_are_optional(bool explicitCommand, bool explicitPath)
    {
        using var workspace = CreateProject();
        var arguments = new List<string>();
        if (explicitCommand)
        {
            arguments.Add("build");
        }

        if (explicitPath)
        {
            workspace.Write("other.filterx", "UnknownCommand;");
            arguments.Add("main.filterx");
        }

        var result = await CliRunner.RunAsync(workspace.Root, [.. arguments]);
        AssertOutput(workspace, result, "main.filter");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Absolute_positional_path_supports_spaces_and_imports_outside_working_directory(bool explicitCommand)
    {
        using var workspace = CreateProject();
        workspace.Write("nested ü/my filter.filterx", workspace.Read("main.filterx"));
        workspace.Write("nested ü/main.fss", workspace.Read("main.fss"));
        var arguments = new List<string>();
        if (explicitCommand)
        {
            arguments.Add("build");
        }

        arguments.Add(workspace.PathFor("nested ü/my filter.filterx"));
        var working = workspace.PathFor("unrelated directory");
        Directory.CreateDirectory(working);
        var result = await CliRunner.RunAsync(working, [.. arguments]);
        AssertOutput(workspace, result, "nested ü/my filter.filter");
        Assert.That(Directory.GetFiles(working), Is.Empty);
    }

    [Test]
    public async Task Positional_input_combines_with_output_and_variable_options()
    {
        using var workspace = CreateProject();
        workspace.Write("main.filterx", "using main.fss; { Style test; Identified %IdentifiedValue%; }");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["--output", "result.filter", "main.filterx", "--IdentifiedValue", "true"]
        );
        AssertOutput(workspace, result, "result.filter");
        Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
    }

    [Test]
    public async Task End_of_options_allows_a_positional_path_starting_with_a_dash()
    {
        using var workspace = CreateProject();
        workspace.Write("--sample.filterx", workspace.Read("main.filterx"));
        var result = await CliRunner.RunAsync(workspace.Root, ["--", "--sample.filterx"]);
        AssertOutput(workspace, result, "--sample.filter");
    }

    [TestCase("main.filterx", "other.filterx")]
    [TestCase("main.filterx", "--path", "main.filterx")]
    public async Task Multiple_input_paths_are_rejected(params string[] arguments)
    {
        using var workspace = CreateProject();
        var result = await CliRunner.RunAsync(workspace.Root, arguments);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Help_describes_the_optional_positional_input(bool explicitCommand)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, explicitCommand ? ["build", "--help"] : ["--help"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardOutput, Does.Contain("[<path>]"));
            Assert.That(result.StandardError, Is.Empty);
        }
    }

    private static TestWorkspace CreateProject()
    {
        var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "using main.fss; { Style test; Identified True; }");
        workspace.Write("main.fss", "test { show: true; font-size: 40; }");

        return workspace;
    }

    private static void AssertOutput(
        TestWorkspace workspace,
        CliResult result,
        string output
    )
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(TestWorkspace.Normalize(workspace.Read(output)), Is.EqualTo(Expected));
        }
    }
}
