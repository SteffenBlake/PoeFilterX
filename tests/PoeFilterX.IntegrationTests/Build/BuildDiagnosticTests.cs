using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class BuildDiagnosticTests
{
    [Test]
    public async Task Syntax_failure_renders_native_message_code_and_EOF_location_without_replacing_output()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "using styles.fss\r\n");
        workspace.Write("main.filter", "existing output");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain(
                "main.filterx(2,1): error PFX1003: missing ';' at '<EOF>'"));
            Assert.That(TestWorkspace.Normalize(result.StandardError), Does.Contain(" 2 | \n   | ^\n"));
            Assert.That(workspace.Read("main.filter"), Is.EqualTo("existing output"));
        }
    }

    [Test]
    public async Task Nested_stylesheet_error_prints_the_complete_import_trace()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "# entry\nusing child.filterx;");
        workspace.Write("child.filterx", "# child\n\nusing styles.fss;");
        workspace.Write("styles.fss", "test {\n    font-size 40;\n}");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        var output = TestWorkspace.Normalize(result.StandardError);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(output, Does.Contain("styles.fss(2,15): error PFX1003: missing ':' at '40'"));
            Assert.That(output, Does.Contain(" 2 |     font-size 40;\n   |               ^^\n"));
            Assert.That(output, Does.Contain("child.filterx(3,7): note: imported"));
            Assert.That(output, Does.Contain("main.filterx(2,7): note: imported"));
            Assert.That(output, Does.Not.Contain("FormatException"));
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }
    }

    [Test]
    public async Task Recovery_reports_errors_in_multiple_styles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "using styles.fss;");
        workspace.Write("styles.fss", "one { font-size 40; }\ntwo { show true; }");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("styles.fss(1,17): error PFX1003:"));
            Assert.That(result.StandardError, Does.Contain("styles.fss(2,12): error PFX1003:"));
        }
    }

    [Test]
    public async Task Malformed_literals_have_specific_codes_instead_of_legacy_wording()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "BaseType \"open");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain(
                "main.filterx(1,10): error PFX1010: unterminated string literal"));
            Assert.That(result.StandardError, Does.Not.Contain("Missing ';' at end of command"));
        }
    }

    [Test]
    public async Task Semantic_failure_uses_the_same_compiler_format_and_trace()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.filterx", "using child.filterx;");
        workspace.Write("child.filterx", "# comment\nItemLevel bananas;");
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("child.filterx(2,1): error PFX3003:"));
            Assert.That(result.StandardError, Does.Contain("main.filterx(1,7): note: imported"));
            Assert.That(result.StandardError, Does.Not.Contain("FormatException"));
        }
    }
}
