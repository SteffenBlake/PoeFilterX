using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PoeFilterX.Common;
using PoeFilterX.Generator;

namespace PoeFilterX.Engine.Tests.Generation;

[Parallelizable(ParallelScope.All)]
public class GeneratorCachingTests
{
    [Test]
    public void Unrelated_edits_reuse_models_and_property_edits_invalidate_them()
    {
        const string text = """
            using PoeFilterX.Common;
            [OperationModel("Codec")]
            public record Model { public int? Count { get; init; } }
            """;
        var model = CSharpSyntaxTree.ParseText(text);
        var unrelated = CSharpSyntaxTree.ParseText("public class Unrelated { }");
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = platform.Append(typeof(OperationModelAttribute).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "GeneratorCacheTest",
            [model, unrelated],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new PoeFilterXGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true
            )
        );
        driver = driver.RunGenerators(compilation);
        var original = driver.GetRunResult().GeneratedTrees.Single().ToString();
        compilation = compilation.ReplaceSyntaxTree(
            unrelated,
            CSharpSyntaxTree.ParseText("public class Unrelated { int Field; }")
        );
        driver = driver.RunGenerators(compilation);
        var cached = driver.GetRunResult();
        var reasons = cached.Results.Single().TrackedSteps["OperationModels"]
            .SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cached.Diagnostics, Is.Empty);
            Assert.That(cached.GeneratedTrees.Single().ToString(), Is.EqualTo(original));
            Assert.That(reasons, Is.All.AnyOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged));
        }

        var equivalentModel = CSharpSyntaxTree.ParseText(text);
        compilation = compilation.ReplaceSyntaxTree(model, equivalentModel);
        driver = driver.RunGenerators(compilation);
        var equivalent = driver.GetRunResult();
        var equivalentReasons = equivalent.Results.Single().TrackedSteps["OperationModels"]
            .SelectMany(step => step.Outputs).Select(output => output.Reason).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(equivalent.GeneratedTrees.Single().ToString(), Is.EqualTo(original));
            Assert.That(equivalentReasons, Is.All.EqualTo(IncrementalStepRunReason.Unchanged));
        }

        compilation = compilation.ReplaceSyntaxTree(
            equivalentModel,
            CSharpSyntaxTree.ParseText(text.Replace("Count", "Amount"))
        );
        driver = driver.RunGenerators(compilation);
        var changed = driver.GetRunResult();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.Diagnostics, Is.Empty);
            Assert.That(changed.GeneratedTrees.Single().ToString(), Does.Contain("Amount = value"));
            Assert.That(changed.GeneratedTrees.Single().ToString(), Is.Not.EqualTo(original));
        }
    }
}
