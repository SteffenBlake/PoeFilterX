namespace PoeFilterX.Business.Extensions
{
    /// <summary>
    /// Extends <see cref="string"/>
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Breaks a string apart into arguments, ensuring strings are kept as a single argument despite whitespace
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static string[] ToArgs(this string command)
        {
            var args = new List<string>();

            var isString = false;

            var runningArg = "";

            var trimmedCmd = command.Trim();

            for (var n = 0; n < trimmedCmd.Length; n++)
            {
                var c = trimmedCmd[n];

                if (Char.IsWhiteSpace(c))
                {
                    if (isString)
                    {
                        runningArg += c;
                    } 
                    else if (runningArg.Length > 0)
                    {
                        if (!isString)
                        {
                            runningArg = runningArg.Trim();
                        }

                        if (!string.IsNullOrEmpty(runningArg))
                        {
                            args.Add(runningArg);
                        }

                        runningArg = "";
                    }
                }
                else if (c == '\\')
                {
                    if (!isString)
                    {
                        throw ParserException.UnexpectedCharacter('\\', ' ');
                    }

                    n++;
                    if (n == trimmedCmd.Length)
                    {
                        throw ParserException.UnexpectedCharacter('\\', ' ');
                    }

                    _ = trimmedCmd[n] switch
                    {
                        'n' => runningArg += '\n',
                        't' => runningArg += '\t',
                        '\\' => runningArg += '\\',
                        '\"' => runningArg += '\"',
                        _ => throw ParserException.UnexpectedCharacter(trimmedCmd[n], ' ')
                    };
                    

                }
                else if (c == '"')
                {
                    if (isString)
                    {
                        args.Add(runningArg);
                        runningArg = "";
                    }
                    isString = !isString;
                } 
                else
                {
                    runningArg += c;
                }
            }

            if (isString)
            {
                throw ParserException.UnexpectedCharacter(' ', '\"');
            }

            if (runningArg.Length > 0)
            {
                args.Add(runningArg);
            }

            return args.ToArray();
        }
    }
}
