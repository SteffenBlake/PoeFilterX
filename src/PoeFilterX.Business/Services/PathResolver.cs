using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;

namespace PoeFilterX.Business.Services
{
    public class PathResolver : IPathResolver
    {
        private FilterXConfiguration Config { get; }
        public PathResolver(FilterXConfiguration config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string ResolvePath(string executingPath, string relativePath)
        {
            // Substitute the @/ path prefix with node_modules folder in executing root
            if (relativePath.StartsWith("@"))
            {
                var nodeRelativePath = relativePath[1..];

                return Path.Combine(Config.NodePath, nodeRelativePath);
            }

            return Path.Combine(executingPath, relativePath);
        }

    }
}
