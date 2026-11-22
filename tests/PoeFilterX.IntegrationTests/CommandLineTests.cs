using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests;

[Parallelizable(ParallelScope.All)]
public class CommandLineTests
{
    [TestCase("--help")]
    [TestCase("-h")]
    [TestCase("-?")]
    public async Task Help_lists_supported_commands(string argument)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, [argument]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(result.StandardOutput, Does.Contain("build").And.Contain("init").And.Contain("--help").And.Contain("--version"));
        }
    }

    [Test]
    public async Task Help_for_build_describes_build_options()
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "--help"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(result.StandardOutput, Does.Contain("--path").And.Contain("--output").And.Contain("--verbosity"));
        }
    }

    [TestCase("--version")]
    public async Task Version_displays_assembly_version(string argument)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, [argument]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Is.Empty);
            Assert.That(result.StandardOutput.Trim(), Does.Match(@"^\d+\.\d+\.\d+"));
        }
    }

    [TestCase("help")]
    [TestCase("version")]
    [TestCase("unknown")]
    [TestCase("--unknown")]
    [TestCase("--path")]
    [TestCase("--output")]
    public async Task Unsupported_commands_and_invalid_options_are_rejected(string argument)
    {
        using var workspace = new TestWorkspace();
        var result = await CliRunner.RunAsync(workspace.Root, [argument]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
        }
    }
}
