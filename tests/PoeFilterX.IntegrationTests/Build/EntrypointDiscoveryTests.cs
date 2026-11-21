using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class EntrypointDiscoveryTests
{
    [TestCase("entrypoint.filterx")]
    [TestCase("game.entrypoint.filterx")]
    public async Task Default_build_selects_the_standard_name_among_other_filter_files(string entrypoint)
    {
        using var workspace = new TestWorkspace();
        workspace.Write(entrypoint, "using styles.fss; { Style test; Identified True; }");
        workspace.Write("styles.fss", "test { show: true; }");
        workspace.Write("main.filterx", "UnknownCommand;");
        workspace.Write("almost-entrypoint.filterx", "UnknownCommand;");
        var result = await CliRunner.RunAsync(workspace.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(TestWorkspace.Normalize(workspace.Read(Path.ChangeExtension(entrypoint, ".filter"))),
                Is.EqualTo("Show\n\tIdentified True\n\n"));
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }
    }

    [Test]
    public async Task Ambiguity_lists_candidates_and_explicit_input_resolves_it()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("entrypoint.filterx", "{ Identified True; }");
        workspace.Write("other.entrypoint.filterx", "UnknownCommand;");
        var ambiguous = await CliRunner.RunAsync(workspace.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ambiguous.ExitCode, Is.EqualTo(1));
            Assert.That(ambiguous.StandardError, Does.Contain("Multiple entrypoints")
                .And.Contain("entrypoint.filterx").And.Contain("other.entrypoint.filterx")
                .And.Contain("poefilterx <path>"));
            Assert.That(Directory.GetFiles(workspace.Root, "*.filter"), Is.Empty);
        }

        var selected = await CliRunner.RunAsync(workspace.Root, ["entrypoint.filterx"]);
        Assert.That(selected.ExitCode, Is.Zero, selected.StandardError);
    }

    [Test]
    public async Task An_arbitrary_filter_filename_requires_explicit_input()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "{ Identified True; }");
        var missing = await CliRunner.RunAsync(workspace.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing.ExitCode, Is.EqualTo(1));
            Assert.That(missing.StandardError, Does.Contain("No entrypoint.filterx or *.entrypoint.filterx"));
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }

        var selected = await CliRunner.RunAsync(workspace.Root, ["main.filterx"]);
        Assert.That(selected.ExitCode, Is.Zero, selected.StandardError);
    }
}
