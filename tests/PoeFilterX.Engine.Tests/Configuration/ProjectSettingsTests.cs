using System.Text.Json;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Packages;
using PoeFilterX.Engine.Projects;

namespace PoeFilterX.Engine.Tests.Configuration;

[Parallelizable(ParallelScope.All)]
public class ProjectSettingsTests
{
    [Test]
    public async Task Engine_consumers_load_local_overrides_without_configuration_plumbing()
    {
        var root = Directory.CreateTempSubdirectory("poefilterx-configuration-").FullName;
        try
        {
            const string repository = "https://github.com/u/theme";
            var local = Path.Combine(root, "checkout");
            Directory.CreateDirectory(local);
            var entrypoint = Path.Combine(root, "entrypoint.filterx");
            File.WriteAllText(entrypoint, $"import theme from \"{repository}\"; using @theme/main.fss; {{ Class Currency; Style accent; }}");
            File.WriteAllText(Path.Combine(local, "main.fss"), "accent { font-size: 42; }");
            File.WriteAllText(Path.Combine(root, ".poefilterx.json"), JsonSerializer.Serialize(new
            {
                packageOverrides = new Dictionary<string, string> { [repository] = "checkout" }
            }));

            var snapshot = new ProjectLoader(new FileProjectSource()).Load(entrypoint);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(snapshot.Diagnostics, Is.Empty);
                Assert.That(snapshot.Packages.References[0].Root, Is.EqualTo(local));
            }
            var prepared = ProjectCompiler.Prepare(snapshot);
            Assert.That(prepared.Value, Is.TypeOf<PreparedProject>());
            using var output = new StringWriter();
            var written = ProjectCompiler.Write((PreparedProject)prepared.Value!, output);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(written.Value, Is.TypeOf<int>());
                Assert.That(output.ToString(), Does.Contain("SetFontSize 42"));
            }
            var restored = await PackageRestorer.RestoreAsync(entrypoint);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(restored.Value, Is.TypeOf<PackageCatalog>());
                Assert.That(File.Exists(entrypoint + ".lock.json"), Is.False);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Invalid_configuration_keeps_source_trees_available_for_editor_diagnostics()
    {
        var root = Directory.CreateTempSubdirectory("poefilterx-configuration-").FullName;
        try
        {
            var entrypoint = Path.Combine(root, "entrypoint.filterx");
            File.WriteAllText(entrypoint, "{ Class Currency; }");
            File.WriteAllText(Path.Combine(root, ".poefilterx.json"), "{");
            var snapshot = new ProjectLoader(new FileProjectSource()).Load(entrypoint);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(snapshot.Diagnostics, Is.Not.Empty);
                Assert.That(snapshot.SyntaxTrees.ContainsKey(entrypoint), Is.True);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
