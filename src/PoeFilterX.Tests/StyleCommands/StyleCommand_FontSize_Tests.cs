using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_FontSize_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "font-size";

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
        [TestCase($"{COMMAND}: 101")]
        [TestCase($"{COMMAND}: -1")]
        [TestCase($"{COMMAND}: 17")]
        [TestCase($"{COMMAND}: null")]
        [TestCase($"{COMMAND}: 255 255 255 255 255")]
        public void FontSize_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: 18", ExpectedResult = 18)]
        [TestCase($"{COMMAND}: 32", ExpectedResult = 32)]
        [TestCase($"{COMMAND}: 45", ExpectedResult = 45)]
        public int FontSize_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetFontSize);
            return filterblock.SetFontSize.Value;
        }

    }
}
