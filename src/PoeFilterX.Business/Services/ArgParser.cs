using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using System.Runtime.CompilerServices;

namespace PoeFilterX.Business.Services
{
    public class ArgParser
    {
        public bool TryParseOperator(string arg, out FilterOperator filterOperator)
        {
            filterOperator = FilterOperator.Equals;
            if (int.TryParse(arg, out _))
            {
                return false;
            }

            var displayNames = Enum.GetValues<FilterOperator>().ToDictionary(o => o.GetDisplayName(), o => o);

            
            if (!displayNames.ContainsKey(arg))
            {
                return false;
            }

            filterOperator = displayNames[arg];
            return true;

        }

        public void ThrowIfIntOutOfRange(string arg, int min, int? max, out int result, [CallerArgumentExpression("result")] string? resultName = null)
        {
            if (!int.TryParse(arg, out result) || result < min || (max.HasValue && result > max))
            {
                throw new ParserException($"Unexpected value for {resultName}, expected {min}-{max}, got '{arg}'");
            }
        }

        public void ThrowIfArgsWrong(IReadOnlyList<string> args, params int[] expected)
        {
            if (expected.Any(c => args.Count == c))
            {
                return;
            }

            throw ParserException.UnexpectedArgCount(args.Count, expected);
        }

        public void ThrowIfNotPositionalString(string arg, out bool result, [CallerArgumentExpression("result")] string? resultName = null)
        {
            var positionalString = arg.ToLower();
            result = positionalString switch
            {
                "global" => false,
                "positional" => true,
                _ => throw ParserException.UnrecognizedCommand(arg, "Global/Positional")
            };
        }

        public void ThrowIfNotEnum<TEnum>(string arg, out TEnum result)
            where TEnum : struct, Enum
        {
            if (int.TryParse(arg, out _))
            {
                throw ParserException.UnrecognizedCommand(arg, typeof(TEnum).Name);
            }

            if (!Enum.TryParse(arg, true, out result))
            {
                throw ParserException.UnrecognizedCommand(arg, typeof(TEnum).Name);
            }

            if (!Enum.IsDefined(result))
            {
                throw ParserException.UnrecognizedCommand(arg, typeof(TEnum).Name);
            }
        }

        public void ThrowIfNotBoolean(string arg, out bool result)
        {
            if (!bool.TryParse(arg, out result))
            {
                throw ParserException.UnrecognizedCommand(arg, "True/False");
            }
        }

        public bool TryParseToggleString(string arg, out bool result)
        {
            result = false;
            var toggleString = arg.ToLower();
            if (toggleString == "disabled")
            {
                return true;
            }
            else if (toggleString == "enabled")
            {
                return result = true;
            }

            // Parse failed
            return false;
        }
    }
}
