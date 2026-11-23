using PoeFilterX.Engine.Configuration;
using System.Collections.Immutable;
using System.Text.Json;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Packages;

namespace PoeFilterX.LanguageServer;

internal sealed class EditorWorkspace(ProjectSettings? settings = null)
{
    private sealed record CachedProject(
        ProjectSnapshot Project,
        ImmutableDictionary<string, (long Ticks, long Length)> Files,
        ImmutableArray<string> PackageFiles
    );

    private readonly EditorProjectSource source = new();
    private readonly Dictionary<string, CachedProject> projects = [with(EditorProjectSource.PathComparer)];
    private readonly HashSet<string> invalidated = [with(EditorProjectSource.PathComparer)];
    private readonly List<string> roots = [];
    private readonly List<string> entrypoints = [];
    private long revision;

    public IReadOnlyDictionary<string, EditorDocument> Documents => source.Documents;

    public void Configure(JsonElement parameters)
    {
        roots.Clear();
        var folders = parameters.Property("workspaceFolders");
        if (folders.ValueKind == JsonValueKind.Array)
        {
            roots.AddRange(folders.EnumerateArray()
                .Select(folder => ProtocolJson.FilePath(folder.Property("uri").Text())));
        }

        if (roots.Count == 0 && parameters.Property("rootUri").ValueKind == JsonValueKind.String)
        {
            roots.Add(ProtocolJson.FilePath(parameters.Property("rootUri").Text()));
        }

        ConfigureEntrypoints(parameters.Property("initializationOptions"));
    }

    public void ConfigureEntrypoints(JsonElement options)
    {
        entrypoints.Clear();
        var configured = options.Property("entrypoints");
        if (configured.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in configured.EnumerateArray())
            {
                AddEntrypoint(entry.Text());
            }
        }

        projects.Clear();
    }

    private void AddEntrypoint(string entry)
    {
        if (Uri.TryCreate(entry, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            entrypoints.Add(ProtocolJson.FilePath(entry));
            return;
        }

        if (Path.IsPathFullyQualified(entry))
        {
            entrypoints.Add(Path.GetFullPath(entry));
            return;
        }

        if (roots.Count != 1 || string.IsNullOrWhiteSpace(entry))
        {
            throw ProtocolJson.InvalidParameters("Relative entrypoints require exactly one workspace folder.");
        }

        entrypoints.Add(Path.GetFullPath(entry, roots[0]));
    }

    public void ChangeFolders(JsonElement change)
    {
        var removed = change.Property("removed");
        if (removed.ValueKind == JsonValueKind.Array)
        {
            foreach (var folder in removed.EnumerateArray())
            {
                roots.Remove(ProtocolJson.FilePath(folder.Property("uri").Text()));
            }
        }

        var added = change.Property("added");
        if (added.ValueKind == JsonValueKind.Array)
        {
            roots.AddRange(added.EnumerateArray()
                .Select(folder => ProtocolJson.FilePath(folder.Property("uri").Text())));
        }

        projects.Clear();
    }

    public void Open(JsonElement document)
    {
        var path = ProtocolJson.FilePath(document.Property("uri").Text());
        source.Documents[path] = new(
            new(path, document.Property("text").Text()),
            document.Property("version").Number(),
            ++revision
        );
        Invalidate(path);
    }

    public void Change(JsonElement parameters)
    {
        var path = ProtocolJson.DocumentPath(parameters);
        if (!source.Documents.TryGetValue(path, out var document))
        {
            throw ProtocolJson.InvalidParameters("Document must be opened before changing it.");
        }

        var version = parameters.Property("textDocument").Property("version").Number();
        if (version <= document.Version)
        {
            return;
        }

        var text = document.Source;
        foreach (var change in parameters.Property("contentChanges").EnumerateArray())
        {
            var range = change.Property("range");
            var replacement = change.Property("text").Text();
            text = range.ValueKind == JsonValueKind.Object
                ? Replace(text, range, replacement) : new(path, replacement);
        }

        source.Documents[path] = new(text, version, ++revision);
        Invalidate(path);
    }

    private static SourceText Replace(
        SourceText source,
        JsonElement range,
        string text
    )
    {
        var start = ProtocolJson.Offset(source, range.Property("start"));
        var end = ProtocolJson.Offset(source, range.Property("end"));
        if (end < start)
        {
            throw ProtocolJson.InvalidParameters("Edit range ends before it starts.");
        }

        return new(source.Path, string.Concat(source.Text.AsSpan(0, start), text, source.Text.AsSpan(end)));
    }

    public void Close(string path)
    {
        source.Documents.Remove(path);
        Invalidate(path);
    }

    public void Invalidate(string path)
    {
        source.Invalidate(path);
        var affected = projects.Where(pair => EditorProjectSource.PathComparer.Equals(pair.Key, path)
            || EditorProjectSource.PathComparer.Equals(PackageCatalogReader.LockPath(pair.Key), path)
            || pair.Value.Project.SyntaxTrees.ContainsKey(path) || pair.Value.Project.VariableFiles.ContainsKey(path)
            || pair.Value.Project.Imports.Any(import => EditorProjectSource.PathComparer.Equals(import.TargetPath, path)))
            .Select(pair => pair.Key).ToArray();
        foreach (var entry in affected)
        {
            invalidated.Add(entry);
        }
    }

    public EditorContext Context(string path, CancellationToken cancellationToken)
    {
        if (ProjectSettings.IsConfigurationFile(path))
        {
            var selected = Load(path, cancellationToken);
            if (selected.SyntaxTrees.TryGetValue(selected.EntryPoint, out var entryTree))
            {
                return new(selected, entryTree);
            }

            throw ProtocolJson.InvalidParameters(string.Join(" ", selected.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var owner = projects.FirstOrDefault(pair => pair.Value.Project.Packages.References.Any(package =>
            package.Root is { } root && PackagePaths.IsWithin(path, root)));
        var candidates = owner.Key is null ? Candidates(path) : [owner.Key];
        var ambiguous = entrypoints.Count == 0 && candidates.Length > 1;
        var warning = ambiguous
            ? $"Multiple PoeFilterX entrypoints were found in '{Path.GetDirectoryName(candidates[0])}': "
                + $"{string.Join(", ", candidates)}. Specify the intended entrypoint in poefilterx.entrypoints "
                + "(workspace settings) or initializationOptions.entrypoints."
            : null;
        foreach (var entry in ambiguous ? [] : candidates)
        {
            var project = Load(entry, cancellationToken);
            if (project.SyntaxTrees.TryGetValue(path, out var tree))
            {
                return new(project, tree);
            }
        }

        var parsed = source.Parse(path, cancellationToken);
        var local = parsed switch
        {
            SyntaxTree tree => tree,
            Deprecated<SyntaxTree> deprecated => deprecated.Value,
            ParseError { PartialTree: { } tree } => tree,
            _ => null
        };
        if (local is not null)
        {
            var localProject = new ProjectSnapshot(
                path,
                ImmutableDictionary.Create<string, SyntaxTree>(EditorProjectSource.PathComparer).Add(path, local),
                [],
                [],
                local.Diagnostics
            );

            return new(localProject, local, false, warning);
        }

        throw ProtocolJson.InvalidParameters("The document is not a readable .filterx or .fss file.");
    }

    private ImmutableArray<string> Candidates(string path)
    {
        if (entrypoints.Count != 0)
        {
            return [.. entrypoints];
        }

        var root = roots.Where(folder => IsWithin(path, folder)).OrderByDescending(folder => folder.Length).FirstOrDefault();
        var directory = Path.GetDirectoryName(path)!;
        var configured = ProjectSettings.Load(directory, settings?.Profile, settings?.CommandLine);
        if (configured is ProjectSettings projectSettings
            && File.Exists(Path.Combine(projectSettings.Root, ProjectSettings.FileName)))
        {
            return [Path.Combine(projectSettings.Root, ProjectSettings.FileName)];
        }

        while (true)
        {
            var candidates = EntrypointDiscovery.Find(directory, source.Documents.Keys);
            if (!candidates.IsEmpty)
            {
                return candidates;
            }

            // Without a containing workspace, discovery is limited to the document's directory.
            if (root is null || EditorProjectSource.PathComparer.Equals(directory, Path.TrimEndingDirectorySeparator(root)))
            {
                return [];
            }

            var parent = Path.GetDirectoryName(directory);
            if (parent is null || !IsWithin(parent, root))
            {
                return [];
            }

            directory = parent;
        }
    }

    private static bool IsWithin(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private ProjectSnapshot Load(string path, CancellationToken cancellationToken)
    {
        if (projects.TryGetValue(path, out var cached) && !invalidated.Remove(path) && !FilesChanged(cached))
        {
            return cached.Project;
        }

        var selectedProfile = ProjectSettings.IsConfigurationFile(path)
            && Path.GetFileName(path) != ProjectSettings.FileName;
        var configured = ProjectSettings.LoadForInput(
            path, selectedProfile ? null : settings?.Profile, settings?.CommandLine
        );
        var resolved = configured is ProjectSettings selectionSettings
            ? ProjectSettings.IsConfigurationFile(path) ? EntrypointResolver.Resolve(path, selectionSettings) : new Result<string>(path)
            : new Result<string>((ParseError)configured.Value!);
        var target = resolved.Value as string ?? path;
        var project = configured is ProjectSettings projectSettings && resolved is string entrypoint
            ? new ProjectLoader(source, settings: projectSettings).Load(entrypoint, cancellationToken)
            : new ProjectLoader(source).Load(target, cancellationToken) with
            {
                Diagnostics = ((ParseError)(resolved.Value ?? configured.Value)!).Diagnostics
            };
        var packageFiles = PackageFiles(project).Concat(settings?.Profile is { } profile
            ? [Path.Combine(project.Settings?.Root ?? Path.GetDirectoryName(path)!, ProjectSettings.ProfileFileName(profile))]
            : Array.Empty<string>()).Distinct(EditorProjectSource.PathComparer).ToImmutableArray();
        var files = ProjectFiles(project).Concat(packageFiles).Distinct(EditorProjectSource.PathComparer)
            .Where(file => !source.Documents.ContainsKey(file))
            .ToImmutableDictionary(file => file, Stamp, EditorProjectSource.PathComparer);
        projects[path] = new(project, files, packageFiles);

        return project;
    }

    public bool PackagesChanged()
    {
        return projects.Values.Any(cached => cached.PackageFiles.Concat(OverrideFiles(cached.Project)).Any(file =>
            cached.Files.TryGetValue(file, out var stamp) && stamp != Stamp(file)));
    }

    private bool FilesChanged(CachedProject project)
    {
        var changed = false;
        foreach (var (file, previous) in project.Files.Where(pair => !source.Documents.ContainsKey(pair.Key)))
        {
            if (previous != Stamp(file))
            {
                source.Invalidate(file);
                changed = true;
            }
        }

        return changed;
    }

    private static IEnumerable<string> ProjectFiles(ProjectSnapshot project)
    {
        return project.SyntaxTrees.Keys.Concat(project.VariableFiles.Keys)
            .Concat(project.Imports.Select(import => import.TargetPath))
            .Distinct(EditorProjectSource.PathComparer);
    }

    private static IEnumerable<string> PackageFiles(ProjectSnapshot project)
    {
        var configurationFiles = project.Settings?.Files ?? [];
        return project.Packages.References.Where(package => package.Commit is not null && !package.IsOverride)
            .Select(package => Path.Combine(
                PackageIdentity.SnapshotDirectory(
                    project.Settings?.CacheRoot ?? PackageIdentity.CacheRoot, package.Repository, package.Commit!
                ),
                "complete"
            )).Prepend(PackageCatalogReader.LockPath(project.EntryPoint)).Concat(configurationFiles).Distinct(EditorProjectSource.PathComparer);
    }

    private static IEnumerable<string> OverrideFiles(ProjectSnapshot project)
    {
        var roots = project.Packages.References.Where(package => package.IsOverride && package.Root is not null)
            .Select(package => package.Root!);
        return ProjectFiles(project).Where(path => roots.Any(root => PackagePaths.IsWithin(path, root)));
    }

    private static (long Ticks, long Length) Stamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? (file.LastWriteTimeUtc.Ticks, file.Length) : default;
    }

    public IEnumerable<ProjectSnapshot> AllProjects(CancellationToken cancellationToken)
    {
        var entries = entrypoints.Count > 0 ? entrypoints : roots
            .Select(root => Candidates(Path.Combine(root, "_")))
            .Where(candidates => candidates.Length == 1)
            .SelectMany(candidates => candidates);
        var loaded = new HashSet<string>(EditorProjectSource.PathComparer);
        foreach (var entry in entries)
        {
            var project = Load(entry, cancellationToken);
            if (loaded.Add(project.EntryPoint))
            {
                yield return project;
            }
        }

        foreach (var path in source.Documents.Keys.Where(path => Path.GetExtension(path).ToLowerInvariant() is ".filterx" or ".fss"))
        {
            var context = Context(path, cancellationToken);
            var project = context.Project;
            if (context.HasEntrypoint && loaded.Add(project.EntryPoint))
            {
                yield return project;
            }
        }
    }

    public SourceText ReadText(string path) => source.ReadText(path);

    public static ImmutableArray<SyntaxDiagnostic> Diagnostics(ProjectSnapshot project)
    {
        if (!project.Diagnostics.IsEmpty)
        {
            return project.Diagnostics;
        }

        var compiled = ProjectCompiler.Prepare(project, ProjectConfiguration.Read(null, project.Settings)).FlatMap(static prepared =>
            ProjectCompiler.Write(prepared, TextWriter.Null));
        if (compiled is ParseError error)
        {
            return error.Diagnostics;
        }

        return [];
    }
}

internal sealed record EditorContext(
    ProjectSnapshot Project,
    SyntaxTree Tree,
    bool HasEntrypoint = true,
    string? DiscoveryWarning = null
);
