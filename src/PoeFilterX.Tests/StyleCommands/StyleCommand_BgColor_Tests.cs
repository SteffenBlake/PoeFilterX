using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using System.Drawing;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_BgColor_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "bg-color";

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
        [TestCase($"{COMMAND}: 300")]
        [TestCase($"{COMMAND}: -1")]
        [TestCase($"{COMMAND}: null")]
        [TestCase($"{COMMAND}: 255 255 255 255 255")]
        public void BgColor_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: 0xFFFFFFFF", ExpectedResult = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        [TestCase($"{COMMAND}: 0xFFFFFF",   ExpectedResult = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        [TestCase($"{COMMAND}: 0x123456",   ExpectedResult = new byte[] { 0xFF, 0x12, 0x34, 0x56 })]
        [TestCase($"{COMMAND}: 0x12345678", ExpectedResult = new byte[] { 0x12, 0x34, 0x56, 0x78 })]
        [TestCase($"{COMMAND}: 0xFF000000", ExpectedResult = new byte[] { 0xFF, 0x00, 0x00, 0x00 })]
        [TestCase($"{COMMAND}: 0x00FF0000", ExpectedResult = new byte[] { 0x00, 0xFF, 0x00, 0x00 })]
        [TestCase($"{COMMAND}: 0x0000FF00", ExpectedResult = new byte[] { 0x00, 0x00, 0xFF, 0x00 })]
        [TestCase($"{COMMAND}: 0x000000FF", ExpectedResult = new byte[] { 0x00, 0x00, 0x00, 0xFF })]
        public byte[] BgColor_HexValues(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] { 
                filterblock.SetBackgroundColor.Value.A, 
                filterblock.SetBackgroundColor.Value.R, 
                filterblock.SetBackgroundColor.Value.G, 
                filterblock.SetBackgroundColor.Value.B, 
            };
        }

        [TestCase($"{COMMAND}: 255 255 255 255",   ExpectedResult = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        [TestCase($"{COMMAND}: 255 255 255",       ExpectedResult = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
        [TestCase($"{COMMAND}: 50 60 70",          ExpectedResult = new byte[] { 50, 60, 70, 255 })]
        [TestCase($"{COMMAND}: 50 60 70 80",       ExpectedResult = new byte[] { 50, 60, 70, 80 })]
        [TestCase($"{COMMAND}: 0 0 0 0",           ExpectedResult = new byte[] { 0, 0, 0, 0 })]
        [TestCase($"{COMMAND}: 00 00 00 00",       ExpectedResult = new byte[] { 0, 0, 0, 0 })]
        [TestCase($"{COMMAND}: 00000 0000 000 00", ExpectedResult = new byte[] { 0, 0, 0, 0 })]
        [TestCase($"{COMMAND}: 00001 0001 001 01", ExpectedResult = new byte[] { 1, 1, 1, 1 })]
        [TestCase($"{COMMAND}: 100 0 0 0",         ExpectedResult = new byte[] { 100, 0, 0, 0 })]
        [TestCase($"{COMMAND}: 0 100 0 0",         ExpectedResult = new byte[] { 0, 100, 0, 0 })]
        [TestCase($"{COMMAND}: 0 0 100 0",         ExpectedResult = new byte[] { 0, 0, 100, 0 })]
        [TestCase($"{COMMAND}: 0 0 0 100",         ExpectedResult = new byte[] { 0, 0, 0, 100 })]
        [TestCase($"{COMMAND}: 100",               ExpectedResult = new byte[] { 100, 0, 0, 255 })]
        [TestCase($"{COMMAND}: 100 100",           ExpectedResult = new byte[] { 100, 100, 0, 255 })]
        public byte[] BgColor_ARGBValues(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] {
                filterblock.SetBackgroundColor.Value.R,
                filterblock.SetBackgroundColor.Value.G,
                filterblock.SetBackgroundColor.Value.B,
                filterblock.SetBackgroundColor.Value.A,
            };
        }

        [TestCase($"{COMMAND}-alpha:")]
        [TestCase($"{COMMAND}-alpha: ")]
        [TestCase($"{COMMAND}-alpha: #42143")]
        [TestCase($"{COMMAND}-alpha: 0x")]
        [TestCase($"{COMMAND}-alpha: 0x1")]
        [TestCase($"{COMMAND}-alpha: 0x12")]
        [TestCase($"{COMMAND}-alpha: 0x123")]
        [TestCase($"{COMMAND}-alpha: 0x1234")]
        [TestCase($"{COMMAND}-alpha: 0x12345")]
        [TestCase($"{COMMAND}-alpha: 0x1234567")]
        [TestCase($"{COMMAND}-alpha: 0x123456789")]
        [TestCase($"{COMMAND}-alpha: 0x1234567890")]
        [TestCase($"{COMMAND}-alpha: FFFFFF")]
        [TestCase($"{COMMAND}-alpha: 300")]
        [TestCase($"{COMMAND}-alpha: -1")]
        [TestCase($"{COMMAND}-alpha: null")]
        [TestCase($"{COMMAND}-alpha: 255 255 255 255 255")]
        public void BgColorAlpha_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-alpha: 0",    ExpectedResult = new byte[] { 100, 100, 100, 0 })]
        [TestCase($"{COMMAND}-alpha: 100",    ExpectedResult = new byte[] { 100, 100, 100, 100 })]
        [TestCase($"{COMMAND}-alpha: 255",  ExpectedResult = new byte[] { 100, 100, 100, 255 })]
        public byte[] BgColorAlpha_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                SetBackgroundColor = Color.FromArgb(50, 100, 100, 100)
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] {
                filterblock.SetBackgroundColor.Value.R,
                filterblock.SetBackgroundColor.Value.G,
                filterblock.SetBackgroundColor.Value.B,
                filterblock.SetBackgroundColor.Value.A,
            };
        }

        [TestCase($"{COMMAND}-red:")]
        [TestCase($"{COMMAND}-red: ")]
        [TestCase($"{COMMAND}-red: #42143")]
        [TestCase($"{COMMAND}-red: 0x")]
        [TestCase($"{COMMAND}-red: 0x1")]
        [TestCase($"{COMMAND}-red: 0x12")]
        [TestCase($"{COMMAND}-red: 0x123")]
        [TestCase($"{COMMAND}-red: 0x1234")]
        [TestCase($"{COMMAND}-red: 0x12345")]
        [TestCase($"{COMMAND}-red: 0x1234567")]
        [TestCase($"{COMMAND}-red: 0x123456789")]
        [TestCase($"{COMMAND}-red: 0x1234567890")]
        [TestCase($"{COMMAND}-red: FFFFFF")]
        [TestCase($"{COMMAND}-red: 300")]
        [TestCase($"{COMMAND}-red: -1")]
        [TestCase($"{COMMAND}-red: null")]
        [TestCase($"{COMMAND}-red: 255 255 255 255 255")]
        public void BgColorRed_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-red: 0", ExpectedResult = new byte[] { 0, 100, 100, 100 })]
        [TestCase($"{COMMAND}-red: 100", ExpectedResult = new byte[] { 100, 100, 100, 100 })]
        [TestCase($"{COMMAND}-red: 255", ExpectedResult = new byte[] { 255, 100, 100, 100 })]
        public byte[] BgColorRed_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                SetBackgroundColor = Color.FromArgb(100, 50, 100, 100)
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] {
                filterblock.SetBackgroundColor.Value.R,
                filterblock.SetBackgroundColor.Value.G,
                filterblock.SetBackgroundColor.Value.B,
                filterblock.SetBackgroundColor.Value.A,
            };
        }

        [TestCase($"{COMMAND}-green:")]
        [TestCase($"{COMMAND}-green: ")]
        [TestCase($"{COMMAND}-green: #42143")]
        [TestCase($"{COMMAND}-green: 0x")]
        [TestCase($"{COMMAND}-green: 0x1")]
        [TestCase($"{COMMAND}-green: 0x12")]
        [TestCase($"{COMMAND}-green: 0x123")]
        [TestCase($"{COMMAND}-green: 0x1234")]
        [TestCase($"{COMMAND}-green: 0x12345")]
        [TestCase($"{COMMAND}-green: 0x1234567")]
        [TestCase($"{COMMAND}-green: 0x123456789")]
        [TestCase($"{COMMAND}-green: 0x1234567890")]
        [TestCase($"{COMMAND}-green: FFFFFF")]
        [TestCase($"{COMMAND}-green: 300")]
        [TestCase($"{COMMAND}-green: -1")]
        [TestCase($"{COMMAND}-green: null")]
        [TestCase($"{COMMAND}-green: 255 255 255 255 255")]
        public void BgColorGreen_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-green: 0", ExpectedResult = new byte[] { 100, 0, 100, 100 })]
        [TestCase($"{COMMAND}-green: 100", ExpectedResult = new byte[] { 100, 100, 100, 100 })]
        [TestCase($"{COMMAND}-green: 255", ExpectedResult = new byte[] { 100, 255, 100, 100 })]
        public byte[] BgColorGreen_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                SetBackgroundColor = Color.FromArgb(100, 100, 50, 100)
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] {
                filterblock.SetBackgroundColor.Value.R,
                filterblock.SetBackgroundColor.Value.G,
                filterblock.SetBackgroundColor.Value.B,
                filterblock.SetBackgroundColor.Value.A,
            };
        }

        [TestCase($"{COMMAND}-blue:")]
        [TestCase($"{COMMAND}-blue: ")]
        [TestCase($"{COMMAND}-blue: #42143")]
        [TestCase($"{COMMAND}-blue: 0x")]
        [TestCase($"{COMMAND}-blue: 0x1")]
        [TestCase($"{COMMAND}-blue: 0x12")]
        [TestCase($"{COMMAND}-blue: 0x123")]
        [TestCase($"{COMMAND}-blue: 0x1234")]
        [TestCase($"{COMMAND}-blue: 0x12345")]
        [TestCase($"{COMMAND}-blue: 0x1234567")]
        [TestCase($"{COMMAND}-blue: 0x123456789")]
        [TestCase($"{COMMAND}-blue: 0x1234567890")]
        [TestCase($"{COMMAND}-blue: FFFFFF")]
        [TestCase($"{COMMAND}-blue: 300")]
        [TestCase($"{COMMAND}-blue: -1")]
        [TestCase($"{COMMAND}-blue: null")]
        [TestCase($"{COMMAND}-blue: 255 255 255 255 255")]
        public void BgColorBlue_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-blue: 0", ExpectedResult = new byte[] { 100, 100, 0, 100 })]
        [TestCase($"{COMMAND}-blue: 100", ExpectedResult = new byte[] { 100, 100, 100, 100 })]
        [TestCase($"{COMMAND}-blue: 255", ExpectedResult = new byte[] { 100, 100, 255, 100 })]
        public byte[] BgColorBlue_Values(string args)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                SetBackgroundColor = Color.FromArgb(100, 100, 100, 50)
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.SetBackgroundColor);
            return new[] {
                filterblock.SetBackgroundColor.Value.R,
                filterblock.SetBackgroundColor.Value.G,
                filterblock.SetBackgroundColor.Value.B,
                filterblock.SetBackgroundColor.Value.A,
            };
        }
    }


}
