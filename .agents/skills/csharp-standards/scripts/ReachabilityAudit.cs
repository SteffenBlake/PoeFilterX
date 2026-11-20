#:property TargetFramework=net11.0
#:property PublishAot=false
#:property Nullable=enable
#:property TreatWarningsAsErrors=true

// Usage: dotnet ReachabilityAudit.cs -- app.csproj [--artifacts DIRECTORY]
// Uses only SDK/MSBuild/ILLink and framework APIs. It does not load or execute the inspected application.
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

return await ReachabilityAudit.Run(args);

internal static class ReachabilityAudit
{
    private sealed record ProjectInfo(string File, string Assembly, string Binary);
    private sealed record SourceLocation(string File, int Line);
    private sealed record MemberInfo(bool Generated, SourceLocation? Location);

    public static async Task<int> Run(string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: dotnet ReachabilityAudit.cs -- app.csproj [--artifacts DIRECTORY]");
            return 0;
        }

        string? temporary = null;
        try
        {
            var project = Path.GetFullPath(arguments[0]);
            if (!File.Exists(project) || !project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The first argument must be an executable .csproj file.");
            }

            var artifacts = ReadArtifacts(arguments);
            temporary = artifacts is null ? Directory.CreateTempSubdirectory("dotnet-reachability-").FullName : null;
            var work = artifacts ?? temporary!;
            Directory.CreateDirectory(work);
            await Analyze(project, work);

            return 0;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException
            or InvalidOperationException or XmlException or BadImageFormatException)
        {
            Console.Error.WriteLine($"Reachability audit failed: {exception.Message}");
            return 1;
        }
        finally
        {
            if (temporary is not null)
            {
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    private static string? ReadArtifacts(string[] arguments)
    {
        string? directory = null;
        for (var index = 1; index < arguments.Length; index++)
        {
            if (arguments[index] != "--artifacts" || ++index >= arguments.Length)
            {
                throw new ArgumentException("Unknown option or missing --artifacts directory.");
            }

            directory = Path.GetFullPath(arguments[index]);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            {
                throw new ArgumentException("--artifacts must point to an empty or new directory.");
            }
        }

        return directory;
    }

    private static async Task Analyze(string project, string work)
    {
        var runtime = RuntimeInformation.RuntimeIdentifier;
        var projects = await FindProjects(project, work, runtime);
        var root = projects[0];
        await Publish(root, work, runtime);
        var inventory = ReadMembers(projects, Path.GetDirectoryName(root.Binary)!);
        var retained = ReadRetained(Path.Combine(work, "dependencies.xml"));
        if (retained.Count == 0)
        {
            throw new InvalidOperationException("The SDK dependency trace contains no retained methods.");
        }

        var candidates = inventory.Where(pair => !retained.Contains(pair.Key)
                && !pair.Value.Generated && pair.Value.Location is not null)
            .OrderBy(pair => pair.Value.Location!.File, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value.Location!.Line)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal);
        foreach (var (signature, member) in candidates)
        {
            var location = member.Location!;
            Console.WriteLine($"{location.File}:{location.Line}: {signature[7..]}");
        }
    }

    private static async Task<List<ProjectInfo>> FindProjects(
        string entry,
        string artifacts,
        string runtime
    )
    {
        var projects = new List<ProjectInfo>();
        var pending = new Queue<string>();
        var visited = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        pending.Enqueue(entry);
        while (pending.TryDequeue(out var file))
        {
            if (!visited.Add(file))
            {
                continue;
            }

            var output = await Execute([
                "msbuild", file, "-nologo", "-getProperty:TargetPath,AssemblyName,OutputType,TargetFrameworks",
                "-getItem:ProjectReference", "-p:Configuration=Release", $"-p:RuntimeIdentifier={runtime}",
                $"-p:ArtifactsPath={artifacts}"
            ]);
            using var document = JsonDocument.Parse(output);
            var properties = document.RootElement.GetProperty("Properties");
            if (properties.GetProperty("TargetFrameworks").GetString() is { Length: > 0 })
            {
                throw new ArgumentException($"Multi-targeted project '{file}' needs an explicit framework; this audit supports single-target projects.");
            }

            if (projects.Count == 0 && properties.GetProperty("OutputType").GetString() is not ("Exe" or "WinExe"))
            {
                throw new ArgumentException("Choose an executable entrypoint project, not a library or solution.");
            }

            projects.Add(new(file, properties.GetProperty("AssemblyName").GetString()!,
                properties.GetProperty("TargetPath").GetString()!));
            foreach (var reference in document.RootElement.GetProperty("Items").GetProperty("ProjectReference").EnumerateArray())
            {
                if (reference.TryGetProperty("ReferenceOutputAssembly", out var used)
                    && used.GetString()?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
                {
                    continue;
                }

                pending.Enqueue(Path.GetFullPath(reference.GetProperty("FullPath").GetString()!));
            }
        }

        return projects;
    }

    private static async Task Publish(
        ProjectInfo root,
        string directory,
        string runtime
    )
    {
        Directory.CreateDirectory(directory);
        var graphFile = Path.Combine(directory, "dependencies.xml");
        // Keep source calls visible instead of substituting constant-returning callees.
        var extra = $"--dump-dependencies --dependencies-file {Quote(graphFile)} --disable-opt ipconstprop";

        // Localize PublishTrimmed to the entry project. A global command-line property would also
        // apply it to netstandard analyzer/common projects and fail NETSDK1124.
        var settings = Path.Combine(directory, "audit.props");
        var condition = $"'$(MSBuildProjectFullPath)' == '{Escape(root.File)}'";
        new XElement("Project", new XElement("PropertyGroup", new XAttribute("Condition", condition),
            new XElement("PublishTrimmed", "true"), new XElement("TrimMode", "full"),
            new XElement("EnableTrimAnalyzer", "false"), new XElement("ILLinkTreatWarningsAsErrors", "false"),
            new XElement("TrimmerSingleWarn", "false"), new XElement("_ExtraTrimmerArgs", extra))).Save(settings);
        var output = await Execute([
            "publish", root.File, "-c", "Release", "-r", runtime, "--self-contained", "true",
            "--artifacts-path", directory, "-o", Path.Combine(directory, "publish"), "-v:q",
            $"-p:CustomBeforeMicrosoftCommonTargets={settings}"
        ]);
        var warnings = output.Split('\n').Where(line => line.Contains("warning IL", StringComparison.Ordinal)).ToArray();
        if (warnings.Length > 0)
        {
            throw new InvalidOperationException("Trimming analysis reported unresolved behavior:\n" + string.Join('\n', warnings));
        }

        if (!File.Exists(graphFile))
        {
            throw new InvalidOperationException("The SDK did not emit its dependency trace; analysis is incomplete.");
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("%", "%25", StringComparison.Ordinal).Replace("'", "%27", StringComparison.Ordinal);
    }

    private static string Quote(string value) => '"' + value + '"';

    private static async Task<string> Execute(string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await stdout;
        var errors = await stderr;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet exited {process.ExitCode}:\n{output}\n{errors}");
        }

        return output + errors;
    }

    private static Dictionary<string, MemberInfo> ReadMembers(
        List<ProjectInfo> projects,
        string binaries
    )
    {
        var members = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
        foreach (var project in projects)
        {
            var file = Path.Combine(binaries, project.Assembly + ".dll");
            using var stream = File.OpenRead(file);
            using var image = new PEReader(stream);
            var reader = image.GetMetadataReader();
            var pdbFile = Path.ChangeExtension(file, ".pdb");
            using var pdbStream = File.OpenRead(pdbFile);
            using var pdb = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
            ReadModule(members, reader, pdb.GetMetadataReader());
        }

        return members;
    }

    private static void ReadModule(
        Dictionary<string, MemberInfo> members,
        MetadataReader reader,
        MetadataReader symbols
    )
    {
        var locations = SourceLocations(symbols);
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = TypeName(reader, handle);
            var generated = GeneratedType(reader, handle);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var context = new SignatureContext(
                    [.. type.GetGenericParameters().Select(parameter => reader.GetString(reader.GetGenericParameter(parameter).Name))],
                    [.. method.GetGenericParameters().Select(parameter => reader.GetString(reader.GetGenericParameter(parameter).Name))]
                );
                var signature = method.DecodeSignature(new SignatureNames(), context);
                var key = $"Method:{signature.ReturnType} {name}::{reader.GetString(method.Name)}({string.Join(',', signature.ParameterTypes)})";
                locations.TryGetValue(methodHandle, out var location);
                // Local functions and lambdas have authored bodies despite compiler-generated names.
                var synthesized = Generated(
                    reader,
                    method.GetCustomAttributes(),
                    includeSynthesized: !reader.GetString(method.Name).StartsWith('<')
                );
                var info = new MemberInfo(generated || synthesized, location);
                if (!members.TryAdd(key, info))
                {
                    throw new InvalidOperationException($"Ambiguous method identity in SDK trace: {key}");
                }
            }
        }
    }

    private static Dictionary<MethodDefinitionHandle, SourceLocation> SourceLocations(MetadataReader symbols)
    {
        var locations = new Dictionary<MethodDefinitionHandle, SourceLocation>();
        foreach (var handle in symbols.MethodDebugInformation)
        {
            var information = symbols.GetMethodDebugInformation(handle);
            var location = FirstLocation(symbols, information);
            if (location is null)
            {
                continue;
            }

            var kickoff = information.GetStateMachineKickoffMethod();
            var method = kickoff.IsNil ? handle.ToDefinitionHandle() : kickoff;
            locations.TryAdd(method, location);
        }

        return locations;
    }

    private static SourceLocation? FirstLocation(MetadataReader symbols, MethodDebugInformation information)
    {
        foreach (var point in information.GetSequencePoints())
        {
            var document = point.Document.IsNil ? information.Document : point.Document;
            if (!point.IsHidden && !document.IsNil)
            {
                return new(symbols.GetString(symbols.GetDocument(document).Name), point.StartLine);
            }
        }

        return null;
    }

    private static bool Generated(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes,
        bool includeSynthesized = false
    )
    {
        foreach (var handle in attributes)
        {
            var constructor = reader.GetCustomAttribute(handle).Constructor;
            var parent = constructor.Kind == HandleKind.MemberReference
                ? reader.GetMemberReference((MemberReferenceHandle)constructor).Parent
                : reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType();
            var name = parent.Kind == HandleKind.TypeReference
                ? reader.GetString(reader.GetTypeReference((TypeReferenceHandle)parent).Name)
                : reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)parent).Name);
            if (name == "GeneratedCodeAttribute" || includeSynthesized && name == "CompilerGeneratedAttribute")
            {
                return true;
            }
        }

        return false;
    }

    private static string TypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var name = reader.GetString(type.Name);
        if (!type.GetDeclaringType().IsNil)
        {
            return TypeName(reader, type.GetDeclaringType()) + '/' + name;
        }

        var space = reader.GetString(type.Namespace);

        return space.Length == 0 ? name : space + '.' + name;
    }

    private static bool GeneratedType(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);

        return Generated(reader, type.GetCustomAttributes())
            || !type.GetDeclaringType().IsNil && GeneratedType(reader, type.GetDeclaringType());
    }

    private static HashSet<string> ReadRetained(string file)
    {
        var methods = new HashSet<string>(StringComparer.Ordinal);
        using var stream = File.OpenRead(file);
        using Stream decompressed = stream.ReadByte() == 0x1f ? OpenCompressed(stream) : Rewind(stream);
        using var xml = XmlReader.Create(decompressed, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "edge" && xml.GetAttribute("mark") == "1"
                && xml.GetAttribute("e") is { } target && target.StartsWith("Method:", StringComparison.Ordinal))
            {
                methods.Add(target);
            }
        }

        return methods;
    }

    private static GZipStream OpenCompressed(FileStream stream)
    {
        stream.Position = 0;

        return new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
    }

    private static FileStream Rewind(FileStream stream)
    {
        stream.Position = 0;

        return stream;
    }

    private sealed record SignatureContext(ImmutableArray<string> Types, ImmutableArray<string> Methods);

    private sealed class SignatureNames : ISignatureTypeProvider<string, SignatureContext>
    {
        public string GetArrayType(string elementType, ArrayShape shape)
        {
            var dimensions = Enumerable.Range(0, shape.Rank).Select(index =>
                index < shape.LowerBounds.Length ? $"{shape.LowerBounds[index]}..." : "");

            return $"{elementType}[{string.Join(',', dimensions)}]";
        }

        public string GetByReferenceType(string elementType) => elementType + '&';

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return $"method {signature.ReturnType} *({string.Join(',', signature.ParameterTypes)})";
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return $"{genericType}<{string.Join(',', typeArguments)}>";
        }

        public string GetGenericMethodParameter(SignatureContext genericContext, int index) => genericContext.Methods[index];

        public string GetGenericTypeParameter(SignatureContext genericContext, int index) => genericContext.Types[index];

        public string GetModifiedType(
            string modifier,
            string unmodifiedType,
            bool isRequired
        )
        {
            return $"{unmodifiedType} {(isRequired ? "modreq" : "modopt")}({modifier})";
        }

        public string GetPinnedType(string elementType) => elementType;

        public string GetPointerType(string elementType) => elementType + '*';

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind
        )
        {
            return TypeName(reader, handle);
        }

        public string GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind
        )
        {
            var type = reader.GetTypeReference(handle);
            var name = reader.GetString(type.Name);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return GetTypeFromReference(reader, (TypeReferenceHandle)type.ResolutionScope, rawTypeKind) + '/' + name;
            }

            var space = reader.GetString(type.Namespace);

            return space.Length == 0 ? name : space + '.' + name;
        }

        public string GetTypeFromSpecification(
            MetadataReader reader,
            SignatureContext genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind
        )
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }
    }

}
