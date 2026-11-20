using Microsoft.Extensions.Logging;
using PoeFilterX.Engine.Logging;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Tests.Projects;

namespace PoeFilterX.Engine.Tests.Logging;

[Parallelizable(ParallelScope.All)]
public class ProjectLoggingTests
{
    [Test]
    public void Project_loader_uses_the_callers_logger_and_logs_cached_imports_at_trace()
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("logging-tests/main.filterx");
        source.Add(path, "using child.filterx; using child.filterx;");
        source.Add(Path.GetFullPath("logging-tests/child.filterx"), "ItemLevel 80;");
        var logger = new RecordingLogger();
        var project = new ProjectLoader(source, logger).Load(path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(project.Diagnostics, Is.Empty);
            var opening = logger.Entries.First(entry => entry.Event.Id == (int)CompilerLogEvent.OpeningFile);
            Assert.That(opening.Properties.Single(property => property.Key == "Path").Value, Is.EqualTo(path));
            Assert.That(logger.Entries.Count(entry => entry.Event.Id == (int)CompilerLogEvent.OpeningFile), Is.EqualTo(2));
            var cached = logger.Entries.Single(entry => entry.Event.Id == (int)CompilerLogEvent.CachedFile);
            Assert.That(cached.Level, Is.EqualTo(LogLevel.Trace));
            Assert.That(logger.Entries.Where(entry => entry.Event.Id == (int)CompilerLogEvent.OpeningFile)
                .Select(entry => entry.Level), Is.All.EqualTo(LogLevel.Debug));
        }
    }
}
