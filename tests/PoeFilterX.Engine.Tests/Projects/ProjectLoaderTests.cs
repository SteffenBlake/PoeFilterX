using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Tests.Projects;

[Parallelizable(ParallelScope.All)]
public class ProjectLoaderTests
{
    private static string Root => Path.GetFullPath("test-project");

    private static string FilePath(string relative) => Path.Combine(Root, relative);

    [Test]
    public void File_source_loads_a_project_and_releases_its_file_handles()
    {
        var root = Path.Combine(Path.GetTempPath(), "PoeFilterX " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "main ü.filterx");
        try
        {
            File.WriteAllText(path, "using main.fss; Style test;");
            File.WriteAllText(Path.Combine(root, "main.fss"), "test {font-size:40;}");
            var snapshot = new ProjectLoader(new FileProjectSource()).Load(path);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(snapshot.Diagnostics, Is.Empty);
                Assert.That(snapshot.SyntaxTrees, Has.Count.EqualTo(2));
            }
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.That(exclusive.CanWrite, Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestCase("using valid.fss;}", true)]
    [TestCase("using valid.fss", false)]
    public void Incomplete_imports_are_skipped_but_adjacent_errors_do_not_hide_valid_imports(
        string text,
        bool expectedRead
    )
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), text);
        source.Add(FilePath("valid.fss"), "");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics, Is.Not.Empty);
            Assert.That(source.Reads.ContainsKey(FilePath("valid.fss")), Is.EqualTo(expectedRead));
        }
    }

    [Test]
    public void Shared_files_parse_once_while_every_import_occurrence_is_retained()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using a.filterx; using b.filterx; using a.filterx;");
        source.Add(FilePath("a.filterx"), "using shared.fss; Style a;");
        source.Add(FilePath("b.filterx"), "using shared.fss; Style b;");
        source.Add(FilePath("shared.fss"), "a {font-size:40;} b {font-size:30;}");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics, Is.Empty);
            Assert.That(snapshot.SyntaxTrees, Has.Count.EqualTo(4));
            Assert.That(source.Reads.Values, Is.All.EqualTo(1));
            Assert.That(snapshot.Imports.Select(import => Path.GetFileName(import.TargetPath)),
                Is.EqualTo(["a.filterx", "shared.fss", "b.filterx", "shared.fss", "a.filterx"]));
        }
    }

    [TestCase("main.filterx", "main.filterx")]
    [TestCase("main.fss", "main.fss")]
    public void Cycles_report_the_importing_file_and_path_span(string entry, string target)
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath(entry), "using child" + Path.GetExtension(entry) + ";");
        var child = FilePath("child" + Path.GetExtension(entry));
        source.Add(child, $"using {target};");
        var snapshot = new ProjectLoader(source).Load(FilePath(entry));
        var error = snapshot.Diagnostics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Code, Is.EqualTo("PFX2001"));
            Assert.That(error.Path, Is.EqualTo(child));
            Assert.That(snapshot.SyntaxTrees[child].Source.Slice(error.Span).ToString(), Is.EqualTo(target));
        }
    }

    [Test]
    public void Imports_resolve_from_the_importing_directory()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using sub/child.filterx;");
        source.Add(FilePath("sub/child.filterx"), "using ../shared.fss;");
        source.Add(FilePath("shared.fss"), "");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics, Is.Empty);
            Assert.That(snapshot.SyntaxTrees, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void Variables_remain_separate_from_the_original_syntax_tree()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using values.json; BaseType %Gear%;");
        source.Add(FilePath("values.json"), "{\"Gear\":[\"Iron Ring\",\"Gold Ring\"]}");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics, Is.Empty);
            Assert.That(snapshot.VariableFiles[FilePath("values.json")]["Gear"],
                Is.EqualTo(["Iron Ring", "Gold Ring"]));
        }
        var command = (CommandSyntax)snapshot.SyntaxTrees[FilePath("main.filterx")].Root.Statements[1];
        Assert.That(command.Arguments[0].Value, Is.EqualTo("%Gear%"));
    }

    [TestCase("bad json")]
    [TestCase("{\"Gear\":1}")]
    [TestCase("{\"Gear\":[null]}")]
    public void Invalid_variable_files_produce_diagnostics(string contents)
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using values.json;");
        source.Add(FilePath("values.json"), contents);
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("PFX2006"));
    }

    [Test]
    public void Missing_file_does_not_prevent_loading_later_imports()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using missing.fss; using valid.fss;");
        source.Add(FilePath("valid.fss"), "test {font-size:40;}");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("PFX2003"));
            Assert.That(snapshot.SyntaxTrees.ContainsKey(FilePath("valid.fss")), Is.True);
        }
    }

    [TestCase("using child.json;")]
    [TestCase("using child.filterx;")]
    public void Stylesheets_reject_non_stylesheet_imports(string input)
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.fss"), input);
        var snapshot = new ProjectLoader(source).Load(FilePath("main.fss"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("PFX2004"));
            Assert.That(source.Reads, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Nested_imports_are_reported_without_opening_the_target()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "[{using child.fss;}]");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("PFX2005"));
            Assert.That(source.Reads, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Reusing_loader_observes_updated_source_without_mutating_the_previous_snapshot()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "Style old;");
        var loader = new ProjectLoader(source);
        var first = loader.Load(FilePath("main.filterx"));
        source.Add(FilePath("main.filterx"), "Style edited;");
        var second = loader.Load(FilePath("main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.SyntaxTrees[FilePath("main.filterx")].Source.Text, Is.EqualTo("Style old;"));
            Assert.That(second.SyntaxTrees[FilePath("main.filterx")].Source.Text, Is.EqualTo("Style edited;"));
        }
    }

    [Test]
    public void Cancellation_is_propagated_instead_of_becoming_a_diagnostic()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ProjectLoader(new MemoryProjectSource()).Load(
            FilePath("main.filterx"),
            cancellationToken: cancellation.Token
        ));
    }
}
