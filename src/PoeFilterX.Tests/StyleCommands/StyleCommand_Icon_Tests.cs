using PoeFilterX.Business;
using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_Icon_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "icon";

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
        [TestCase($"{COMMAND}: 2 burger")]
        [TestCase($"{COMMAND}: 2 red burger")]
        [TestCase($"{COMMAND}: 2 red circle burger")]
        public void Icon_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: 1", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] {1, FilterColor.Blue, MiniMapIconShape.Moon, false})]
        [TestCase($"{COMMAND}: 1 red", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] {1, FilterColor.Red, MiniMapIconShape.Moon, false})]
        [TestCase($"{COMMAND}: 1 red circle", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] {1, FilterColor.Red, MiniMapIconShape.Circle, false})]
        [TestCase($"{COMMAND}: 1", 2, FilterColor.Blue, MiniMapIconShape.Moon, true, ExpectedResult = new object[] { 1, FilterColor.Blue, MiniMapIconShape.Moon, true })]
        [TestCase($"{COMMAND}: 1 red", 2, FilterColor.Blue, MiniMapIconShape.Moon, true, ExpectedResult = new object[] { 1, FilterColor.Red, MiniMapIconShape.Moon, true })]
        [TestCase($"{COMMAND}: 1 red circle", 2, FilterColor.Blue, MiniMapIconShape.Moon, true, ExpectedResult = new object[] { 1, FilterColor.Red, MiniMapIconShape.Circle, true })]
        [TestCase($"{COMMAND}: enabled", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] { 2, FilterColor.Blue, MiniMapIconShape.Moon, true })]
        [TestCase($"{COMMAND}: disabled", 2, FilterColor.Blue, MiniMapIconShape.Moon, true, ExpectedResult = new object[] { 2, FilterColor.Blue, MiniMapIconShape.Moon, false })]
        public object[] Icon_Values(string args, int size, FilterColor color, MiniMapIconShape shape, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock();
            filterblock.MinimapIcon = new MiniMapIcon
            {
                Size = size,
                Color = color,
                Shape = shape,
                Enabled = enabled
            };

            // Act
            var style = Parser.Parse(args);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.MinimapIcon);
            return new object[]
            {
                filterblock.MinimapIcon.Size,
                filterblock.MinimapIcon.Color,
                filterblock.MinimapIcon.Shape,
                filterblock.MinimapIcon.Enabled,
            };
        }

        [TestCase($"{COMMAND}-size:")]
        [TestCase($"{COMMAND}-size: ")]
        [TestCase($"{COMMAND}-size: #42143")]
        [TestCase($"{COMMAND}-size: 0x")]
        [TestCase($"{COMMAND}-size: 0x1")]
        [TestCase($"{COMMAND}-size: 0x12")]
        [TestCase($"{COMMAND}-size: 0x123")]
        [TestCase($"{COMMAND}-size: 0x1234")]
        [TestCase($"{COMMAND}-size: 0x12345")]
        [TestCase($"{COMMAND}-size: 0x1234567")]
        [TestCase($"{COMMAND}-size: 0x123456789")]
        [TestCase($"{COMMAND}-size: 0x1234567890")]
        [TestCase($"{COMMAND}-size: FFFFFF")]
        [TestCase($"{COMMAND}-size: -1")]
        [TestCase($"{COMMAND}-size: 3")]
        [TestCase($"{COMMAND}-size: null")]
        [TestCase($"{COMMAND}-size: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-size: 2 burger")]
        [TestCase($"{COMMAND}-size: 2 red")]
        [TestCase($"{COMMAND}-size: 2 red circle")]
        public void IconSize_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-size: 1", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] { 1, FilterColor.Blue, MiniMapIconShape.Moon, false })]
        public object[] IconSize_Values(string args, int size, FilterColor color, MiniMapIconShape shape, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock();
            filterblock.MinimapIcon = new MiniMapIcon
            {
                Size = size,
                Color = color,
                Shape = shape,
                Enabled = enabled
            };

            // Act
            var style = Parser.Parse(args);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.MinimapIcon);
            return new object[]
            {
                filterblock.MinimapIcon.Size,
                filterblock.MinimapIcon.Color,
                filterblock.MinimapIcon.Shape,
                filterblock.MinimapIcon.Enabled,
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
        [TestCase($"{COMMAND}-color: 2 burger")]
        [TestCase($"{COMMAND}-color: red circle")]
        public void IconColor_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-color: red", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] { 2, FilterColor.Red, MiniMapIconShape.Moon, false })]
        public object[] IconColor_Values(string args, int size, FilterColor color, MiniMapIconShape shape, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock();
            filterblock.MinimapIcon = new MiniMapIcon
            {
                Size = size,
                Color = color,
                Shape = shape,
                Enabled = enabled
            };

            // Act
            var style = Parser.Parse(args);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.MinimapIcon);
            return new object[]
            {
                filterblock.MinimapIcon.Size,
                filterblock.MinimapIcon.Color,
                filterblock.MinimapIcon.Shape,
                filterblock.MinimapIcon.Enabled,
            };
        }

        [TestCase($"{COMMAND}-shape:")]
        [TestCase($"{COMMAND}-shape: ")]
        [TestCase($"{COMMAND}-shape: #42143")]
        [TestCase($"{COMMAND}-shape: 0x")]
        [TestCase($"{COMMAND}-shape: 0x1")]
        [TestCase($"{COMMAND}-shape: 0x12")]
        [TestCase($"{COMMAND}-shape: 0x123")]
        [TestCase($"{COMMAND}-shape: 0x1234")]
        [TestCase($"{COMMAND}-shape: 0x12345")]
        [TestCase($"{COMMAND}-shape: 0x1234567")]
        [TestCase($"{COMMAND}-shape: 0x123456789")]
        [TestCase($"{COMMAND}-shape: 0x1234567890")]
        [TestCase($"{COMMAND}-shape: FFFFFF")]
        [TestCase($"{COMMAND}-shape: -1")]
        [TestCase($"{COMMAND}-shape: 3")]
        [TestCase($"{COMMAND}-shape: null")]
        [TestCase($"{COMMAND}-shape: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-shape: 2 burger")]
        [TestCase($"{COMMAND}-shape: red circle")]
        public void IconShape_InvalidArgs_Throws(string args)
        {
            // Assert
            Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-shape: star", 2, FilterColor.Blue, MiniMapIconShape.Moon, false, ExpectedResult = new object[] { 2, FilterColor.Blue, MiniMapIconShape.Star, false })]
        public object[] IconShape_Values(string args, int size, FilterColor color, MiniMapIconShape shape, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock();
            filterblock.MinimapIcon = new MiniMapIcon
            {
                Size = size,
                Color = color,
                Shape = shape,
                Enabled = enabled
            };

            // Act
            var style = Parser.Parse(args);
            style(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.MinimapIcon);
            return new object[]
            {
                filterblock.MinimapIcon.Size,
                filterblock.MinimapIcon.Color,
                filterblock.MinimapIcon.Shape,
                filterblock.MinimapIcon.Enabled,
            };
        }

    }
}
