using PoeFilterX.Business;
using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_Pillar_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "pillar";

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
        [TestCase($"{COMMAND}: -1")]
        [TestCase($"{COMMAND}: 3")]
        [TestCase($"{COMMAND}: null")]
        [TestCase($"{COMMAND}: 255 255 255 255 255")]
        [TestCase($"{COMMAND}: burger")]
        [TestCase($"{COMMAND}: red burger")]
        [TestCase($"{COMMAND}: red permanent burger")]
        public void Pillar_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: red", FilterColor.Blue, false, false, ExpectedResult = new object[] {FilterColor.Red, false, false})]
        [TestCase($"{COMMAND}: red", FilterColor.Blue, false, true, ExpectedResult = new object[] {FilterColor.Red, false, true })]
        [TestCase($"{COMMAND}: red", FilterColor.Blue, true, false, ExpectedResult = new object[] {FilterColor.Red, true, false })]
        [TestCase($"{COMMAND}: red", FilterColor.Blue, true, true, ExpectedResult = new object[] {FilterColor.Red, true, true })]
        [TestCase($"{COMMAND}: red permanent", FilterColor.Blue, false, false, ExpectedResult = new object[] { FilterColor.Red, false, false })]
        [TestCase($"{COMMAND}: red temporary", FilterColor.Blue, false, true, ExpectedResult = new object[] { FilterColor.Red, true, true })]
        [TestCase($"{COMMAND}: red permanent", FilterColor.Blue, true, false, ExpectedResult = new object[] { FilterColor.Red, false, false })]
        [TestCase($"{COMMAND}: red temporary", FilterColor.Blue, true, true, ExpectedResult = new object[] { FilterColor.Red, true, true })]
        [TestCase($"{COMMAND}: enabled", FilterColor.Blue, false, false, ExpectedResult = new object[] { FilterColor.Blue, false, true })]
        [TestCase($"{COMMAND}: enabled", FilterColor.Blue, false, true, ExpectedResult = new object[] { FilterColor.Blue, false, true })]
        [TestCase($"{COMMAND}: disabled", FilterColor.Blue, true, false, ExpectedResult = new object[] { FilterColor.Blue, true, false })]
        [TestCase($"{COMMAND}: disabled", FilterColor.Blue, true, true, ExpectedResult = new object[] { FilterColor.Blue, true, false })]
        public object[] Pillar_Values(string args, FilterColor color, bool temporary, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                PlayEffect = new PlayEffect
                {
                    Color = color,
                    Temporary = temporary,
                    Enabled = enabled
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.PlayEffect);
            return new object[]
            {
                filterblock.PlayEffect.Color,
                filterblock.PlayEffect.Temporary,
                filterblock.PlayEffect.Enabled,
            };
        }

        [TestCase($"{COMMAND}-color:")]
        [TestCase($"{COMMAND}-color: ")]
        [TestCase($"{COMMAND}-color: #42143")]
        [TestCase($"{COMMAND}-color: 0x")]
        [TestCase($"{COMMAND}-color: 0x1")]
        [TestCase($"{COMMAND}-color: 0x12")]
        [TestCase($"{COMMAND}-color: 0x123")]
        [TestCase($"{COMMAND}-color: 0x1234")]
        [TestCase($"{COMMAND}-color: 0x12345")]
        [TestCase($"{COMMAND}-color: 0x1234567")]
        [TestCase($"{COMMAND}-color: 0x123456789")]
        [TestCase($"{COMMAND}-color: 0x1234567890")]
        [TestCase($"{COMMAND}-color: FFFFFF")]
        [TestCase($"{COMMAND}-color: -1")]
        [TestCase($"{COMMAND}-color: 3")]
        [TestCase($"{COMMAND}-color: null")]
        [TestCase($"{COMMAND}-color: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-color: burger")]
        [TestCase($"{COMMAND}-color: red burger")]
        [TestCase($"{COMMAND}-color: red permanent burger")]
        public void PillarColor_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-color: red", FilterColor.Blue, false, false, ExpectedResult = new object[] { FilterColor.Red, false, false })]
        [TestCase($"{COMMAND}-color: red", FilterColor.Blue, false, true, ExpectedResult = new object[] { FilterColor.Red, false, true })]
        [TestCase($"{COMMAND}-color: red", FilterColor.Blue, true, false, ExpectedResult = new object[] { FilterColor.Red, true, false })]
        [TestCase($"{COMMAND}-color: red", FilterColor.Blue, true, true, ExpectedResult = new object[] { FilterColor.Red, true, true })]
        public object[] PillarColor_Values_PreExisting(string args, FilterColor color, bool temporary, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                PlayEffect = new PlayEffect
                {
                    Color = color,
                    Temporary = temporary,
                    Enabled = enabled
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.PlayEffect);
            return new object[]
            {
                filterblock.PlayEffect.Color,
                filterblock.PlayEffect.Temporary,
                filterblock.PlayEffect.Enabled,
            };
        }

        [TestCase($"{COMMAND}-color: red", ExpectedResult = new object?[] { FilterColor.Red, false, true })]
        public object[] PillarColor_Values_NoPreExisting(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.PlayEffect);
            return new object[]
            {
                filterblock.PlayEffect.Color,
                filterblock.PlayEffect.Temporary,
                filterblock.PlayEffect.Enabled,
            };
        }

        [TestCase($"{COMMAND}-duration:")]
        [TestCase($"{COMMAND}-duration: ")]
        [TestCase($"{COMMAND}-duration: #42143")]
        [TestCase($"{COMMAND}-duration: 0x")]
        [TestCase($"{COMMAND}-duration: 0x1")]
        [TestCase($"{COMMAND}-duration: 0x12")]
        [TestCase($"{COMMAND}-duration: 0x123")]
        [TestCase($"{COMMAND}-duration: 0x1234")]
        [TestCase($"{COMMAND}-duration: 0x12345")]
        [TestCase($"{COMMAND}-duration: 0x1234567")]
        [TestCase($"{COMMAND}-duration: 0x123456789")]
        [TestCase($"{COMMAND}-duration: 0x1234567890")]
        [TestCase($"{COMMAND}-duration: FFFFFF")]
        [TestCase($"{COMMAND}-duration: -1")]
        [TestCase($"{COMMAND}-duration: 3")]
        [TestCase($"{COMMAND}-duration: null")]
        [TestCase($"{COMMAND}-duration: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-duration: burger")]
        [TestCase($"{COMMAND}-duration: red burger")]
        [TestCase($"{COMMAND}-duration: red permanent burger")]
        public void PillarDuration_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-duration: permanent", FilterColor.Blue, false, false, ExpectedResult = new object[] { FilterColor.Blue, false, false })]
        [TestCase($"{COMMAND}-duration: temporary", FilterColor.Blue, false, true, ExpectedResult = new object[] { FilterColor.Blue, true, true })]
        [TestCase($"{COMMAND}-duration: permanent", FilterColor.Blue, true, false, ExpectedResult = new object[] { FilterColor.Blue, false, false })]
        [TestCase($"{COMMAND}-duration: temporary", FilterColor.Blue, true, true, ExpectedResult = new object[] { FilterColor.Blue, true, true })]
        public object[] PillarDuration_Values_PreExisting(string args, FilterColor color, bool temporary, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                PlayEffect = new PlayEffect
                {
                    Color = color,
                    Temporary = temporary,
                    Enabled = enabled
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.PlayEffect);
            return new object[]
            {
                filterblock.PlayEffect.Color,
                filterblock.PlayEffect.Temporary,
                filterblock.PlayEffect.Enabled,
            };
        }

        [TestCase($"{COMMAND}-duration: permanent", ExpectedResult = new object?[] { null, false, true })]
        [TestCase($"{COMMAND}-duration: temporary", ExpectedResult = new object?[] { null, true, true })]
        public object[] PillarDuration_Values_NoPreExisting(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.PlayEffect);
            return new object[]
            {
                filterblock.PlayEffect.Color,
                filterblock.PlayEffect.Temporary,
                filterblock.PlayEffect.Enabled,
            };
        }
    }
}
