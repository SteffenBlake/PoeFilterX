using PoeFilterX.Business;
using PoeFilterX.Business.Extensions;

namespace PoeFilterX.Tests
{
    public class String_ToArgs_Tests
    {
        [TestCase("", ExpectedResult = new string[]{})]
        [TestCase("A B C", ExpectedResult = new string[]{"A", "B", "C"})]
        [TestCase("A BCDE", ExpectedResult = new string[]{"A", "BCDE"})]
        [TestCase(@"A B ""C D E""", ExpectedResult = new string[]{"A", "B", "C D E"})]
        [TestCase(@"A B ""C \""D E""", ExpectedResult = new string[]{"A", "B", @"C ""D E"})]
        [TestCase(@" A      B "" C \\D E""", ExpectedResult = new string[]{"A", "B", @" C \D E"})]
        [TestCase("            \n", ExpectedResult = new string[0])]
        [TestCase(" \"  \" \"\t\"  \"\n\"", ExpectedResult = new string[] {"  ", "\t", "\n"})]
        public string[] String_ToArgs_ByCase(string command)
        {
            return command.ToArgs();
        }

        [TestCase(@"\")]
        [TestCase(@"\""")]
        [TestCase(@"""\")]
        public void String_ToArgs_ThrowsParserException(string command)
        {
            Assert.Throws<ParserException>(() => command.ToArgs());
        }

    }
}