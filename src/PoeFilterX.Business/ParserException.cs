namespace PoeFilterX.Business
{
    public class ParserException : Exception
    {
        public ParserException(string message) : base(message)
        {
        }

        public static ParserException UnexpectedArgCount(int actual, params int[] expected)
        {
            return new ParserException($"Unexpected number of arguments, expected '{string.Join("/", expected)}' but got '{actual}'");
        }

        public static ParserException UnexpectedCharacter(char? actual, char expected) 
        {
            return new ParserException($"Unexpected character, expected '{expected}' but got '{actual}'");
        }

        internal static ParserException UnrecognizedCommand(string cmd, string? expected = null)
        {
            var msg = $"Unexpected command '{cmd}'";
            if (expected != null)
                msg += $", expected {expected}";

            return new ParserException(msg);
        }
    }
}
