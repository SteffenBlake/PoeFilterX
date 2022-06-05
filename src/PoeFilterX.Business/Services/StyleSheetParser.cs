using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class StyleSheetParser : ISectionParser
    {
        private Func<IFileParser> FileParserFactory { get; }
        private IStyleBlockParser BlockParser { get; }
        public StyleSheetParser(Func<IFileParser> fileParserFactory, IStyleBlockParser blockParser)
        {
            FileParserFactory = fileParserFactory ?? throw new ArgumentNullException(nameof(fileParserFactory));
            BlockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
        }

        public string FileExtension => ".fss";

        public async Task ParseAsync(Filter filter, TrackingStreamReader reader)
        {
            var runningArg = "";
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

                if (next == '{')
                {
                    var trimmedName = runningArg.Trim();
                    if (trimmedName.Any(c => char.IsWhiteSpace(c)))
                        throw ParserException.UnexpectedCharacter(' ', '{');

                    var commands = BlockParser.Parse(reader);

                    foreach(var cmd in commands)
                    {
                        filter.AddStyle(trimmedName, cmd);
                    }

                    runningArg = "";
                } 
                else if (next == ';')
                {
                    var args = runningArg.Trim().ToArgs();
                    if (args[0].ToLower() == "using")
                    {
                        if (args.Length != 2)
                            throw ParserException.UnexpectedArgCount(args.Length - 1, 2);

                        var filePath = args[1];
                        var extension = Path.GetExtension(filePath);
                        if (extension != FileExtension)
                            throw new ParserException($"Unrecognized file extension for StyleSheet using statement: '{extension}'");

                        var relativeFile = Path.GetRelativePath(reader.Path, filePath);
                        await FileParserFactory().ParseAsync(filter, relativeFile);
                    }
                    else
                    {
                        throw new ParserException($"Unrecognized command '{args[0]}'");
                    }
                }
                else
                {
                    runningArg += next;
                }
            }
        }
    }
}
