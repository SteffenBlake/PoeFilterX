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
                    if (next is '*' && (char)reader.Read() is '/')
                    {
                        comment = false;
                    }

                    continue;
                }
                
                switch (next)
                {
                    case '/' when reader.Peek() > 0 && (char)reader.Peek() == '*':
                        comment = true;
                        _ = reader.Read();
                        continue;

                    case ';' when !stringed:
                        {
                            var cmd = CommandParser.Parse(runningArgs.Trim());
                            if (cmd != null)
                            {
                                commands.Add(cmd);
                            }

                            runningArgs = "";
                            break;
                        }

                    case '}' when !stringed:
                        return commands;

                    case '"':
                        stringed = !stringed;
                        break;

                    case '\\':
                        if (!stringed)
                        {
                            throw ParserException.UnexpectedCharacter('\\', '"');
                        }

                        var escaped = (char)reader.Read();
                        if (escaped is '"' or '\\')
                        {
                            runningArgs += escaped;
                        } 
                        else
                        {
                            throw ParserException.UnexpectedCharacter(escaped, '"');
                        }

                        break;

                    default:
                        runningArgs += next;
                        break;
                }
            }

            throw ParserException.UnexpectedCharacter('}', ' ');
        }
    }
}