using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class VerbosityTests
{
    [TestCase("Trace", true, true, true)]
    [TestCase("Debug", true, true, false)]
    [TestCase("Information", true, false, false)]
    [TestCase("Warning", false, false, false)]
    [TestCase("Error", false, false, false)]
    [TestCase("Critical", false, false, false)]
    [TestCase("None", false, false, false)]
    public async Task Standard_log_levels_filter_progress_file_and_import_events(
        string level,
        bool information,
        bool debug,
        bool trace
    )
    {
        using var workspace = CreateProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["main.filterx", "--verbosity", level]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(result.StandardOutput.Contains("Building from"), Is.EqualTo(information));
            Assert.That(result.StandardOutput.Contains("Opening '"), Is.EqualTo(debug));
            Assert.That(result.StandardOutput.Contains("Import '"), Is.EqualTo(trace));
            Assert.That(result.StandardOutput.Contains("Loaded variable '"), Is.EqualTo(trace));
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.True);
        }

        if (!information)
        {
            Assert.That(result.StandardOutput, Is.Empty);
        }
    }

    [Test]
    public async Task Default_level_restores_build_progress_without_debug_details()
    {
        using var workspace = CreateProject();
        workspace.Write("entrypoint.filterx", workspace.Read("main.filterx"));
        var result = await CliRunner.RunAsync(workspace.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Contain("Building from").And.Contain("Publishing to").And.Contain("Published 1 blocks"));
            Assert.That(result.StandardOutput, Does.Not.Contain("Opening '"));
            Assert.That(result.StandardError, Is.Empty);
        }
    }

    [TestCase("--verbosity", "trace")]
    [TestCase("--v", "Debug")]
    [TestCase("-v", "Information")]
    public async Task Verbosity_accepts_level_names_case_insensitively_and_with_aliases(string option, string level)
    {
        using var workspace = CreateProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx", option, level]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Contain("Publishing to"));
            Assert.That(result.StandardError, Is.Empty);
        }
    }

    [Test]
    public async Task Equals_verbosity_is_not_consumed_as_a_variable()
    {
        using var workspace = CreateProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["main.filterx", "--verbosity=Trace"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(result.StandardOutput, Does.Contain("Import '"));
            Assert.That(result.StandardError, Is.Empty);
        }
    }

    [TestCase("Trace", true)]
    [TestCase("Debug", true)]
    [TestCase("Information", true)]
    [TestCase("Warning", true)]
    [TestCase("Error", true)]
    [TestCase("Critical", false)]
    [TestCase("None", false)]
    public async Task Compiler_errors_respect_the_threshold_and_preserve_import_traces(
        string level,
        bool visible
    )
    {
        using var workspace = CreateProject();
        workspace.Write("child.filterx", "# imported file\nItemLevel bananas;");
        workspace.Write("main.filterx", "using child.filterx;");
        var result = await CliRunner.RunAsync(workspace.Root, ["main.filterx", "--verbosity", level]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError.Contains("child.filterx(2,1): error PFX3003:"), Is.EqualTo(visible));
            Assert.That(result.StandardError.Contains("main.filterx(1,7): note: imported"), Is.EqualTo(visible));
            Assert.That(result.StandardOutput, Does.Not.Contain("error PFX"));
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }

        if (!visible)
        {
            Assert.That(result.StandardError, Is.Empty);
        }
    }

    [TestCase("--verbosity", "true")]
    [TestCase("--verbosity", "Verbose")]
    [TestCase("--verbosity", "999")]
    [TestCase("--verbosity")]
    [TestCase("--verbose", "true")]
    public async Task Invalid_or_obsolete_verbosity_options_are_rejected(params string[] arguments)
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

    private static TestWorkspace CreateProject()
    {
        var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "using values.json; using main.fss; { Style test; BaseType %Bases%; }");
        workspace.Write("values.json", "{\"Bases\":[\"Iron Ring\"]}");
        workspace.Write("main.fss", "test { show: true; font-size: 40; }");

        return workspace;
    }
}
