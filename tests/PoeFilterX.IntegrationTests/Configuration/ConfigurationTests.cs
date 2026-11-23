using System.Text.Json;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Configuration;

[Parallelizable(ParallelScope.All)]
public class ConfigurationTests
{
    [Test]
    public async Task Configuration_layers_and_project_discovery_respect_precedence()
    {
        using var project = new TestWorkspace();
        project.Write(".poefilterx.json", """{"entrypoint":"src/base.filterx","output":"base.filter","verbosity":"None"}""");
        project.Write(".poefilterx.dev.json", """{"entrypoint":"src/dev.filterx","output":"dev.filter"}""");
        project.Write("src/base.filterx", "using main.fss; { Class Currency; Style base; }");
        project.Write("src/main.fss", "base { show: true; }");
        project.Write("src/dev.filterx", "using main.fss; { Class Maps; Style base; }");
        var nested = project.PathFor("src/nested");
        Directory.CreateDirectory(nested);
        var baseline = await CliRunner.RunAsync(nested, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(baseline.ExitCode, Is.Zero, baseline.StandardError);
            Assert.That(project.Read("base.filter"), Does.Contain("Currency"));
            Assert.That(baseline.StandardOutput, Is.Empty);
        }

        var profile = await CliRunner.RunAsync(nested, ["--profile", "dev", "build"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.ExitCode, Is.Zero, profile.StandardError);
            Assert.That(project.Read("dev.filter"), Does.Contain("Maps"));
        }

        var environment = new Dictionary<string, string>
        {
            ["POEFILTERX_ENTRYPOINT"] = "src/base.filterx",
            ["POEFILTERX_OUTPUT"] = "environment.filter"
        };
        var env = await CliRunner.RunAsync(nested, ["build", "--profile", "dev"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(env.ExitCode, Is.Zero, env.StandardError);
            Assert.That(project.Read("environment.filter"), Does.Contain("Currency"));
        }

        var cli = await CliRunner.RunAsync(nested,
            ["build", "../dev.filterx", "--profile", "dev", "--output", "cli.filter"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cli.ExitCode, Is.Zero, cli.StandardError);
            Assert.That(File.ReadAllText(Path.Combine(nested, "cli.filter")), Does.Contain("Maps"));
        }
    }

    [TestCase("profile")]
    [TestCase("environment")]
    [TestCase("cli")]
    public async Task Local_packages_work_offline_with_live_edits_and_preserve_release_pins(string layer)
    {
        using var project = new TestWorkspace();
        const string repository = "https://github.com/UserName/my-filter-style";
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        project.Write("entrypoint.filterx", $"import theme from \"{repository}\" at \"{commit}\";\nusing @theme/main.fss;\n{{ Class Currency; Style accent; }}");
        project.Write("local/main.fss", "using nested.fss;");
        project.Write("local/nested.fss", "accent { font-size: 40; }");
        project.Write(".poefilterx.json", "{}");
        var lockText = $$"""
            {"version":1,"packages":[{"alias":"theme","repository":"{{repository}}","requestedCommit":"{{commit}}","commit":"{{commit}}"}]}
            """;
        project.Write("entrypoint.filterx.lock.json", lockText);
        var overrides = JsonSerializer.Serialize(new Dictionary<string, string> { [repository] = project.PathFor("local") });
        var environment = new Dictionary<string, string>
        {
            ["PATH"] = project.PathFor("no-git"),
            ["POEFILTERX_PACKAGE_CACHE"] = project.PathFor("cache")
        };
        string[] arguments = [];
        if (layer == "profile")
        {
            project.Write(".poefilterx.dev.json", "{\"packageOverrides\":" + overrides + "}");
            arguments = ["--profile", "dev"];
        }
        else if (layer == "environment")
        {
            environment["POEFILTERX_PACKAGE_OVERRIDES"] = overrides;
        }
        else
        {
            arguments = ["--package-overrides", overrides];
        }

        var first = await CliRunner.RunAsync(project.Root, [.. arguments, "--locked"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.ExitCode, Is.Zero, first.StandardError);
            Assert.That(project.Read("entrypoint.filter"), Does.Contain("SetFontSize 40"));
        }
        project.Write("local/nested.fss", "accent { font-size: 44; }");
        var second = await CliRunner.RunAsync(project.Root, [.. arguments, "--no-restore"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.ExitCode, Is.Zero, second.StandardError);
            Assert.That(project.Read("entrypoint.filter"), Does.Contain("SetFontSize 44"));
            Assert.That(project.Read("entrypoint.filterx.lock.json"), Is.EqualTo(lockText));
            Assert.That(Directory.Exists(project.PathFor("cache")), Is.False);
        }
    }

    [TestCase("{", "Cannot load project configuration")]
    [TestCase("{\"locked\":\"nonsense\"}", "Cannot load project configuration")]
    [TestCase("{\"verbosity\":\"loud\"}", "Verbosity")]
    [TestCase("{\"packageOverrides\":{\"https://github.com/u/p\":\"missing\"}}", "Invalid package override")]
    public async Task Invalid_configuration_reports_a_diagnostic(string json, string message)
    {
        using var project = new TestWorkspace();
        project.Write(".poefilterx.json", json);
        project.Write("entrypoint.filterx", "{ Class Currency; }");
        var result = await CliRunner.RunAsync(project.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.EqualTo(1));
            Assert.That(result.StandardError, Does.Contain("PFX0001").And.Contain(message));
        }
        Assert.That(result.StandardError, Does.Not.Contain(" at PoeFilterX."));
    }

    [Test]
    public async Task Init_creates_schema_linked_configuration_and_ignored_dev_profile()
    {
        using var project = new TestWorkspace();
        var result = await CliRunner.RunAsync(project.Root, ["init", "--name", "example"]);
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
        foreach (var file in new[] { ".poefilterx.json", ".poefilterx.dev.json" })
        {
            using var json = JsonDocument.Parse(project.Read("example/" + file));
            Assert.That(json.RootElement.GetProperty("$schema").GetString(),
                Is.EqualTo("https://raw.githubusercontent.com/SteffenBlake/PoeFilterX/main/schemas/poefilterx.schema.json"));
        }

        Assert.That(project.Read("example/.gitignore"), Does.Contain(".poefilterx.dev.json"));
        var build = await CliRunner.RunAsync(project.PathFor("example"), ["--profile", "dev"]);
        Assert.That(build.ExitCode, Is.Zero, build.StandardError);
    }

    [Test]
    public async Task Explicit_boolean_arguments_override_configuration_and_locked_mode_still_checks_pins()
    {
        using var project = new TestWorkspace();
        project.Write(".poefilterx.json", """
            {"locked":true,"packageOverrides":{"https://github.com/u/p":"local"}}
            """);
        project.Write("entrypoint.filterx", "import theme from \"https://github.com/u/p\"; using @theme/main.fss; { Style accent; }");
        project.Write("local/main.fss", "accent { show: true; }");
        var locked = await CliRunner.RunAsync(project.Root, []);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(locked.ExitCode, Is.EqualTo(1));
            Assert.That(locked.StandardError, Does.Contain("PFX4002"));
        }
        var overridden = await CliRunner.RunAsync(project.Root, ["--locked", "false"]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(overridden.ExitCode, Is.Zero, overridden.StandardError);
            Assert.That(File.Exists(project.PathFor("entrypoint.filterx.lock.json")), Is.False);
        }
    }

    [Test]
    public async Task Check_reads_item_game_format_and_area_from_configuration()
    {
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", "using main.fss; { Class Currency; Style accent; }");
        project.Write("main.fss", "accent { show: true; }");
        project.Write("copy.txt", Checking.CheckCommandTests.Item);
        project.Write(".poefilterx.json", """
            {"itemFile":"copy.txt","game":"Poe2","format":"Json","areaLevel":30,"verbosity":"None"}
            """);
        var result = await CliRunner.RunAsync(project.Root, ["check"]);
        Assert.That(result.ExitCode, Is.Zero, result.StandardError);
        using var json = JsonDocument.Parse(result.StandardOutput);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.RootElement.GetProperty("result").GetProperty("areaLevel").GetInt32(), Is.EqualTo(30));
            Assert.That(json.RootElement.GetProperty("result").GetProperty("item").GetProperty("game").GetString(), Is.EqualTo("Poe2"));
        }
    }

    [TestCase(".poefilterx.json", "Currency", "base.filter")]
    [TestCase(".poefilterx.dev.json", "Maps", "dev.filter")]
    public async Task Configuration_files_can_be_selected_as_project_inputs(
        string selection,
        string expectedClass,
        string output
    )
    {
        using var project = new TestWorkspace();
        project.Write(".poefilterx.json", """{"entrypoint":"base.filterx","output":"base.filter"}""");
        project.Write(".poefilterx.dev.json", """{"entrypoint":"dev.filterx","output":"dev.filter"}""");
        project.Write("base.filterx", "using main.fss; { Class Currency; Style base; }");
        project.Write("dev.filterx", "using main.fss; { Class Maps; Style base; }");
        project.Write("main.fss", "base { show: true; }");
        var result = await CliRunner.RunAsync(project.Root, ["build", selection]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero, result.StandardError);
            Assert.That(project.Read(output), Does.Contain(expectedClass));
        }
    }
}
