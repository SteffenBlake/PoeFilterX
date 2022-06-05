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
                    if (next == '*' && (char)reader.Read() == '/')
                        comment = false;

                    continue;
                }
                
                if (next == '/' && reader.Peek() > 0 && (char)reader.Peek() == '*')
                {
                    comment = true;
                    reader.Read();
                    continue;
                }

                if (next == ';' && !stringed)
                {
                    var cmd = CommandParser.Parse(runningArgs.Trim());
                    if (cmd != null)
                        commands.Add(cmd);

                    runningArgs = "";
                }
                else if (next == '}' && !stringed)
                {
                    return commands;
                }
                else if (next == '"')
                {
                    stringed = !stringed;
                }
                else if (next == '\\')
                {
                    if (!stringed)
                        throw ParserException.UnexpectedCharacter('\\', '"');

                    var escaped = (char)reader.Read();
                    if (escaped == '"' || escaped == '\\')
                    {
                        runningArgs += escaped;
                    } 
                    else
                    {
                        throw ParserException.UnexpectedCharacter(escaped, '"');
                    }
                }
                else
                {
                    runningArgs += next;

                }
            }

            throw ParserException.UnexpectedCharacter('}', ' ');
        }
    }
}