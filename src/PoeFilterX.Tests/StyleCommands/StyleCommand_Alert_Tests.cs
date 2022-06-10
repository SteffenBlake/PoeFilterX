using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_Alert_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "alert";

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
        [TestCase($"{COMMAND}: 17")]
        [TestCase($"{COMMAND}: -1")]
        [TestCase($"{COMMAND}: 0")]
        [TestCase($"{COMMAND}: null")]
        [TestCase($"{COMMAND}: 255 255 255 255 255")]
        [TestCase($"{COMMAND}: 10 -1")]
        [TestCase($"{COMMAND}: 10 301")]
        [TestCase($"{COMMAND}: 10 150 burger")]
        [TestCase($"{COMMAND}: 10 150 null")]
        [TestCase($"{COMMAND}: 10 150 1")]
        [TestCase($"{COMMAND}: 10 150 0")]
        [TestCase($"{COMMAND}: 10 150 -1")]
        [TestCase($"{COMMAND}: enabled -1")]
        public void Alert_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: 1", ExpectedResult = new object?[] { 1, null })]
        [TestCase($"{COMMAND}: 1 100", ExpectedResult = new object?[] { 1, 100 })]
        public object?[] Alert_Values_GlobalImplicit(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.That(filterblock.AlertSound?.Positional, Is.EqualTo(false));
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume
            };
        }

        [TestCase($"{COMMAND}: 1 100 global", ExpectedResult = new object?[] { 1, 100 })]
        [TestCase($"{COMMAND}: 1 100 GLOBAL", ExpectedResult = new object?[] { 1, 100 })]
        [TestCase($"{COMMAND}: 1 100 gLoBaL ", ExpectedResult = new object?[] { 1, 100 })]
        public object?[] Alert_Values_GlobalExplicit(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.That(filterblock.AlertSound?.Positional, Is.EqualTo(false));
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume
            };
        }

        [TestCase($"{COMMAND}: 1 100 positional", ExpectedResult = new object?[] { 1, 100 })]
        [TestCase($"{COMMAND}: 1 100 POSITIONAL", ExpectedResult = new object?[] { 1, 100 })]
        [TestCase($"{COMMAND}: 1 100 PoSiTiOnAl", ExpectedResult = new object?[] { 1, 100 })]
        public object?[] Alert_Values_PositionalExplicit(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.That(filterblock.AlertSound?.Positional, Is.EqualTo(true));
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume
            };
        }

        [TestCase($"{COMMAND}: enabled", ExpectedResult = new object?[] { null, null, true })]
        [TestCase($"{COMMAND}: disabled", ExpectedResult = new object?[] { null, null, false })]
        public object?[] Alert_Toggle(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume,
                filterblock.AlertSound?.Enabled,
            };
        }

        [TestCase($"{COMMAND}-id:")]
        [TestCase($"{COMMAND}-id: ")]
        [TestCase($"{COMMAND}-id: #42143")]
        [TestCase($"{COMMAND}-id: 0x")]
        [TestCase($"{COMMAND}-id: 0x1")]
        [TestCase($"{COMMAND}-id: 0x12")]
        [TestCase($"{COMMAND}-id: 0x123")]
        [TestCase($"{COMMAND}-id: 0x1234")]
        [TestCase($"{COMMAND}-id: 0x12345")]
        [TestCase($"{COMMAND}-id: 0x1234567")]
        [TestCase($"{COMMAND}-id: 0x123456789")]
        [TestCase($"{COMMAND}-id: 0x1234567890")]
        [TestCase($"{COMMAND}-id: FFFFFF")]
        [TestCase($"{COMMAND}-id: 17")]
        [TestCase($"{COMMAND}-id: -1")]
        [TestCase($"{COMMAND}-id: 0")]
        [TestCase($"{COMMAND}-id: null")]
        [TestCase($"{COMMAND}-id: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-id: 10 -1")]
        [TestCase($"{COMMAND}-id: 10 301")]
        [TestCase($"{COMMAND}-id: 10 150 burger")]
        [TestCase($"{COMMAND}-id: 10 150 null")]
        [TestCase($"{COMMAND}-id: 10 150 1")]
        [TestCase($"{COMMAND}-id: 10 150 0")]
        [TestCase($"{COMMAND}-id: 10 150 -1")]
        public void AlertId_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-id: 1", ExpectedResult = 1)]
        [TestCase($"{COMMAND}-id: 10", ExpectedResult = 10)]
        [TestCase($"{COMMAND}-id: 16", ExpectedResult = 16)]
        public int? AlertId_Values_NoPreExisting(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.That(filterblock.AlertSound?.Positional, Is.EqualTo(false));
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return filterblock.AlertSound?.Id;
        }

        [TestCase($"{COMMAND}-id: 10", null, false, false, ExpectedResult = new object?[] { 10, null, false, false })]
        [TestCase($"{COMMAND}-id: 10", null, false, true, ExpectedResult = new object?[] { 10, null, false, true })]
        [TestCase($"{COMMAND}-id: 10", null, true, false, ExpectedResult = new object?[] { 10, null, true, false })]
        [TestCase($"{COMMAND}-id: 10", null, true, true, ExpectedResult = new object?[] { 10, null, true, true })]
        [TestCase($"{COMMAND}-id: 10", 100, false, false, ExpectedResult = new object?[] { 10, 100, false, false })]
        [TestCase($"{COMMAND}-id: 10", 100, false, true, ExpectedResult = new object?[] { 10, 100, false, true })]
        [TestCase($"{COMMAND}-id: 10", 100, true, false, ExpectedResult = new object?[] { 10, 100, true, false })]
        [TestCase($"{COMMAND}-id: 10", 100, true, true, ExpectedResult = new object?[] { 10, 100, true, true })]
        public object?[] AlertId_Values_PreExisting(string args, int? volume, bool positional, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                AlertSound = new AlertSound
                {
                    Volume = volume,
                    Positional = positional,
                    Enabled = enabled,
                    Id = 1
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);

            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume,
                filterblock.AlertSound?.Positional,
                filterblock.AlertSound?.Enabled

            };
        }

        [TestCase($"{COMMAND}-volume:")]
        [TestCase($"{COMMAND}-volume: ")]
        [TestCase($"{COMMAND}-volume: #42143")]
        [TestCase($"{COMMAND}-volume: 0x")]
        [TestCase($"{COMMAND}-volume: 0x1")]
        [TestCase($"{COMMAND}-volume: 0x12")]
        [TestCase($"{COMMAND}-volume: 0x123")]
        [TestCase($"{COMMAND}-volume: 0x1234")]
        [TestCase($"{COMMAND}-volume: 0x12345")]
        [TestCase($"{COMMAND}-volume: 0x1234567")]
        [TestCase($"{COMMAND}-volume: 0x123456789")]
        [TestCase($"{COMMAND}-volume: 0x1234567890")]
        [TestCase($"{COMMAND}-volume: FFFFFF")]
        [TestCase($"{COMMAND}-volume: 301")]
        [TestCase($"{COMMAND}-volume: -1")]
        [TestCase($"{COMMAND}-volume: null")]
        [TestCase($"{COMMAND}-volume: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-volume: 10 -1")]
        [TestCase($"{COMMAND}-volume: 10 301")]
        [TestCase($"{COMMAND}-volume: 10 150 burger")]
        [TestCase($"{COMMAND}-volume: 10 150 null")]
        [TestCase($"{COMMAND}-volume: 10 150 1")]
        [TestCase($"{COMMAND}-volume: 10 150 0")]
        [TestCase($"{COMMAND}-volume: 10 150 -1")]
        public void AlertVolume_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-volume: 0", ExpectedResult = 0)]
        [TestCase($"{COMMAND}-volume: 150", ExpectedResult = 150)]
        [TestCase($"{COMMAND}-volume: 300", ExpectedResult = 300)]
        public int? AlertVolume_Values_NoPreExisting(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.IsNotNull(filterblock.AlertSound?.Volume);
            Assert.That(filterblock.AlertSound?.Positional, Is.EqualTo(false));
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return filterblock.AlertSound?.Volume;
        }

        [TestCase($"{COMMAND}-volume: 100", 1, null, false, false, ExpectedResult = new object?[] { 1, 100, false, false })]
        [TestCase($"{COMMAND}-volume: 100", 1, null, false, true, ExpectedResult = new object?[] { 1, 100, false, true })]
        [TestCase($"{COMMAND}-volume: 100", 1, null, true, false, ExpectedResult = new object?[] { 1, 100, true, false })]
        [TestCase($"{COMMAND}-volume: 100", 1, null, true, true, ExpectedResult = new object?[] { 1, 100, true, true })]
        [TestCase($"{COMMAND}-volume: 100", 1, 50, false, false, ExpectedResult = new object?[] { 1, 100, false, false })]
        [TestCase($"{COMMAND}-volume: 100", 1, 50, false, true, ExpectedResult = new object?[] { 1, 100, false, true })]
        [TestCase($"{COMMAND}-volume: 100", 1, 50, true, false, ExpectedResult = new object?[] { 1, 100, true, false })]
        [TestCase($"{COMMAND}-volume: 100", 1, 50, true, true, ExpectedResult = new object?[] { 1, 100, true, true })]
        public object?[] AlertVolume_Values_PreExisting(string args, int id, int? volume, bool positional, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                AlertSound = new AlertSound
                {
                    Volume = volume,
                    Positional = positional,
                    Enabled = enabled,
                    Id = id
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);

            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume,
                filterblock.AlertSound?.Positional,
                filterblock.AlertSound?.Enabled
            };
        }

        [TestCase($"{COMMAND}-style:")]
        [TestCase($"{COMMAND}-style: ")]
        [TestCase($"{COMMAND}-style: #42143")]
        [TestCase($"{COMMAND}-style: 0x")]
        [TestCase($"{COMMAND}-style: 0x1")]
        [TestCase($"{COMMAND}-style: 0x12")]
        [TestCase($"{COMMAND}-style: 0x123")]
        [TestCase($"{COMMAND}-style: 0x1234")]
        [TestCase($"{COMMAND}-style: 0x12345")]
        [TestCase($"{COMMAND}-style: 0x1234567")]
        [TestCase($"{COMMAND}-style: 0x123456789")]
        [TestCase($"{COMMAND}-style: 0x1234567890")]
        [TestCase($"{COMMAND}-style: FFFFFF")]
        [TestCase($"{COMMAND}-style: 301")]
        [TestCase($"{COMMAND}-style: -1")]
        [TestCase($"{COMMAND}-style: null")]
        [TestCase($"{COMMAND}-style: 255 255 255 255 255")]
        [TestCase($"{COMMAND}-style: 10 -1")]
        [TestCase($"{COMMAND}-style: 10 301")]
        [TestCase($"{COMMAND}-style: 10 150 burger")]
        [TestCase($"{COMMAND}-style: 10 150 null")]
        [TestCase($"{COMMAND}-style: 10 150 1")]
        [TestCase($"{COMMAND}-style: 10 150 0")]
        [TestCase($"{COMMAND}-style: 10 150 -1")]
        public void AlertStyle_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}-style: global", ExpectedResult = false)]
        [TestCase($"{COMMAND}-style: positional", ExpectedResult = true)]
        public bool? AlertStyle_Values_NoPreExisting(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);
            Assert.IsNotNull(filterblock.AlertSound?.Positional);
            Assert.That(filterblock.AlertSound?.Enabled, Is.EqualTo(true));
            return filterblock.AlertSound?.Positional;
        }

        [TestCase($"{COMMAND}-style: global", 1, null, false, false, ExpectedResult = new object?[] { 1, null, false, false })]
        [TestCase($"{COMMAND}-style: global", 1, null, false, true, ExpectedResult = new object?[] { 1, null, false, true })]
        [TestCase($"{COMMAND}-style: global", 1, null, true, false, ExpectedResult = new object?[] { 1, null, false, false })]
        [TestCase($"{COMMAND}-style: global", 1, null, true, true, ExpectedResult = new object?[] { 1, null, false, true })]
        [TestCase($"{COMMAND}-style: global", 1, 50, false, false, ExpectedResult = new object?[] { 1, 50, false, false })]
        [TestCase($"{COMMAND}-style: global", 1, 50, false, true, ExpectedResult = new object?[] { 1, 50, false, true })]
        [TestCase($"{COMMAND}-style: global", 1, 50, true, false, ExpectedResult = new object?[] { 1, 50, false, false })]
        [TestCase($"{COMMAND}-style: global", 1, 50, true, true, ExpectedResult = new object?[] { 1, 50, false, true })]
        [TestCase($"{COMMAND}-style: positional", 1, null, false, false, ExpectedResult = new object?[] { 1, null, true, false })]
        [TestCase($"{COMMAND}-style: positional", 1, null, false, true, ExpectedResult = new object?[] { 1, null, true, true })]
        [TestCase($"{COMMAND}-style: positional", 1, null, true, false, ExpectedResult = new object?[] { 1, null, true, false })]
        [TestCase($"{COMMAND}-style: positional", 1, null, true, true, ExpectedResult = new object?[] { 1, null, true, true })]
        [TestCase($"{COMMAND}-style: positional", 1, 50, false, false, ExpectedResult = new object?[] { 1, 50, true, false })]
        [TestCase($"{COMMAND}-style: positional", 1, 50, false, true, ExpectedResult = new object?[] { 1, 50, true, true })]
        [TestCase($"{COMMAND}-style: positional", 1, 50, true, false, ExpectedResult = new object?[] { 1, 50, true, false })]
        [TestCase($"{COMMAND}-style: positional", 1, 50, true, true, ExpectedResult = new object?[] { 1, 50, true, true })]
        public object?[] AlertStyle_Values_PreExisting(string args, int id, int? volume, bool positional, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                AlertSound = new AlertSound
                {
                    Volume = volume,
                    Positional = positional,
                    Enabled = enabled,
                    Id = id
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);

            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume,
                filterblock.AlertSound?.Positional,
                filterblock.AlertSound?.Enabled
            };
        }

        [TestCase($"{COMMAND}: disabled", 1, null, false, false, ExpectedResult = new object?[] { 1, null, false, false })]
        [TestCase($"{COMMAND}: disabled", 1, null, false, true, ExpectedResult = new object?[] { 1, null, false, false })]
        [TestCase($"{COMMAND}: disabled", 1, null, true, false, ExpectedResult = new object?[] { 1, null, true, false })]
        [TestCase($"{COMMAND}: disabled", 1, null, true, true, ExpectedResult = new object?[] { 1, null, true, false })]
        [TestCase($"{COMMAND}: disabled", 1, 50, false, false, ExpectedResult = new object?[] { 1, 50, false, false })]
        [TestCase($"{COMMAND}: disabled", 1, 50, false, true, ExpectedResult = new object?[] { 1, 50, false, false })]
        [TestCase($"{COMMAND}: disabled", 1, 50, true, false, ExpectedResult = new object?[] { 1, 50, true, false })]
        [TestCase($"{COMMAND}: disabled", 1, 50, true, true, ExpectedResult = new object?[] { 1, 50, true, false })]
        [TestCase($"{COMMAND}: enabled", 1, null, false, false, ExpectedResult = new object?[] { 1, null, false, true })]
        [TestCase($"{COMMAND}: enabled", 1, null, false, true, ExpectedResult = new object?[] { 1, null, false, true })]
        [TestCase($"{COMMAND}: enabled", 1, null, true, false, ExpectedResult = new object?[] { 1, null, true, true })]
        [TestCase($"{COMMAND}: enabled", 1, null, true, true, ExpectedResult = new object?[] { 1, null, true, true })]
        [TestCase($"{COMMAND}: enabled", 1, 50, false, false, ExpectedResult = new object?[] { 1, 50, false, true })]
        [TestCase($"{COMMAND}: enabled", 1, 50, false, true, ExpectedResult = new object?[] { 1, 50, false, true })]
        [TestCase($"{COMMAND}: enabled", 1, 50, true, false, ExpectedResult = new object?[] { 1, 50, true, true })]
        [TestCase($"{COMMAND}: enabled", 1, 50, true, true, ExpectedResult = new object?[] { 1, 50, true, true })]
        public object?[] AlertToggled_Values_PreExisting(string args, int id, int? volume, bool positional, bool enabled)
        {
            // Arrange
            var filterblock = new FilterBlock
            {
                AlertSound = new AlertSound
                {
                    Volume = volume,
                    Positional = positional,
                    Enabled = enabled,
                    Id = id
                }
            };

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.AlertSound);

            return new object?[] {
                filterblock.AlertSound?.Id,
                filterblock.AlertSound?.Volume,
                filterblock.AlertSound?.Positional,
                filterblock.AlertSound?.Enabled
            };
        }

        [TestCase($"{COMMAND}-path:")]
        [TestCase($"{COMMAND}-path: ")]
        [TestCase($"{COMMAND}-path: sadasd sadsadas")]
        [TestCase($"{COMMAND}-path: 1 2")]
        [TestCase(@$"{COMMAND}-path: ""1 2"" "" 3 4""")]
        public void AlertPath_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase(@$"{COMMAND}-path: enabled", ExpectedResult = true)]
        [TestCase(@$"{COMMAND}-path: disabled", ExpectedResult = false)]
        public bool AlertPath_Toggled(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            return filterblock.CustomAlertSoundEnabled;
        }

        [TestCase(@$"{COMMAND}-path: test", ExpectedResult = "test")]
        [TestCase(@$"{COMMAND}-path: ""test""", ExpectedResult = "test")]
        [TestCase(@$"{COMMAND}-path: ""test test""", ExpectedResult = "test test")]
        public string? AlertPath_Values_GlobalExplicit(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            // Assert
            Assert.IsNotNull(filterblock.CustomAlertSound);
            return filterblock.CustomAlertSound;
        }
    }
}
