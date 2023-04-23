using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class FilterBlockParser : IFilterBlockParser
    {
        private IPathResolver PathResolver { get; }
        private Func<IFileParser> FileParserFactory { get; }
        private IFilterCommandParser CommandParser { get; }
        private ExecutingContext Context { get; }

        public FilterBlockParser(IPathResolver pathResolver, Func<IFileParser> fileParserFactory, IFilterCommandParser commandParser, ExecutingContext context)
        {
            PathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
            FileParserFactory = fileParserFactory ?? throw new ArgumentNullException(nameof(fileParserFactory));
            CommandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task ReadBlockAsync(Filter filter, TrackingStreamReader reader, FilterBlock? parent = null, bool nested = false)
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
                    await ReadBlockAsync(filter, reader, filterBlock, true);
                }
                else if (next == '}' && !isComment)
                {
                    if (parent == null)
                    {
                        throw ParserException.UnexpectedCharacter('}', ' ');
                    }

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
                        if (nested)
                        {
                            throw ParserException.UnrecognizedCommand(args[0]);
                        }

                        if (args.Length != 2)
                        {
                            throw ParserException.UnexpectedArgCount(args.Length - 1, 1);
                        }

                        var filePath = args[1].Trim();

                        var currentDir = Path.GetDirectoryName(reader.Path) ?? throw new DirectoryNotFoundException(reader.Path);
                        var relativeFile = PathResolver.ResolvePath(currentDir, filePath);

                        if (!Context.TryAddUsing(reader.Path, relativeFile))
                        {
                            throw ParserException.CircularDependency();
                        }

                        await FileParserFactory().ParseAsync(filter, relativeFile, filterBlock);
                    } 
                    else
                    {
                        var cmd = CommandParser.Parse(args);
                        if (cmd != null)
                        {
                            filterBlock.AddCommand(cmd);
                        }
                    }

                    runningString = "";
                }
                else if (!isComment)
                {
                    runningString += next;
                }
            }
        }
    }
}
