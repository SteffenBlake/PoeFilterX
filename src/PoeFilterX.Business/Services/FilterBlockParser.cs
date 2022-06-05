using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class FilterBlockParser : IFilterBlockParser
    {
        private Func<IFileParser> FileParserFactory { get; }
        private IFilterCommandParser CommandParser { get; }
        public FilterBlockParser(Func<IFileParser> fileParserFactory, IFilterCommandParser commandParser)
        {
            FileParserFactory = fileParserFactory ?? throw new ArgumentNullException(nameof(fileParserFactory));
            CommandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        }

        public async Task ReadBlockAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null)
        {
            var runningString = "";
            var isComment = false;

            var filterBlock = new FilterBlock(parent);
            filter.AddFilterBlock(filterBlock);

            while (!reader.EndOfStream)
            {
                var next = (char)reader.Read();

                if (next == '#')
                {
                    isComment = true;
                }
                else if (next == '{' && !isComment)
                {
                    await ReadBlockAsync(filter, reader, filterBlock);
                }
                else if (next == '}' && !isComment)
                {
                    if (parent == null)
                        throw ParserException.UnexpectedCharacter('}', ' ');
                    break;
                }
                else if (next == '\n')
                {
                    isComment = false;
                }
                else if (next == ';' && !isComment)
                {

                    var args = runningString.Trim().ToArgs();

                    if (args[0].ToLower() == "using")
                    {
                        if (parent != null)
                            throw ParserException.UnrecognizedCommand(args[0]);

                        if (args.Length != 2)
                            throw ParserException.UnexpectedArgCount(args.Length - 1, 1);

                        var filePath = args[1].Trim();

                        var directory = Path.GetDirectoryName(reader.Path) ?? throw new DirectoryNotFoundException(reader.Path);
                        var relativeFile = Path.Combine(directory, filePath);
                        await FileParserFactory().ParseAsync(filter, relativeFile);
                    } 
                    else
                    {
                        var cmd = CommandParser.Parse(args);
                        if (cmd != null)
                            filterBlock.AddCommand(cmd);
                    }

                    runningString = "";
                }
                else
                {
                    runningString += next;
                }
            }
        }
    }
}
