using Antlr4.Runtime;

namespace PoeFilterX.Engine.Parsing;

internal sealed class GuardedTokenSource(
    Lexer lexer,
    int openBrace,
    int closeBrace,
    int openBracket = -1,
    int closeBracket = -1
) : ITokenSource
{
    private int depth;

    public IToken? LimitToken { get; private set; }

    public int Line => lexer.Line;

    public int Column => lexer.Column;

    public ICharStream InputStream => (ICharStream)lexer.InputStream;

    public string SourceName => lexer.SourceName;

    public ITokenFactory TokenFactory
    {
        get => lexer.TokenFactory;
        set => lexer.TokenFactory = value;
    }

    public IToken NextToken()
    {
        var token = lexer.NextToken();
        if (token is CommonToken eof && token.Type == TokenConstants.EOF)
        {
            // The moving character stream returns empty text at EOF; retain ANTLR's standard display.
            eof.Text = "<EOF>";
        }

        var previousDepth = depth;
        if (token.Type == openBrace || token.Type == openBracket)
        {
            depth++;
        }
        else if (token.Type == closeBrace || token.Type == closeBracket)
        {
            depth = Math.Max(0, depth - 1);
        }

        if (Math.Max(previousDepth, depth) > 128 && token is CommonToken delimiter
            && (token.Type == openBrace || token.Type == closeBrace
                || token.Type == openBracket || token.Type == closeBracket))
        {
            LimitToken ??= token;
            // Retain source text while preventing ANTLR from recursing beyond its supported depth.
            delimiter.Channel = TokenConstants.HiddenChannel;
        }

        return token;
    }
}
