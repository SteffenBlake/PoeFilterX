using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.LanguageServer;

internal static class SyntaxFacts
{
    public static IEnumerable<CommandSyntax> Commands(SyntaxNode node)
    {
        return node switch
        {
            CommandSyntax command => [command],
            DocumentSyntax document => document.Statements.SelectMany(Commands),
            FilterBlockSyntax block => block.Statements.SelectMany(Commands),
            StyleSyntax style => style.Declarations,
            _ => []
        };
    }

    public static IEnumerable<FilterBlockSyntax> Blocks(SyntaxNode node)
    {
        var children = node switch
        {
            DocumentSyntax document => document.Statements,
            FilterBlockSyntax block => block.Statements,
            _ => []
        };

        return children.OfType<FilterBlockSyntax>().SelectMany(block => Blocks(block).Prepend(block));
    }

    public static bool Contains(SourceSpan span, int offset) => offset >= span.Start && offset <= span.End;

    public static SourceSpan Word(SourceText source, int offset)
    {
        var start = offset;
        var end = offset;
        while (start > 0 && IsWord(source.Text[start - 1]))
        {
            start--;
        }

        while (end < source.Text.Length && IsWord(source.Text[end]))
        {
            end++;
        }

        return new(start, end - start);
    }

    private static bool IsWord(char value) => !char.IsWhiteSpace(value) && !";{}[]:\"".Contains(value);

    public static string StatementHead(SyntaxTree tree, int offset)
    {
        string head = "";
        foreach (var token in tree.Tokens)
        {
            if (token.Span.Start >= offset)
            {
                break;
            }

            if (token.IsTrivia)
            {
                continue;
            }

            if (token.Kind is "SEMICOLON" or "OPEN_BRACE" or "CLOSE_BRACE" or "OPEN_BRACKET" or "CLOSE_BRACKET")
            {
                head = "";
                continue;
            }

            if (head.Length == 0)
            {
                head = tree.Source.Slice(token.Span).ToString();
            }
        }

        return head;
    }
}
