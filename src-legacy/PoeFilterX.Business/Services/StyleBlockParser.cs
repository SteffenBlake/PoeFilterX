using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class StyleBlockParser : IStyleBlockParser
    {
        private IStyleCommandParser CommandParser { get; }
        public StyleBlockParser(IStyleCommandParser commandParser)
        {
            CommandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        }

        public IList<Action<FilterBlock>> Parse(TrackingStreamReader reader)
        {
            var runningArgs = "";
            var stringed = false;
            var commands = new List<Action<FilterBlock>>();
            var comment = false;
            while (!reader.EndOfStream)
            {
                var next = (char)reader.Read();
                if (comment)
                {
                    if (next == '*' && reader.Peek() == '/')
                    {
                        reader.Read();
                        comment = false;
                    }

                    continue;
                }

                if (!stringed && next == '/' && reader.Peek() == '*')
                {
                    comment = true;
                    reader.Read();
                    runningArgs += ' ';
                    continue;
                }

                if (next == '\\' && stringed)
                {
                    runningArgs += next;
                    if (reader.EndOfStream)
                    {
                        throw ParserException.UnexpectedCharacter(next, '"');
                    }

                    var escaped = (char)reader.Read();
                    if (escaped is not '"' and not '\\')
                    {
                        throw ParserException.UnexpectedCharacter(escaped, '"');
                    }

                    runningArgs += escaped;
                    continue;
                }

                if (next == '"')
                {
                    stringed = !stringed;
                    runningArgs += next;
                    continue;
                }

                if (!stringed && next == ';')
                {
                    var command = CommandParser.Parse(runningArgs.Trim());
                    if (command != null)
                    {
                        commands.Add(command);
                    }

                    runningArgs = "";
                    continue;
                }

                if (!stringed && next == '}')
                {
                    if (!string.IsNullOrWhiteSpace(runningArgs))
                    {
                        throw new ParserException("Missing ';' at end of style command.");
                    }

                    return commands;
                }

                if (!stringed && next == '\\')
                {
                    throw ParserException.UnexpectedCharacter(next, '"');
                }

                runningArgs += next;
            }

            throw new ParserException("Unterminated style block, string, or comment.");
        }
    }
}
