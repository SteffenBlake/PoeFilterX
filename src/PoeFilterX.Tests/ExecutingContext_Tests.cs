using PoeFilterX.Business.Models;

namespace PoeFilterX.Tests
{
    public class ExecutingContext_Tests
    {
        public ExecutingContext ExecutingContext { get; set; } = new();

        [SetUp]
        public void Setup()
        {
            ExecutingContext = new ExecutingContext();
        }

        [Test]
        public void TryAddUsing_CircularDependencyCheck()
        {
            Assert.That(ExecutingContext.TryAddUsing("A", "B"), Is.True);
            Assert.That(ExecutingContext.TryAddUsing("A", "C"), Is.True);
            Assert.That(ExecutingContext.TryAddUsing("B", "C"), Is.True);
            Assert.That(ExecutingContext.TryAddUsing("C", "D"), Is.True);
            Assert.That(ExecutingContext.TryAddUsing("C", "E"), Is.True);
            Assert.That(ExecutingContext.TryAddUsing("A", "E"), Is.True);

            // Circular Dependency, B=>C=>E=>B
            Assert.That(ExecutingContext.TryAddUsing("E", "B"), Is.False);

            Assert.That(ExecutingContext.TryAddUsing("B", "E"), Is.True);

        }



    }
}
