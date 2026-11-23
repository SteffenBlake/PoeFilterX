namespace PoeFilterX.Engine.Logging;

public enum CompilerLogEvent
{
    BuildStarting = 1000,
    ExecutionDirectory,
    BuildInput,
    ConfigurationLoaded,
    ProjectLoaded,
    Publishing,
    BuildCompleted,
    OpeningFile,
    ParsedFile,
    ImportResolved,
    CachedFile,
    VariableLoaded,
    ConfigurationVariable,
    DeprecatedSyntax,
    Diagnostic,
    PackageCached,
    PackageResolving,
    PackageRestoring,
    PackagesRestored,
    PackageOverridden
}
