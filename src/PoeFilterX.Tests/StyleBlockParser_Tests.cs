using PoeFilterX.Business;
using PoeFilterX.Business.Services;
using PoeFilterX.Business.Services.Abstractions;
using PoeFilterX.Tests.Mocks;

namespace PoeFilterX.Tests
{
    public class StyleBlockParser_Tests
    {
        private MockStyleCommandParser CommandParser { get; set; }
        private StyleBlockParser BlockParser { get; set; }

        [SetUp]
        public void Setup()
        {
            CommandParser = new MockStyleCommandParser();
            BlockParser = new StyleBlockParser(CommandParser);
        }

        [TestCase("dsadad")]
        [TestCase("using")]
        [TestCase("null")]
        [TestCase(@"""}""")]
        [TestCase(@"""\n"";")]
        [TestCase(@"""\t"";")]
        [TestCase(@"\"";")]
        [TestCase(@"\\;")]
        public void Parse_InvalidData_Throws(string data) 
        {
            // Arrange
            using var stream = new MemoryStream();
            using var trackingStream = new TrackingStreamReader("", stream);
            using var writer = new StreamWriter(stream);
            writer.Write(data);
            writer.Flush();
            stream.Position = 0;

            // Assert
            Assert.Throws<ParserException>(() => BlockParser.Parse(trackingStream));
            Assert.That(CommandParser.Calls, Is.Empty);
        }

        [TestCase(
            "testA; testA; testB; }", 
            ExpectedResult = new object[] { 
                new object[] {"testA", 2 }, 
                new object[] { "testB", 1 }, 
            }
        )]
        [TestCase(
            @" "";""; }",
            ExpectedResult = new object[] {
                new object[] {";", 1 },
            }
        )]
        [TestCase(
            @" ""\""\\""; }",
            ExpectedResult = new object[] {
                new object[] { @"""\", 1 },
            }
        )]
        [TestCase(
            @"/*comment*/test; }",
            ExpectedResult = new object[] {
                new object[] { "test", 1 },
            }
        )]
        [TestCase(
            @"test/*comment*/; }",
            ExpectedResult = new object[] {
                new object[] { "test", 1 },
            }
        )]
        [TestCase(
            @"test; /*comment*/ }",
            ExpectedResult = new object[] {
                new object[] { "test", 1 },
            }
        )]
        [TestCase(
            "\ntest\n; \n/*comment\n*/ \n}\n",
            ExpectedResult = new object[] {
                new object[] { "test", 1 },
            }
        )]
        public object[] Parse_Cases(string data)
        {
            // Arrange
            using var stream = new MemoryStream();
            using var trackingStream = new TrackingStreamReader("", stream);
            using var writer = new StreamWriter(stream);
            writer.Write(data);
            writer.Flush();
            stream.Position = 0;

            // Act
            BlockParser.Parse(trackingStream);

            // Assert
            var calls = CommandParser.Calls.Select(c => new object[] {c.Key, c.Value }).ToArray();
            return calls;
        }
    }
}
