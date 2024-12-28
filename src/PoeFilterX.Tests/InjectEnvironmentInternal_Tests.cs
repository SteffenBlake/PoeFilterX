using Microsoft.Extensions.Configuration;
using PoeFilterX.Business;
using PoeFilterX.Business.Services;

namespace PoeFilterX.Tests
{
    public class InjectEnvironmentInternal_Tests
    {
        private VariableStore Store { get; }
        public InjectEnvironmentInternal_Tests()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"TEST_A", "VAL_A"},
                    {"TEST_B", "VAL_B"},
                    {"TEST_C", "VAL_C"},
                    {"TEST_D", "VAL_D"},
                })
                .Build();

            Store = new VariableStore(config);
        }


        [TestCase("%TEST_A%", @"""%TEST_B%""", ExpectedResult = new [] { "VAL_A", @"VAL_B" })]
        [TestCase("TEST_C", @"""TEST_D""", ExpectedResult = new [] { "TEST_C", @"""TEST_D""" })]
        public string[] InjectEnvironment_Cases(params string[] args)
        {
            // Act
            var results = Store.InjectEnvironment(args);

            // Assert
            return results;
        }
    }
}
