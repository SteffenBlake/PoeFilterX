using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_DropSound_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "drop-sound";

        [TestCase($"{COMMAND}:")]
        [TestCase($"{COMMAND}: ")]
        [TestCase($"{COMMAND}: #42143")]
        [TestCase($"{COMMAND}: 0x")]
        [TestCase($"{COMMAND}: 0x1")]
        [TestCase($"{COMMAND}: 0x12")]
        [TestCase($"{COMMAND}: 0x123")]
        [TestCase($"{COMMAND}: 0x1234")]
        [TestCase($"{COMMAND}: 0x12345")]
        [TestCase($"{COMMAND}: 0x1234567")]
        [TestCase($"{COMMAND}: 0x123456789")]
        [TestCase($"{COMMAND}: 0x1234567890")]
        [TestCase($"{COMMAND}: FFFFFF")]
        [TestCase($"{COMMAND}: 46")]
        [TestCase($"{COMMAND}: -1")]
        [TestCase($"{COMMAND}: 17")]
        [TestCase($"{COMMAND}: null")]
        [TestCase($"{COMMAND}: 255 255 255 255 255")]
        public void DropSound_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: enabled", ExpectedResult = true)]
        [TestCase($"{COMMAND}: disabled", ExpectedResult = false)]
        public bool DropSound_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.DropSound);
            return filterblock.DropSound.Value;
        }

    }
}
