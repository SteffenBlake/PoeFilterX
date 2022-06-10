using Microsoft.Extensions.Configuration;
using PoeFilterX.Business;
using PoeFilterX.Business.Extensions;

namespace PoeFilterX.Tests
{
    public class InjectEnvironmentInternal_Tests
    {
        private IConfiguration Config { get; set; }

        public InjectEnvironmentInternal_Tests()
        {
            Config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"TEST_A", "VAL_A"},
                    {"TEST_B", "VAL_B"},
                    {"TEST_C", "VAL_C"},
                    {"TEST_D", "VAL_D"},
                })
                .Build();
        }

        [TestCase("%TEST_A%", @"""%TEST_B%""", ExpectedResult = new [] { "VAL_A", @"VAL_B" })]
        [TestCase("TEST_C", @"""TEST_D""", ExpectedResult = new [] { "TEST_C", @"""TEST_D""" })]
        public string[] InjectEnvironment_Cases(params string[] args)
        {
            // Act
            var results = Config.InjectEnvironment(args);

            // Assert
            return results;
        }

        [TestCase("%TEST_X%")]
        public void InjectEnvironment_ThrowsUnrecognized(params string[] args)
        {
            // Act
            _ = Assert.Throws<ParserException>(() => Config.InjectEnvironment(args));
        }
    }
}
