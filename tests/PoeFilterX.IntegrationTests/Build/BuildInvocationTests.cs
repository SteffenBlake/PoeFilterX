using NUnit.Framework;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class BuildInvocationTests
{
    private const string VariableKey = "PFX_IT_8C71_GearTypes";
    private const string Expected = "Show\n\tIdentified True\n\tSetFontSize 40\n\n";

    private static TestWorkspace SimpleProject()
    {
        var casePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Build/Cases/Success/Conditions/Identified/True"
        );
        return TestWorkspace.FromCase(casePath);
    }

    [TestCase("--p")]
    [TestCase("--path")]
    public async Task Explicit_input_resolves_relative_and_absolute_paths(string option)
    {
        using var workspace = SimpleProject();
        workspace.Write("another.filterx", "{ InvalidCommand 1; }");
        foreach (var path in new[] { "main.filterx", workspace.PathFor("main.filterx") })
        {
            var result = await CliRunner.RunAsync(workspace.Root, ["build", option, path]);
            AssertSuccess(result);
            Assert.That(
                TestWorkspace.Normalize(workspace.Read("main.filter")),
                Is.EqualTo(Expected)
            );
        }
    }

    [TestCase("--o")]
    [TestCase("--output")]
    public async Task Output_option_resolves_relative_and_absolute_paths(string option)
    {
        using var workspace = SimpleProject();
        Directory.CreateDirectory(workspace.PathFor("output ü"));
        foreach (var path in new[] { "output ü/result.filter", workspace.PathFor("output ü/result.filter") })
        {
            var result = await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx", option, path]);
            AssertSuccess(result);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(
                            TestWorkspace.Normalize(workspace.Read("output ü/result.filter")),
                            Is.EqualTo(Expected)
                        );
                Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
            }
        }
    }

    [Test]
    public async Task Equals_arguments_and_verbosity_option_are_accepted()
    {
        using var workspace = SimpleProject();
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path=main.filterx", "--output=result.filter", "--verbosity=Debug"]
        );
        AssertSuccess(result);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TestWorkspace.Normalize(workspace.Read("result.filter")), Is.EqualTo(Expected));
            Assert.That(result.StandardOutput, Does.Contain("Publishing to"));
        }
    }

    [TestCase("--v")]
    [TestCase("--verbosity")]
    public async Task Verbosity_alias_preserves_output(string option)
    {
        using var workspace = SimpleProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx", option, "Debug"]);
        AssertSuccess(result);
        Assert.That(TestWorkspace.Normalize(workspace.Read("main.filter")), Is.EqualTo(Expected));
    }

    [Test]
    public async Task Input_outside_working_directory_uses_its_own_imports_and_default_output()
    {
        using var workspace = SimpleProject();
        var working = workspace.PathFor("different working directory");
        Directory.CreateDirectory(working);
        var result = await CliRunner.RunAsync(
            working,
            ["build", "--path", workspace.PathFor("main.filterx")]
        );
        AssertSuccess(result);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TestWorkspace.Normalize(workspace.Read("main.filter")), Is.EqualTo(Expected));
            Assert.That(Directory.GetFiles(working), Is.Empty);
        }
    }

    [Test]
    public async Task Nested_input_with_dot_segments_resolves_correctly()
    {
        using var workspace = SimpleProject();
        workspace.Write("nested ü/main.filterx", workspace.Read("main.filterx"));
        workspace.Write("nested ü/main.fss", workspace.Read("main.fss"));
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "nested ü/../nested ü/./main.filterx"]
        );
        AssertSuccess(result);
        Assert.That(
            TestWorkspace.Normalize(workspace.Read("nested ü/main.filter")),
            Is.EqualTo(Expected)
        );
    }

    [TestCase("None")]
    [TestCase("Multiple")]
    [TestCase("NestedOnly")]
    public async Task Ambiguous_or_missing_discovery_fails_without_creating_output(string scenario)
    {
        using var workspace = new TestWorkspace();
        if (scenario == "Multiple")
        {
            workspace.Write("one.entrypoint.filterx", "");
            workspace.Write("entrypoint.filterx", "");
        }

        if (scenario == "NestedOnly")
        {
            workspace.Write("nested/entrypoint.filterx", "");
        }

        var result = await CliRunner.RunAsync(workspace.Root, ["build"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Does.Contain("Unable to automatically detect"));
            Assert.That(
                Directory.GetFiles(workspace.Root, "*.filter", SearchOption.AllDirectories),
                Is.Empty
            );
        }
    }

    [TestCase("missing.filterx")]
    [TestCase("main.fss")]
    [TestCase("folder.filterx")]
    public async Task Invalid_explicit_input_is_rejected(string path)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("main.fss", "");
        Directory.CreateDirectory(workspace.PathFor("folder.filterx"));
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "--path", path]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
            Assert.That(Directory.GetFiles(workspace.Root, "*.filter"), Is.Empty);
        }
    }

    [TestCase("--path")]
    [TestCase("--output")]
    public async Task Missing_option_value_is_rejected(string option)
    {
        using var workspace = SimpleProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["build", option]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }
    }

    [TestCase("--path")]
    [TestCase("--output")]
    public async Task Empty_path_value_is_rejected(string option)
    {
        using var workspace = SimpleProject();
        var result = await CliRunner.RunAsync(workspace.Root, ["build", option, ""]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
        }
    }

    [TestCase("output-folder")]
    [TestCase("missing-parent/result.filter")]
    public async Task Invalid_output_location_is_rejected(string output)
    {
        using var workspace = SimpleProject();
        Directory.CreateDirectory(workspace.PathFor("output-folder"));
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "main.filterx", "--output", output]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
            Assert.That(File.Exists(workspace.PathFor("main.filter")), Is.False);
        }
    }

    [Test]
    public async Task Rebuild_is_repeatable_and_truncates_old_output()
    {
        using var workspace = SimpleProject();
        workspace.Write("main.filter", new string('x', 2000));
        for (var iteration = 0; iteration < 2; iteration++)
        {
            AssertSuccess(await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx"]));
            Assert.That(
                TestWorkspace.Normalize(workspace.Read("main.filter")),
                Is.EqualTo(Expected)
            );
        }

        workspace.Write("main.filterx", "using main.fss; { Style test; Identified False; }");
        AssertSuccess(await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx"]));
        Assert.That(
            TestWorkspace.Normalize(workspace.Read("main.filter")),
            Is.EqualTo(Expected.Replace("True", "False"))
        );
    }

    [Test]
    public async Task Parse_failure_preserves_existing_output_and_reports_file_and_line()
    {
        using var workspace = SimpleProject();
        workspace.Write("main.filter", "existing output\n");
        workspace.Write("main.filterx", "using main.fss;\n{\nUnknownCommand 1;\n}");
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("main.filterx(3,1): error PFX3001:"));
            Assert.That(workspace.Read("main.filter"), Is.EqualTo("existing output\n"));
        }
    }

    [Test]
    public async Task Serialization_failure_exposes_current_partial_output_behavior()
    {
        using var workspace = SimpleProject();
        workspace.Write("main.filter", "existing output\n");
        workspace.Write("main.filterx", "using main.fss; { Style missing; Identified True; }");
        var result = await CliRunner.RunAsync(workspace.Root, ["build", "main.filterx"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("undeclared style 'missing'"));
            Assert.That(workspace.Read("main.filter"), Is.Empty);
        }
    }

    [TestCase(false, false, "Iron Ring")]
    [TestCase(true, false, "Environment Ring")]
    [TestCase(false, true, "Command Ring")]
    [TestCase(true, true, "Command Ring")]
    public async Task Variables_follow_cli_then_environment_then_json_precedence(
        bool useEnvironment,
        bool useCommandLine,
        string expectedValue
    )
    {
        using var workspace = SimpleProject();
        workspace.Write("values.json", "{\"" + VariableKey + "\":[\"Iron Ring\"]}");
        workspace.Write(
            "main.filterx",
            "using values.json; using main.fss; {Style test; BaseType %" + VariableKey + "%;}"
        );
        var args = new List<string> { "build", "main.filterx" };
        if (useCommandLine)
        {
            args.Add("--" + VariableKey);
            args.Add("\"Command Ring\"");
        }

        var env = new Dictionary<string, string>();
        if (useEnvironment)
        {
            env["POEFILTERX_" + VariableKey] = "\"Environment Ring\"";
        }

        AssertSuccess(await CliRunner.RunAsync(workspace.Root, [.. args], environment: env));
        var expected = $"Show\n\tBaseType = \"{expectedValue}\"\n\tSetFontSize 40\n\n";
        Assert.That(TestWorkspace.Normalize(workspace.Read("main.filter")), Is.EqualTo(expected));
    }

    [Test]
    public async Task Environment_and_cli_variables_expand_multiple_values_and_chained_placeholders()
    {
        using var workspace = SimpleProject();
        workspace.Write(
            "main.filterx",
            "using main.fss; {Style test; BaseType %PFX_IT_8C71_Indirect%;}"
        );
        var env = new Dictionary<string, string>
        {
            ["POEFILTERX_PFX_IT_8C71_Indirect"] = "%PFX_IT_8C71_Final%"
        };
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "main.filterx", "--PFX_IT_8C71_Final", "\"Iron Ring\" \"Gold Ring\""],
            environment: env
        );
        AssertSuccess(result);
        Assert.That(
            TestWorkspace.Normalize(workspace.Read("main.filter")),
            Is.EqualTo("Show\n\tBaseType = \"Iron Ring\" \"Gold Ring\"\n\tSetFontSize 40\n\n")
        );
    }

    private static void AssertSuccess(CliResult result)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Is.Empty);
        }
    }
}
