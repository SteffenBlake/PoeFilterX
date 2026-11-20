using System.Text;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Tests.Parsing;

[Parallelizable(ParallelScope.All)]
public class ParserTests
{
    [Test]
    public void Large_file_builds_all_blocks_and_preserves_the_last_source_location()
    {
        const string block = "{ ItemLevel >= 80; Style rare; }\n";
        var input = string.Concat(Enumerable.Repeat(block, 10_000));
        var tree = Filter(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(tree.Root.Statements, Has.Length.EqualTo(10_000));
        }
        Assert.That(tree.Root.Statements[^1].Span.Start, Is.EqualTo(9_999 * block.Length));
        AssertRoundTrip(tree);
    }

    [Test]
    public void Excessive_nesting_returns_a_diagnostic_instead_of_overflowing_the_stack()
    {
        var input = new string('{', 2_000) + new string('}', 2_000);
        var tree = Filter(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics.Any(error => error.Message.Contains("nesting")), Is.True);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
        }
    }

    public static IEnumerable<string> SyntaxExamples => Directory.EnumerateFiles(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures"),
        "*",
        SearchOption.AllDirectories
    );

    [TestCaseSource(nameof(SyntaxExamples))]
    public void Existing_language_examples_parse_without_syntax_errors(string path)
    {
        using var stream = File.OpenRead(path);
        var tree = ParsedSyntaxTree.From(Path.GetExtension(path) == ".fss"
            ? FssParserService.Parse(stream, path)
            : FilterXParserService.Parse(stream, path));
        Assert.That(tree.Diagnostics, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Arbitrary_incomplete_edits_do_not_throw_or_lose_source(bool stylesheet)
    {
        var random = new Random(732);
        const string alphabet = "a;{}[]\"\\/#*:%! \r\n";
        for (var sample = 0; sample < 300; sample++)
        {
            var input = new string([.. Enumerable.Range(0, random.Next(100)).Select(_ => alphabet[random.Next(alphabet.Length)])]);
            var tree = stylesheet ? Styles(input) : Filter(input);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
            AssertRoundTrip(tree);
        }
    }

    [Test]
    public void Filter_preserves_structure_values_and_source()
    {
        const string input = "using main.fss; # comment\r\n[ Style hidden; { BaseType == \"a;{}#😀\" %Items%; } ]";
        var tree = Filter(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
            Assert.That(tree.Root.Statements[0], Is.TypeOf<ImportSyntax>());
        }
        var parent = (FilterBlockSyntax)tree.Root.Statements[1];
        Assert.That(parent.IsAbstract, Is.True);
        var child = (FilterBlockSyntax)parent.Statements[1];
        var command = (CommandSyntax)child.Statements[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Name, Is.EqualTo("BaseType"));
            Assert.That(
                command.Arguments.Select(arg => arg.Value),
                Is.EqualTo(["==", "a;{}#😀", "%Items%"])
            );
            Assert.That(tree.Source.Slice(command.NameSpan).ToString(), Is.EqualTo("BaseType"));
            Assert.That(tree.Tokens.Any(token => token.Kind == "COMMENT" && token.IsTrivia), Is.True);
        }
        AssertRoundTrip(tree);
    }

    [Test]
    public void Styles_preserve_comments_paths_and_declarations()
    {
        const string input = "using @pack/common.fss; /* ; } */ rare/*name*/{font-size:40; alert-path: \"a;😀.wav\";}";
        var tree = Styles(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(((ImportSyntax)tree.Root.Statements[0]).Path.Value, Is.EqualTo("@pack/common.fss"));
        }
        var style = (StyleSyntax)tree.Root.Statements[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(style.Name, Is.EqualTo("rare"));
            Assert.That(
                style.Declarations.Select(command => command.Name),
                Is.EqualTo(["font-size", "alert-path"])
            );
            Assert.That(style.Declarations[1].Arguments[0].Value, Is.EqualTo("a;😀.wav"));
        }
        AssertRoundTrip(tree);
    }

    [TestCase(false, "{ Identified True }")]
    [TestCase(false, "[ { Rarity Rare; } }")]
    [TestCase(false, "BaseType \"unclosed")]
    [TestCase(false, "BaseType \"bad\\q\";")]
    [TestCase(false, "using ;")]
    [TestCase(false, "}")]
    [TestCase(true, "rare { font-size: 40 }")]
    [TestCase(true, "rare { font-size: 40;")]
    [TestCase(true, "/* unclosed")]
    [TestCase(true, "rare { alert-path: \"unclosed")]
    public void Malformed_input_returns_diagnostics_and_preserves_text(bool stylesheet, string input)
    {
        var tree = stylesheet ? Styles(input) : Filter(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Not.Empty);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
        }
        Assert.That(tree.Diagnostics.All(error => error.Span.End <= input.Length), Is.True);
        AssertRoundTrip(tree);
    }

    [Test]
    public void Missing_semicolon_recovers_and_keeps_following_command()
    {
        var tree = Filter("{ Identified True } { Rarity Rare; }");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Not.Empty);
            Assert.That(tree.Root.Statements.OfType<FilterBlockSyntax>().Last().Statements.OfType<CommandSyntax>()
                .Single().Name, Is.EqualTo("Rarity"));
        }
    }

    [Test]
    public void Locations_use_utf16_and_handle_crlf()
    {
        var tree = Filter("BaseType \"😀\";\r\n  Rarity Rare;");
        var command = (CommandSyntax)tree.Root.Statements[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.NameSpan.Start, Is.EqualTo(18));
            Assert.That(tree.Source.GetPosition(command.NameSpan.Start), Is.EqualTo(new SourcePosition(1, 2)));
            Assert.That(tree.Source.GetPosition(12), Is.EqualTo(new SourcePosition(0, 12)));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Reader_is_consumed_progressively_and_left_open(bool stylesheet)
    {
        const string input = "using shared.fss;";
        using var reader = new ForwardOnlyReader(input);
        var tree = ParsedSyntaxTree.From(stylesheet
            ? FssParserService.Parse(reader, "main.fss")
            : FilterXParserService.Parse(reader, "main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
            Assert.That(reader.Disposed, Is.False);
            Assert.That(reader.ReadCalls, Is.GreaterThan(input.Length));
        }
    }

    [Test]
    public void Stream_handles_one_byte_reads_bom_and_unicode_and_stays_open()
    {
        const string input = "BaseType \"😀ü\";";
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(input)];
        using var stream = new ChunkedReadStream(bytes);
        var tree = ParsedSyntaxTree.From(FilterXParserService.Parse(stream, "main.filterx"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(tree.Source.Text, Is.EqualTo(input));
            Assert.That(stream.CanRead, Is.True);
        }
    }

    [Test]
    public void Cancellation_interrupts_a_read_in_progress()
    {
        using var cancellation = new CancellationTokenSource();
        using var reader = new ForwardOnlyReader("BaseType \"very long value\";", cancellation);
        Assert.Throws<OperationCanceledException>(() =>
            FilterXParserService.Parse(reader, "main.filterx", cancellation.Token));
    }

    [TestCase("\r")]
    [TestCase("\n")]
    [TestCase("\r\n")]
    public void Source_text_understands_line_endings(string newline)
    {
        var source = new SourceText("file", "a" + newline + "b");
        Assert.That(source.GetPosition(source.Text.Length - 1), Is.EqualTo(new SourcePosition(1, 0)));
    }

    private static SyntaxTree Filter(string input)
    {
        using var reader = new StringReader(input);

        return ParsedSyntaxTree.From(FilterXParserService.Parse(reader, "main.filterx"));
    }

    private static SyntaxTree Styles(string input)
    {
        using var reader = new StringReader(input);

        return ParsedSyntaxTree.From(FssParserService.Parse(reader, "main.fss"));
    }

    private static void AssertRoundTrip(SyntaxTree tree)
    {
        var text = string.Concat(tree.Tokens.Select(token => tree.Source.Slice(token.Span).ToString()));
        Assert.That(text, Is.EqualTo(tree.Source.Text));
    }
}
