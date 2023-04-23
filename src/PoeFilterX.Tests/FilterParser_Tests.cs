using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services;
using PoeFilterX.Tests.Mocks;

namespace PoeFilterX.Tests
{
    public  class FilterParser_Tests
    {
        public FilterXParser FilterParser { get; set; }
        public MockStreamFetcher MockStreamFetcher { get; set; }
        public MockFilterBlockParser MockFilterBlockParser { get; set; }

        public FilterParser_Tests()
        {
            MockStreamFetcher = new MockStreamFetcher(new Dictionary<string, string> { { "", "" } });
            MockFilterBlockParser = new MockFilterBlockParser();
            FilterParser = new FilterXParser(MockStreamFetcher, MockFilterBlockParser);
        }

        [SetUp]
        public void Setup()
        {
            MockStreamFetcher = new MockStreamFetcher(new Dictionary<string, string> { { "A", "test\ntest\n" } });
            MockFilterBlockParser = new MockFilterBlockParser();
            FilterParser = new FilterXParser(MockStreamFetcher, MockFilterBlockParser);
        }

        [Test]
        public async Task Parse_ReadsToEnd()
        {
            using var stream = MockStreamFetcher.Fetch("A");
            await FilterParser.ParseAsync(new Filter(), stream);

            Assert.That(stream.EndOfStream, Is.True);
        }
    }
}
