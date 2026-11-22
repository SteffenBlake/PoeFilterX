using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Packages;
using PoeFilterX.Engine.Parsing;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;
using PoeFilterX.Engine.Tests.Parsing;

namespace PoeFilterX.Engine.Tests.PackageManagement;

[Parallelizable(ParallelScope.All)]
public class PackageManifestTests
{
    [TestCase("https://github.com/owner/theme.git")]
    [TestCase("http://localhost:1234/theme.git")]
    [TestCase("ssh://git@github.com/owner/theme.git")]
    [TestCase("git@github.com:owner/theme.git")]
    public void Imports_preserve_alias_URL_pin_and_source_locations(string repository)
    {
        var commit = new string('A', 40);
        var tree = Parse($"import theme from \"{repository}\" at \"{commit}\";\nusing @theme/styles/main.fss;");
        var manifest = PackageManifest.Read(tree);
        var declaration = (PackageImportSyntax)tree.Root.Statements[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.Diagnostics, Is.Empty);
            Assert.That(manifest.Diagnostics, Is.Empty);
            Assert.That(manifest.References, Has.Length.EqualTo(1));
            Assert.That(manifest.References[0].Alias, Is.EqualTo("theme"));
            Assert.That(manifest.References[0].Repository, Is.EqualTo(repository));
            Assert.That(manifest.References[0].RequestedCommit, Is.EqualTo(commit.ToLowerInvariant()));
            Assert.That(tree.Source.Slice(declaration.Repository.Span).ToString(), Is.EqualTo($"\"{repository}\""));
            Assert.That(((ImportSyntax)tree.Root.Statements[1]).Path.Value, Is.EqualTo("@theme/styles/main.fss"));
        }
    }

    [TestCase("import theme from \"file:///local/repo\";")]
    [TestCase("import theme from \"ext::malicious command\";")]
    [TestCase("import theme from \"https://user:password@host/repo\";")]
    [TestCase("import theme from \"https://ghp_token@host/repo\";")]
    [TestCase("import theme from \"https://host/repo?token=secret\";")]
    [TestCase("import theme from \"https://host/repo\" at \"main\";")]
    [TestCase("import \"theme\" from \"https://host/repo\";")]
    [TestCase("import 123 from \"https://host/repo\";")]
    [TestCase("import theme from \"https://host/one\"; import theme from \"https://host/two\";")]
    public void Invalid_declarations_return_diagnostics(string text)
    {
        Assert.That(PackageManifest.Read(Parse(text)).Diagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.InvalidPackage));
    }

    [Test]
    public void Incomplete_package_declarations_recover_without_throwing_at_every_character()
    {
        const string text = "import theme from \"https://host/repo.git\" at \"0123456789012345678901234567890123456789\";";
        for (var length = 0; length <= text.Length; length++)
        {
            var tree = Parse(text[..length]);
            Assert.That(tree.Source.Text, Is.EqualTo(text[..length]));
            Assert.DoesNotThrow(() => PackageManifest.Read(tree));
        }
    }

    [Test]
    public void Nested_declarations_are_rejected()
    {
        var manifest = PackageManifest.Read(Parse("{ { import theme from \"https://host/repo\"; } }"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(manifest.References, Is.Empty);
            Assert.That(manifest.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo([DiagnosticCodes.PackageScope]));
        }
    }

    [Test]
    public void Alias_resolution_reports_unknown_packages_but_defers_unrestored_files()
    {
        var manifest = PackageManifest.Read(Parse("import theme from \"https://host/repo\";"));
        var from = Path.GetFullPath("entrypoint.filterx");
        var deferred = PackagePaths.Resolve("@theme/main.fss", from, manifest) is not ParseError;
        var missing = PackagePaths.Resolve("@missing/main.fss", from, manifest) is ParseError;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(deferred, Is.True);
            Assert.That(missing, Is.True);
        }
    }

    private static SyntaxTree Parse(string text)
    {
        using var reader = new StringReader(text);
        return ParsedSyntaxTree.From(FilterXParserService.Parse(reader, Path.GetFullPath("entrypoint.filterx")));
    }
}
