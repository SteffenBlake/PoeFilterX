using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Tests.Projects;

namespace PoeFilterX.Engine.Tests.Diagnostics;

[Parallelizable(ParallelScope.All)]
public class DiagnosticReportTests
{
    private static string FilePath(string file) => Path.GetFullPath(Path.Combine("diagnostic-tests", file));

    [Test]
    public void Nested_import_errors_include_each_import_site_with_file_line_and_column()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "# entry\nusing child.filterx;");
        source.Add(FilePath("child.filterx"), "# child\n\nusing styles.fss;");
        source.Add(FilePath("styles.fss"), "test {\n    font-size 40;\n}");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        var report = ProjectDiagnostics.CreateReports(snapshot, snapshot.Diagnostics).Single();
        using var writer = new StringWriter { NewLine = "\n" };
        DiagnosticRenderer.Write(snapshot, snapshot.Diagnostics, writer);
        var output = writer.ToString();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Diagnostic.Code, Is.EqualTo(DiagnosticCodes.MissingToken));
            Assert.That(report.ImportTrace.Select(frame => frame.Path), Is.EqualTo(
            [
                FilePath("child.filterx"), FilePath("main.filterx")
            ]));
            Assert.That(report.ImportTrace.Select(frame => frame.Span.Start), Is.EqualTo([15, 14]));
            Assert.That(output, Does.Contain($"{FilePath("styles.fss")}(2,15): error PFX1003: missing ':' at '40'"));
            Assert.That(output, Does.Contain(" 2 |     font-size 40;\n   |               ^^"));
            Assert.That(output, Does.Contain($"{FilePath("child.filterx")}(3,7): note: imported"));
            Assert.That(output, Does.Contain($"{FilePath("main.filterx")}(2,7): note: imported"));
        }
    }

    [Test]
    public void Renderer_reports_UTF16_columns_and_keeps_the_source_line()
    {
        var snapshot = Load("BaseType \"💎\"; }");
        using var writer = new StringWriter { NewLine = "\n" };
        DiagnosticRenderer.Write(snapshot, snapshot.Diagnostics, writer);
        Assert.That(writer.ToString(), Does.Contain("main.filterx(1,16): error PFX1004:"));
        Assert.That(writer.ToString(), Does.Contain(" 1 | BaseType \"💎\"; }"));
    }

    [Test]
    public void End_of_file_diagnostic_renders_a_caret_on_an_empty_final_line()
    {
        var snapshot = Load("using styles.fss\r\n");
        using var writer = new StringWriter { NewLine = "\n" };
        DiagnosticRenderer.Write(snapshot, snapshot.Diagnostics, writer);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(writer.ToString(), Does.Contain("main.filterx(2,1): error PFX1003:"));
            Assert.That(writer.ToString(), Does.Contain("at '<EOF>'\n 2 | \n   | ^\n"));
        }
    }

    [Test]
    public void Semantic_errors_use_the_same_structured_location_and_import_trace()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using child.filterx;");
        source.Add(FilePath("child.filterx"), "# comment\nItemLevel bananas;");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        var result = ProjectCompiler.Prepare(snapshot);
        Assert.That(result.Value, Is.TypeOf<ParseError>());
        var failure = (ParseError)result.Value!;
        var report = ProjectDiagnostics.CreateReports(snapshot, failure.Diagnostics).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Diagnostic.Code, Is.EqualTo(DiagnosticCodes.InvalidArgument));
            Assert.That(report.Diagnostic.Path, Is.EqualTo(FilePath("child.filterx")));
            Assert.That(report.Diagnostic.Span.Start, Is.EqualTo(10));
            Assert.That(report.ImportTrace, Has.Length.EqualTo(1));
            Assert.That(report.Diagnostic.Message, Does.Not.Contain("FormatException"));
        }
    }

    [Test]
    public void Import_cycles_have_a_bounded_trace_that_ends_at_the_entrypoint()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using child.filterx;");
        source.Add(FilePath("child.filterx"), "using main.filterx;");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        var report = ProjectDiagnostics.CreateReports(snapshot, snapshot.Diagnostics).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Diagnostic.Code, Is.EqualTo("PFX2001"));
            Assert.That(report.ImportTrace, Has.Length.EqualTo(1));
            Assert.That(report.ImportTrace[0].Path, Is.EqualTo(FilePath("main.filterx")));
        }
    }

    [Test]
    public void Multiple_import_routes_choose_a_deterministic_shortest_trace()
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), "using child.filterx;\nusing styles.fss;");
        source.Add(FilePath("child.filterx"), "using styles.fss;");
        source.Add(FilePath("styles.fss"), "test { show true; }");
        var snapshot = new ProjectLoader(source).Load(FilePath("main.filterx"));
        var report = ProjectDiagnostics.CreateReports(snapshot, snapshot.Diagnostics).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ImportTrace, Has.Length.EqualTo(1));
            Assert.That(report.ImportTrace[0].Path, Is.EqualTo(FilePath("main.filterx")));
            Assert.That(report.ImportTrace[0].Span.Start, Is.EqualTo(27));
            Assert.That(source.Reads[FilePath("styles.fss")], Is.EqualTo(1));
        }
    }

    [TestCase("first\r\nsecond\rthird\n", 0, 0, 5)]
    [TestCase("first\r\nsecond\rthird\n", 1, 7, 6)]
    [TestCase("first\r\nsecond\rthird\n", 2, 14, 5)]
    [TestCase("first\r\nsecond\rthird\n", 3, 20, 0)]
    public void Source_line_spans_exclude_newline_sequences(
        string text,
        int line,
        int start,
        int length
    )
    {
        var source = new SourceText("source", text);
        Assert.That(source.GetLineSpan(line), Is.EqualTo(new SourceSpan(start, length)));
    }

    private static ProjectSnapshot Load(string input)
    {
        var source = new MemoryProjectSource();
        source.Add(FilePath("main.filterx"), input);

        return new ProjectLoader(source).Load(FilePath("main.filterx"));
    }
}
