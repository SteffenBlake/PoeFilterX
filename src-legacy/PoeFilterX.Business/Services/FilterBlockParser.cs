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

        public async Task ReadBlockAsync(
            Filter filter,
            TrackingStreamReader reader,
            FilterBlock? parent = null,
            bool nested = false,
            bool abstracted = false
        )
        {
            var runningString = "";
            var isComment = false;
            var isString = false;
            var filterBlock = new FilterBlock(parent);
            if (!abstracted)
            {
                filter.AddFilterBlock(filterBlock);
            }

            while (!reader.EndOfStream)
            {
                var next = (char)reader.Read();
                if (isComment)
                {
                    if (next == '\n')
                    {
                        isComment = false;
                        runningString += ' ';
                    }

                    continue;
                }

                if (next == '\\' && isString)
                {
                    runningString += next;
                    if (reader.EndOfStream)
                    {
                        throw ParserException.UnexpectedCharacter(next, '"');
                    }

                    runningString += (char)reader.Read();
                    continue;
                }

                if (next == '"')
                {
                    isString = !isString;
                    runningString += next;
                    continue;
                }

                if (isString)
                {
                    runningString += next;
                    continue;
                }

                switch (next)
                {
                    case '#':
                        isComment = true;
                        break;

                    case '{':
                    case '[':
                        RequireTerminatedCommand(runningString);
                        await ReadBlockAsync(
                            filter,
                            reader,
                            filterBlock,
                            nested: true,
                            abstracted: next == '['
                        );
                        break;

                    case '}':
                    case ']':
                        RequireTerminatedCommand(runningString);
                        if (!nested || (next == ']' != abstracted))
                        {
                            throw ParserException.UnexpectedCharacter(next, abstracted ? ']' : '}');
                        }

                        return;

                    case ';':
                        var args = runningString.Trim().ToArgs();
                        if (args.Length == 0)
                        {
                            throw new ParserException("Empty command.");
                        }

                        await ReadCommandAsync(filter, reader, filterBlock, args, nested);
                        runningString = "";
                        break;

                    default:
                        runningString += next;
                        break;
                }
            }

            RequireTerminatedCommand(runningString);
            if (nested)
            {
                throw new ParserException($"Missing closing '{(abstracted ? ']' : '}')}'.");
            }
        }

        private static void RequireTerminatedCommand(string command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                throw new ParserException("Missing ';' at end of command.");
            }
        }

        private async Task ReadCommandAsync(
            Filter filter,
            TrackingStreamReader reader,
            FilterBlock filterBlock,
            string[] args,
            bool nested
        )
        {
            if (!args[0].Equals("using", StringComparison.OrdinalIgnoreCase))
            {
                var command = CommandParser.Parse(args);
                if (command != null)
                {
                    filterBlock.AddCommand(command);
                }

                return;
            }

            if (nested)
            {
                throw ParserException.UnrecognizedCommand(args[0]);
            }

            if (args.Length != 2)
            {
                throw ParserException.UnexpectedArgCount(args.Length - 1, 1);
            }

            var currentDir = Path.GetDirectoryName(reader.Path) ?? throw new DirectoryNotFoundException(reader.Path);
            var relativeFile = PathResolver.ResolvePath(currentDir, args[1].Trim());
            if (!Context.TryAddUsing(reader.Path, relativeFile))
            {
                throw ParserException.CircularDependency();
            }

            await FileParserFactory().ParseAsync(filter, relativeFile, filterBlock);
        }
    }
}
