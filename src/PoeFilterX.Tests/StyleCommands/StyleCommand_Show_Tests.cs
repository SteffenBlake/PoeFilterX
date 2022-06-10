using PoeFilterX.Business;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PoeFilterX.Tests.StyleCommands
{
    public class StyleCommand_Show_Tests
    {
        private StyleCommandParser Parser { get; } = new();

        protected const string COMMAND = "show";

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
        public void Show_InvalidArgs_Throws(string args)
        {
            // Assert
            _ = Assert.Throws<ParserException>(() => Parser.Parse(args));
        }

        [TestCase($"{COMMAND}: true", ExpectedResult = true)]
        [TestCase($"{COMMAND}: True", ExpectedResult = true)]
        [TestCase($"{COMMAND}: TRUE", ExpectedResult = true)]
        [TestCase($"{COMMAND}: false", ExpectedResult = false)]
        [TestCase($"{COMMAND}: FALSE", ExpectedResult = false)]
        public bool? Show_Cases(string args)
        {
            // Arrange
            var filterblock = new FilterBlock();

            // Act
            var style = Parser.Parse(args);
            Assert.IsNotNull(style);
            style?.Invoke(filterblock);

            return filterblock.Show;
        }
    }
}
