using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Tests.Projects;

namespace PoeFilterX.Engine.Tests.Results;

[Parallelizable(ParallelScope.All)]
public class CompilerResultTests
{
    [TestCase("ItemLevel bananas;", DiagnosticCodes.InvalidArgument)]
    [TestCase("Corrupted maybe;", DiagnosticCodes.InvalidArgument)]
    [TestCase("NotACommand true;", DiagnosticCodes.UnknownOperation)]
    [TestCase("ItemLevel 1 2;", DiagnosticCodes.InvalidArgumentCount)]
    [TestCase("{", DiagnosticCodes.InputMismatch)]
    public void Invalid_filter_code_returns_a_located_ParseError(string text, string code)
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("result-tests/main.filterx");
        source.Add(path, text);
        var project = new ProjectLoader(source).Load(path);
        var result = ProjectCompiler.Prepare(project);
        Assert.That(result is ParseError error && error.Diagnostics.Any(
            diagnostic => diagnostic.Code == code && diagnostic.Path == path
        ), Is.True);
    }

    [TestCase("font-size", "wrong")]
    [TestCase("border-color", "0xINVALID")]
    [TestCase("icon", "2", "InvalidColor")]
    [TestCase("alert", "1", "300", "wrong")]
    public void Invalid_style_arguments_return_ParseError(string name, params string[] arguments)
    {
        var result = StyleOperationsCodec.Parse(name, arguments);
        Assert.That(result.Value, Is.TypeOf<ParseError>());
    }

    [TestCase("[]")]
    [TestCase("{\"value\": null}")]
    [TestCase("{\"value\": 1}")]
    [TestCase("{\"value\": [1]}")]
    [TestCase("{")]
    public void Invalid_variable_documents_reach_the_compiler_as_ParseError(string text)
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("result-tests/main.filterx");
        source.Add(path, "using variables.json;");
        source.Add(Path.GetFullPath("result-tests/variables.json"), text);
        var project = new ProjectLoader(source).Load(path);
        var result = ProjectCompiler.Prepare(project);
        Assert.That(result is ParseError error && error.Diagnostics.Single().Code == "PFX2006", Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Syntax_failures_return_ParseError_with_a_recoverable_tree_for_editors(bool stylesheet)
    {
        const string input = "{";
        using var reader = new StringReader(input);
        var result = stylesheet
            ? FssParserService.Parse(reader, "source.fss")
            : FilterXParserService.Parse(reader, "source.filterx");
        Assert.That(result.Value, Is.TypeOf<ParseError>());
        var error = (ParseError)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Diagnostics, Is.Not.Empty);
            Assert.That(error.PartialTree?.Source.Text, Is.EqualTo(input));
            Assert.That(error.PartialTree?.Diagnostics, Is.EqualTo(error.Diagnostics));
        }
    }

    [Test]
    public void Undeclared_styles_return_a_ParseError_from_the_writer()
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("result-tests/main.filterx");
        source.Add(path, "Style missing;");
        var snapshot = new ProjectLoader(source).Load(path);
        var result = ProjectCompiler.Prepare(snapshot).FlatMap(project =>
        {
            using var output = new StringWriter();

            return ProjectCompiler.Write(project, output);
        });
        Assert.That(result is ParseError error
            && error.Diagnostics.Single().Code == DiagnosticCodes.UndeclaredStyle, Is.True);
    }
}
