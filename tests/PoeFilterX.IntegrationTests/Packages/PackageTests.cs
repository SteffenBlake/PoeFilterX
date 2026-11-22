using System.Text.Json;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Packages;

[Parallelizable(ParallelScope.All)]
public class PackageTests
{
    private static Dictionary<string, string> EnvironmentFor(TestWorkspace workspace)
    {
        return new() { ["POEFILTERX_PACKAGE_CACHE"] = workspace.PathFor("cache") };
    }

    [Test]
    public async Task Restore_locks_HEAD_builds_transitive_package_files_and_reuses_cache_without_Git()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("styles/main.fss", "using nested.fss; base { show: true; }");
        repository.Write("styles/nested.fss", "accent { font-size: 40; }");
        var commit = await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import theme from \"{repository.Url}\";\nusing @theme/styles/main.fss;\n{{ Class Currency; Style base accent; }}");
        var environment = EnvironmentFor(project);
        var restored = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        Assert.That(restored.ExitCode, Is.Zero, restored.StandardError);
        using var locked = JsonDocument.Parse(project.Read("entrypoint.filterx.lock.json"));
        Assert.That(locked.RootElement.GetProperty("packages")[0].GetProperty("commit").GetString(), Is.EqualTo(commit));
        var lockText = project.Read("entrypoint.filterx.lock.json");
        environment["PATH"] = project.PathFor("no-git");
        var built = await CliRunner.RunAsync(project.Root, ["--locked"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(built.ExitCode, Is.Zero, built.StandardError);
            Assert.That(project.Read("entrypoint.filter"), Does.Contain("SetFontSize 40"));
            Assert.That(project.Read("entrypoint.filterx.lock.json"), Is.EqualTo(lockText));
        }
    }

    [Test]
    public async Task Update_refreshes_unpinned_packages_and_leaves_explicit_pins_fixed()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("main.fss", "first { show: true; }");
        var first = await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import floating from \"{repository.Url}\";\nimport pinned from \"{repository.Url}\" at \"{first}\";");
        var environment = EnvironmentFor(project);
        var initial = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        Assert.That(initial.ExitCode, Is.Zero, initial.StandardError);
        var initialLock = project.Read("entrypoint.filterx.lock.json");
        repository.Write("main.fss", "second { show: true; }");
        var second = await repository.CommitAsync();
        var ordinary = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ordinary.ExitCode, Is.Zero, ordinary.StandardError);
            Assert.That(project.Read("entrypoint.filterx.lock.json"), Is.EqualTo(initialLock));
        }

        var updated = await CliRunner.RunAsync(project.Root, ["restore", "--update"], environment: environment);
        Assert.That(updated.ExitCode, Is.Zero, updated.StandardError);
        using var locked = JsonDocument.Parse(project.Read("entrypoint.filterx.lock.json"));
        var entries = locked.RootElement.GetProperty("packages");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].GetProperty("commit").GetString(), Is.EqualTo(second));
            Assert.That(entries[1].GetProperty("commit").GetString(), Is.EqualTo(first));
        }

        project.Write("entrypoint.filterx", "{ Class Currency; }");
        environment["PATH"] = project.PathFor("no-git");
        var stale = await CliRunner.RunAsync(project.Root, ["restore", "--locked"], environment: environment);
        var pruned = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        using var empty = JsonDocument.Parse(project.Read("entrypoint.filterx.lock.json"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stale.ExitCode, Is.EqualTo(1));
            Assert.That(stale.StandardError, Does.Contain("PFX4002"));
            Assert.That(pruned.ExitCode, Is.Zero, pruned.StandardError);
            Assert.That(empty.RootElement.GetProperty("packages").GetArrayLength(), Is.Zero);
        }
    }

    [Test]
    public async Task Projects_without_imports_do_not_require_Git_or_create_cache()
    {
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", "{ Class Currency; }");
        var environment = EnvironmentFor(project);
        environment["PATH"] = project.PathFor("no-git");
        var restored = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        var built = await CliRunner.RunAsync(project.Root, [], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.ExitCode, Is.Zero, restored.StandardError);
            Assert.That(built.ExitCode, Is.Zero, built.StandardError);
            Assert.That(Directory.Exists(project.PathFor("cache")), Is.False);
            Assert.That(File.Exists(project.PathFor("entrypoint.filterx.lock.json")), Is.False);
        }
    }

    [Test]
    public async Task Locked_restore_requires_a_matching_lock_and_no_restore_never_downloads()
    {
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", "import theme from \"https://packages.invalid/theme.git\"; using @theme/main.fss; { Style accent; }");
        var environment = EnvironmentFor(project);
        environment["PATH"] = project.PathFor("no-git");
        var locked = await CliRunner.RunAsync(project.Root, ["restore", "--locked"], environment: environment);
        var offline = await CliRunner.RunAsync(project.Root, ["--no-restore"], environment: environment);
        var missingGit = await CliRunner.RunAsync(project.Root, ["restore"], environment: environment);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(locked.ExitCode, Is.EqualTo(1));
            Assert.That(locked.StandardError, Does.Contain("PFX4002"));
            Assert.That(offline.ExitCode, Is.EqualTo(1));
            Assert.That(offline.StandardError, Does.Contain("PFX4003").And.Not.Contain("Undeclared"));
            Assert.That(missingGit.ExitCode, Is.EqualTo(1));
            Assert.That(missingGit.StandardError, Does.Contain("Git is required"));
        }
    }

    [TestCase("using ../outside.fss;", "PFX4005")]
    [TestCase("import nested from \"https://packages.invalid/hidden.git\";", "PFX4006")]
    public async Task Package_files_cannot_escape_or_cascade_imports(string content, string code)
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("main.filterx", content);
        await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import theme from \"{repository.Url}\"; using @theme/main.filterx;");
        var built = await CliRunner.RunAsync(project.Root, [], environment: EnvironmentFor(project));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(built.ExitCode, Is.EqualTo(1));
            Assert.That(built.StandardError, Does.Contain(code).And.Contain("note: imported"));
        }
    }
    [Test]
    public async Task Submodules_are_never_initialized_and_restore_does_not_compile_package_sources()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("main.filterx", "deliberately invalid filter syntax");
        var first = await repository.CommitAsync();
        repository.AddSubmodule(first);
        await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import theme from \"{repository.Url}\"; using @theme/main.filterx;");
        var restored = await CliRunner.RunAsync(project.Root, ["restore"], environment: EnvironmentFor(project));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.ExitCode, Is.Zero, restored.StandardError);
            Assert.That(Directory.EnumerateDirectories(project.PathFor("cache"), "blocked", SearchOption.AllDirectories).Count(), Is.EqualTo(1));
            Assert.That(Directory.EnumerateFiles(project.PathFor("cache"), "*", SearchOption.AllDirectories)
                .Any(path => path.Contains(Path.DirectorySeparatorChar + "blocked" + Path.DirectorySeparatorChar, StringComparison.Ordinal)), Is.False);
        }
    }

    [Test]
    [Platform(Exclude = "Win")]
    public async Task Symlinks_are_rejected_and_incomplete_snapshots_are_removed()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.WriteLink("escape.fss", "../../outside.fss");
        await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import theme from \"{repository.Url}\";");
        var restored = await CliRunner.RunAsync(project.Root, ["restore"], environment: EnvironmentFor(project));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored.ExitCode, Is.EqualTo(1));
            Assert.That(restored.StandardError, Does.Contain("PFX4004").And.Contain("symbolic links"));
            Assert.That(Directory.EnumerateFiles(project.PathFor("cache"), "complete", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.EnumerateDirectories(project.PathFor("cache"), "*.tmp", SearchOption.AllDirectories), Is.Empty);
            Assert.That(File.Exists(project.PathFor("entrypoint.filterx.lock.json")), Is.False);
        }
    }

    [Test]
    public async Task Concurrent_restores_publish_one_complete_snapshot_and_usable_lockfile()
    {
        await using var repository = new GitPackageRepository();
        await repository.InitializeAsync();
        repository.Write("main.fss", "accent { font-size: 40; }");
        var commit = await repository.CommitAsync();
        using var project = new TestWorkspace();
        project.Write("entrypoint.filterx", $"import theme from \"{repository.Url}\" at \"{commit}\";");
        var environment = EnvironmentFor(project);
        var restores = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            CliRunner.RunAsync(project.Root, ["restore"], environment: environment)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restores.Select(result => result.ExitCode), Is.All.Zero);
            Assert.That(Directory.EnumerateFiles(project.PathFor("cache"), "complete", SearchOption.AllDirectories).Count(), Is.EqualTo(1));
            Assert.That(Directory.EnumerateDirectories(project.PathFor("cache"), "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }
    }

}
