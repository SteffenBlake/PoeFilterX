using Antlr4.Runtime;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Parsing.Generated;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Tests.Parsing;

namespace PoeFilterX.Engine.Tests.Diagnostics;

[Parallelizable(ParallelScope.All)]
public class ParserDiagnosticTests
{
    [TestCase(false, "using main.fss", DiagnosticCodes.MissingToken, 14, 0, "';'")]
    [TestCase(true, "test { font-size 40; }", DiagnosticCodes.MissingToken, 17, 2, "':'")]
    [TestCase(false, "}", DiagnosticCodes.UnexpectedToken, 0, 1, "EOF")]
    [TestCase(false, "[ }", DiagnosticCodes.InputMismatch, 2, 1, "']'")]
    [TestCase(false, "{", DiagnosticCodes.InputMismatch, 1, 0, "'}'")]
    public void Recognition_events_have_codes_exact_spans_and_expected_tokens(
        bool stylesheet,
        string input,
        string code,
        int offset,
        int length,
        string expected
    )
    {
        var tree = Parse(stylesheet, input);
        var error = tree.Diagnostics.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(error.Path, Is.EqualTo("source"));
            Assert.That(error.Span, Is.EqualTo(new SourceSpan(offset, length)));
            Assert.That(error.ExpectedTokens, Does.Contain(expected));
            Assert.That(error.RuleStack, Does.Contain("document"));
        }
    }

    [TestCase(false, "BaseType \"bad\\q\";", DiagnosticCodes.InvalidEscape, 9, 7)]
    [TestCase(true, "test { alert-path: \"bad\\q\"; }", DiagnosticCodes.InvalidEscape, 19, 7)]
    [TestCase(false, "BaseType \"open", DiagnosticCodes.UnterminatedString, 9, 5)]
    [TestCase(true, "test { alert-path: \"open", DiagnosticCodes.UnterminatedString, 19, 5)]
    [TestCase(true, "/* open", DiagnosticCodes.UnterminatedComment, 0, 7)]
    [TestCase(false, "\\", DiagnosticCodes.InvalidToken, 0, 1)]
    public void Malformed_tokens_are_diagnosed_at_the_token_without_inspecting_other_errors(
        bool stylesheet,
        string input,
        string code,
        int offset,
        int length
    )
    {
        var error = Parse(stylesheet, input).Diagnostics.Single(diagnostic => diagnostic.Code == code);
        Assert.That(error.Span, Is.EqualTo(new SourceSpan(offset, length)));
    }

    [TestCase(false, "using main.fss")]
    [TestCase(false, "[ }")]
    [TestCase(false, "BaseType \"bad\\q\";")]
    [TestCase(true, "test { font-size 40; }")]
    [TestCase(true, "test {")]
    public void Parser_messages_are_the_unmodified_ANTLR_messages(bool stylesheet, string input)
    {
        var native = new NativeErrorListener();
        var stream = new AntlrInputStream(input);
        if (stylesheet)
        {
            var parser = new FssParser(new CommonTokenStream(new FssLexer(stream)));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(native);
            parser.document();
        }
        else
        {
            var parser = new FilterXParser(new CommonTokenStream(new FilterXLexer(stream)));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(native);
            parser.document();
        }

        var actual = Parse(stylesheet, input).Diagnostics
            .Where(diagnostic => !diagnostic.RuleStack.IsEmpty)
            .Select(diagnostic => diagnostic.Message);
        Assert.That(actual, Is.EqualTo(native.Messages));
    }

    [Test]
    public void Recovery_preserves_multiple_errors_and_following_styles()
    {
        var tree = Parse(true, "one { font-size 40; }\ntwo { show true; }");
        var errors = tree.Diagnostics;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Root.Statements, Has.Length.EqualTo(2));
            Assert.That(errors, Has.Length.EqualTo(2));
            Assert.That(errors.Select(error => error.Code), Is.All.EqualTo(DiagnosticCodes.MissingToken));
            Assert.That(errors.Select(error => tree.Source.GetPosition(error.Span.Start).Line), Is.EqualTo([0, 1]));
            Assert.That(errors.All(error => error.RuleStack.Contains("declaration")), Is.True);
        }
    }

    [Test]
    public void Unrelated_bad_escape_does_not_change_an_unexpected_delimiter_diagnostic()
    {
        const string input = "Style test;\n]\nBaseType \"bad\\q\";";
        var errors = Parse(false, input).Diagnostics;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Some.Matches<SyntaxDiagnostic>(error =>
                error.Code == DiagnosticCodes.UnexpectedToken && error.Span == new SourceSpan(12, 1)));
            Assert.That(errors, Has.Some.Matches<SyntaxDiagnostic>(error =>
                error.Code == DiagnosticCodes.InvalidEscape && error.Span.Start == 23));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Resource_limit_has_its_own_code_and_is_not_reported_as_a_recognition_error(bool stylesheet)
    {
        var input = new string('{', 2000) + new string('}', 2000);
        var tree = Parse(stylesheet, input);
        Assert.That(tree.Diagnostics, Has.Some.Matches<SyntaxDiagnostic>(diagnostic =>
            diagnostic.Code == DiagnosticCodes.NestingLimit));
    }

    private static SyntaxTree Parse(bool stylesheet, string input)
    {
        using var reader = new StringReader(input);

        return ParsedSyntaxTree.From(stylesheet
            ? FssParserService.Parse(reader, "source")
            : FilterXParserService.Parse(reader, "source"));
    }
}
