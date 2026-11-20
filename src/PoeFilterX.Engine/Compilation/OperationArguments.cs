using System.Globalization;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

internal static class OperationArguments
{
    public static ParseError? Count(ReadOnlySpan<string> args, params ReadOnlySpan<int> allowed)
    {
        if (!allowed.Contains(args.Length))
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgumentCount,
                $"Unexpected number of arguments, got '{args.Length}'"
            );
        }

        return null;
    }

    public static Result<int> Integer(
        string value,
        int minimum,
        int maximum,
        string name = "value"
    )
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            || number < minimum || number > maximum)
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgument,
                $"Unexpected value for {name}, expected {minimum}-{maximum}, got '{value}'"
            );
        }

        return number;
    }

    public static Result<bool> Boolean(string value)
    {
        if (!bool.TryParse(value, out var result))
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgument,
                $"Unexpected command '{value}', expected true or false"
            );
        }

        return result;
    }

    public static Result<T> Enumeration<T>(string value) where T : struct, Enum
    {
        if (!Enum.TryParse<T>(value, true, out var result) || !Enum.IsDefined(result)
            || int.TryParse(value, out _))
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgument,
                $"Unexpected command '{value}', expected {typeof(T).Name}"
            );
        }

        return result;
    }

    public static bool TryToggle(string value, out bool enabled)
    {
        enabled = value.Equals("enabled", StringComparison.OrdinalIgnoreCase);

        return enabled || value.Equals("disabled", StringComparison.OrdinalIgnoreCase);
    }

    public static Result<bool> Choice(
        string value,
        string yes,
        string no
    )
    {
        if (!value.Equals(yes, StringComparison.OrdinalIgnoreCase)
            && !value.Equals(no, StringComparison.OrdinalIgnoreCase))
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgument,
                $"Unexpected command '{value}'"
            );
        }

        return value.Equals(yes, StringComparison.OrdinalIgnoreCase);
    }

    public static ComparisonOperator Operator(ref ReadOnlySpan<string> args)
    {
        if (args.IsEmpty)
        {
            return ComparisonOperator.Equal;
        }

        ComparisonOperator? result = args[0] switch
        {
            "=" => ComparisonOperator.Equal,
            "==" => ComparisonOperator.ExactEqual,
            "!" => ComparisonOperator.NotEqual,
            ">" => ComparisonOperator.GreaterThan,
            "<" => ComparisonOperator.LessThan,
            "<=" => ComparisonOperator.LessThanOrEqual,
            ">=" => ComparisonOperator.GreaterThanOrEqual,
            _ => null
        };
        if (result is not { } value)
        {
            return ComparisonOperator.Equal;
        }

        args = args[1..];

        return value;
    }
}
