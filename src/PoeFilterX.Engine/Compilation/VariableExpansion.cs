using System.Text;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;
using System.Text.RegularExpressions;

namespace PoeFilterX.Engine.Compilation;

internal static partial class VariableExpansion
{
    [GeneratedRegex("%\\w+%", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    public static Result<string[]> Expand(IEnumerable<string> arguments, IReadOnlyDictionary<string, string> variables)
    {
        var result = new List<string>();
        foreach (var argument in arguments)
        {
            var text = argument;
            var expanded = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (Placeholder().IsMatch(text))
            {
                if (!seen.Add(text) || seen.Count > 128)
                {
                    return new ParseError(DiagnosticCodes.VariableExpansion, "Circular variable expansion");
                }

                expanded = true;
                var match = Placeholder().Match(text);
                variables.TryGetValue(match.Value[1..^1], out var value);
                text = text.Replace(match.Value, value ?? "", StringComparison.Ordinal);
            }

            if (expanded)
            {
                var split = Split(text);
                if (split is ParseError error)
                {
                    return error;
                }

                if (split is string[] values)
                {
                    result.AddRange(values);
                }
            }
            else
            {
                result.Add(text);
            }
        }

        return result.ToArray();
    }

    // Expanded variables use the language's argument rules, including quotes and escapes.
    // .NET has no shell/language argument tokenizer with these semantics.
    private static Result<string[]> Split(string text)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted)
                {
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }

                quoted = !quoted;
                continue;
            }

            if (character == '\\')
            {
                if (!quoted || ++index == text.Length)
                {
                    return new ParseError(DiagnosticCodes.VariableExpansion, "Unexpected character '\\'");
                }

                var escaped = text[index];
                if (escaped is not ('n' or 't' or '\\' or '"'))
                {
                    return new ParseError(DiagnosticCodes.VariableExpansion, $"Unexpected character '{escaped}'");
                }

                character = escaped switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => escaped
                };
            }
            else if (!quoted && char.IsWhiteSpace(character))
            {
                if (buffer.Length > 0)
                {
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }

                continue;
            }

            buffer.Append(character);
        }

        if (quoted)
        {
            return new ParseError(DiagnosticCodes.VariableExpansion, "Unexpected character, expected closing quote");
        }

        if (buffer.Length > 0)
        {
            result.Add(buffer.ToString());
        }

        return result.ToArray();
    }
}
