using NUnit.Framework;
using PoeFilterX.IntegrationTests.Infrastructure;

namespace PoeFilterX.IntegrationTests.Build;

[Parallelizable(ParallelScope.All)]
public class BuildProjectTests
{
    public static IEnumerable<TestCaseData> SuccessCases => Cases("Success");

    public static IEnumerable<TestCaseData> FailureCases => Cases("Failure");

    private static IEnumerable<TestCaseData> Cases(string outcome)
    {
        var root = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Build",
            "Cases",
            outcome
        );
        return Directory.GetDirectories(root, "Input", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetDirectoryName(path)!)
            .Select(path => new TestCaseData(path)
                .SetName($"Build_{outcome}_{Path.GetRelativePath(root, path)}"));
    }

    [TestCaseSource(nameof(SuccessCases))]
    public async Task Build_matches_expected_project(string casePath)
    {
        using var workspace = TestWorkspace.FromCase(casePath);
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Zero);
            Assert.That(result.StandardError, Is.Empty);
        }
        workspace.AssertExpected(Path.Combine(casePath, "Expected"));
        workspace.AssertInputsUnchanged(Path.Combine(casePath, "Input"));
    }

    [TestCaseSource(nameof(FailureCases))]
    public async Task Build_rejects_invalid_project(string casePath)
    {
        using var workspace = TestWorkspace.FromCase(casePath);
        var result = await CliRunner.RunAsync(
            workspace.Root,
            ["build", "--path", "main.filterx"]
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.StandardError, Is.Not.Empty);
        }
        var diagnosticPath = Path.Combine(casePath, "error.txt");
        var code = File.ReadAllText(diagnosticPath).Trim();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(code, Does.Match(@"^PFX\d{4}$"), "Failure cases specify a stable diagnostic code.");
            Assert.That(result.StandardError, Does.Contain($"error {code}:"));
        }

        workspace.AssertInputsUnchanged(Path.Combine(casePath, "Input"));
    }
}
