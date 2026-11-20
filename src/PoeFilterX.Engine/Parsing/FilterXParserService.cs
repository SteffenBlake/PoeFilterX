using System.Text;
using Antlr4.Runtime;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Parsing.Generated;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Parsing;

public static class FilterXParserService
{
    /// <summary>Parses progressively from the current stream position. Leaves the stream open.</summary>
    public static Result<SyntaxTree> Parse(
        Stream stream,
        string path,
        CancellationToken cancellationToken = default
    )
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);

        return Parse(reader, path, cancellationToken);
    }

    /// <summary>Leaves the reader open. Source text is retained for diagnostics and editor tooling.</summary>
    public static Result<SyntaxTree> Parse(
        TextReader reader,
        string path,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = new UnbufferedCharStream(new CancellationTextReader(reader, cancellationToken));
        var lexer = new FilterXLexer(input) { TokenFactory = new CommonTokenFactory(copyText: true) };
        lexer.RemoveErrorListeners();
        var guarded = new GuardedTokenSource(
            lexer,
            FilterXLexer.OPEN_BRACE,
            FilterXLexer.CLOSE_BRACE,
            FilterXLexer.OPEN_BRACKET,
            FilterXLexer.CLOSE_BRACKET
        );
        var tokens = new CommonTokenStream(guarded);
        var parser = new FilterXParser(tokens)
        {
            ErrorHandler = new SyntaxErrorStrategy()
        };
        var diagnostics = new ParserDiagnostics();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(diagnostics);
        var document = parser.document();
        tokens.Fill();
        if (guarded.LimitToken is { } limit)
        {
            diagnostics.Entries.Add(new ParserDiagnostics.Entry(
                limit, DiagnosticCodes.NestingLimit, "Maximum block nesting of 128 exceeded.", [], []
            ));
        }

        var builder = new SyntaxTreeBuilder(path, tokens, lexer, diagnostics);

        var tree = builder.Build([.. document.statement().Select(statement => BuildStatement(statement, builder))]);

        return tree.Diagnostics.IsEmpty ? tree : new ParseError(tree.Diagnostics, tree);
    }

    private static SyntaxNode BuildStatement(FilterXParser.StatementContext statement, SyntaxTreeBuilder builder)
    {
        if (statement.importStatement() is { } import)
        {
            return new ImportSyntax(builder.Span(import), builder.Argument(import.argument()));
        }

        if (statement.block() is { } block)
        {
            return new FilterBlockSyntax(
                builder.Span(block),
                block.OPEN_BRACKET() != null,
                [.. block.statement().Select(child => BuildStatement(child, builder))]
            );
        }

        var command = statement.command();

        return new CommandSyntax(
            builder.Span(statement),
            command?.ATOM()?.GetText() ?? string.Empty,
            builder.Span(command?.ATOM()),
            command == null ? [] : [.. command.argument().Select(builder.Argument)]
        );
    }
}
