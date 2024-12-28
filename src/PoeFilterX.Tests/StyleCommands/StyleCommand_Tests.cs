using PoeFilterX.Business;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        [TestCase($"UNSUPPORTED_COMMAND:")]
        public void Alert_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [Test]
        public void Alert_Empty_ReturnsNull()
        {
            Assert.That(Parser.Parse(""), Is.Null);
        }
    }
}
