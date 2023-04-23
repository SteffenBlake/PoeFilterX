namespace PoeFilterX.Business.Models
{
    public class FilterXConfiguration
    {
        public string? P { get; set; }
        public string? Path { get; set; }

        private string? _usedPath;
        public string UsedPath => P ?? Path ?? (_usedPath ??= DetectUsedPath());

        private string DetectUsedPath()
        {
            var matches = Directory.GetFiles(Environment.CurrentDirectory, "*.filterx");
            if (matches is { Length: 1 })
            {
                return matches[0];
            }

            throw new ParserException("Unable to automatically detect a valid .filterx project to build from, please specify a path via the --P/--Path arguments");
        }

        public string? O { get; set; }
        public string? Output { get; set; }
        private string? _outputPath;
        public string OutputPath() => O ?? Output ?? (_outputPath ??= DetectOutputPath());
        private string DetectOutputPath()
        {
            var dir = System.IO.Path.GetDirectoryName(UsedPath) ??
                throw new ParserException($"Unrecognized output path: '{UsedPath}', please specify an output path via --O/--Output argument");
            var without = System.IO.Path.GetFileNameWithoutExtension(UsedPath) ??
                throw new ParserException($"Unrecognized output path: '{dir}', please specify an output path via --O/--Output argument");
            var file = without + ".filter";
            return System.IO.Path.Combine(dir, file);
        }

        public string NodePath => System.IO.Path.Combine(UsedPath, "node_modules");
    }
}
