using System.Collections.Immutable;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Diagnostics;

namespace PoeFilterX.Engine.Parsing;

internal sealed class SyntaxTreeBuilder
{
    private readonly Dictionary<IToken, SourceSpan> spans = [];

    public SourceText Source { get; }

    public ImmutableArray<SyntaxToken> Tokens { get; }

    public ImmutableArray<SyntaxDiagnostic> Diagnostics { get; }

    public SyntaxTreeBuilder(
        string path,
        CommonTokenStream stream,
        Lexer lexer,
        ParserDiagnostics diagnostics
    )
    {
        var text = new StringBuilder();
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();
        var errors = ImmutableArray.CreateBuilder<SyntaxDiagnostic>();
        foreach (var token in stream.GetTokens())
        {
            var raw = token.Type == TokenConstants.EOF ? string.Empty : token.Text;
            var span = new SourceSpan(text.Length, raw.Length);
            spans.Add(token, span);
            text.Append(raw);
            var kind = lexer.Vocabulary.GetSymbolicName(token.Type) ?? "EOF";
            tokens.Add(new SyntaxToken(kind, span, token.Channel != TokenConstants.DefaultChannel));
            var error = kind switch
            {
                "INVALID" => (DiagnosticCodes.InvalidToken, $"invalid token '{raw}'"),
                "INVALID_STRING" => (DiagnosticCodes.InvalidEscape, "invalid escape sequence in string literal"),
                "UNTERMINATED_STRING" => (DiagnosticCodes.UnterminatedString, "unterminated string literal"),
                "COMMENT" when raw.StartsWith("/*") && !raw.EndsWith("*/") =>
                    (DiagnosticCodes.UnterminatedComment, "unterminated block comment"),
                _ => default
            };
            if (error.Item1 is not null)
            {
                errors.Add(new SyntaxDiagnostic(error.Item1, error.Item2, path, span));
            }
        }

        Source = new SourceText(path, text.ToString());
        Tokens = tokens.ToImmutable();
        foreach (var entry in diagnostics.Entries)
        {
            errors.Add(new SyntaxDiagnostic(entry.Code, entry.Message, path, Span(entry.Token))
            {
                ExpectedTokens = entry.ExpectedTokens,
                RuleStack = entry.RuleStack
            });
        }

        Diagnostics = [.. errors.OrderBy(error => error.Span.Start)];
    }

    public SourceSpan Span(ITerminalNode? node) => Span(node?.Symbol);

    public SourceSpan Span(IToken? token)
    {
        return token != null && spans.TryGetValue(token, out var span)
            ? span
            : new SourceSpan(Source.Text.Length, 0);
    }

    public SourceSpan Span(ParserRuleContext context)
    {
        var start = Span(context.Start).Start;
        var end = Span(context.Stop).End;

        return new SourceSpan(start, Math.Max(0, end - start));
    }

    public ArgumentSyntax Argument(ParserRuleContext context)
    {
        var span = Span(context);
        var raw = span.Length == 0 ? string.Empty : ArgumentText(context, span);
        var quoted = raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"';

        return new ArgumentSyntax(span, quoted ? DecodeString(raw) : raw, quoted);
    }

    private string ArgumentText(ParserRuleContext context, SourceSpan span)
    {
        // Reuse the lexer's copied string on the normal path; recovery may cover multiple tokens.
        return Span(context.Start) == span ? context.Start.Text : Source.Slice(span).ToString();
    }

    public SyntaxTree Build(ImmutableArray<SyntaxNode> statements)
    {
        return new SyntaxTree(
            Source,
            new DocumentSyntax(new SourceSpan(0, Source.Text.Length), statements),
            Tokens,
            Diagnostics
        );
    }

    private static string DecodeString(string raw)
    {
        if (!raw.Contains('\\'))
        {
            return raw[1..^1];
        }

        var value = new StringBuilder(raw.Length - 2);
        for (var index = 1; index < raw.Length - 1; index++)
        {
            var character = raw[index];
            if (character == '\\')
            {
                character = raw[++index] switch { 'n' => '\n', 't' => '\t', var escaped => escaped };
            }

            value.Append(character);
        }

        return value.ToString();
    }
}
