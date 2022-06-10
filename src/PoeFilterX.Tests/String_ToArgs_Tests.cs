using PoeFilterX.Business;
using PoeFilterX.Business.Extensions;

namespace PoeFilterX.Tests
{
    public class String_ToArgs_Tests
    {
        [TestCase("", ExpectedResult = new string[0])]
        [TestCase("A B C", ExpectedResult = new []{"A", "B", "C"})]
        [TestCase("A BCDE", ExpectedResult = new []{"A", "BCDE"})]
        [TestCase(@"A B ""C D E""", ExpectedResult = new []{"A", "B", "C D E"})]
        [TestCase(@"A B ""C \""D E""", ExpectedResult = new []{"A", "B", @"C ""D E"})]
        [TestCase(@" A      B "" C \\D E""", ExpectedResult = new []{"A", "B", @" C \D E"})]
        [TestCase("            \n", ExpectedResult = new string[0])]
        [TestCase(" \"  \" \"\t\"  \"\n\"", ExpectedResult = new [] {"  ", "\t", "\n"})]
        [TestCase("\"\\t\" \"\\n\"", ExpectedResult = new [] {"\t", "\n"})]
        public string[] String_ToArgs_ByCase(string command)
        {
            return command.ToArgs();
        }

        [TestCase(@"\")]
        [TestCase(@"\""")]
        [TestCase(@"""")]
        [TestCase(@"""\")]
        [TestCase(@"""\o""")]
        [TestCase(@"""\ """)]
        [TestCase(@"""\p""")]
        public void String_ToArgs_ThrowsParserException(string command)
        {
            _ = Assert.Throws<ParserException>(() => command.ToArgs());
        }

    }
}