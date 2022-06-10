using PoeFilterX.Business.Extensions;
using System.Linq.Expressions;

namespace PoeFilterX.Tests
{
    public class Lamba_Tests
    {
        public class TestClass
        {
            public string? ValueString { get; set; }
            public int? ValueInt { get; set; }
        }

        [Test]
        public void SetPropertyValue_String_NullToVal_Works()
        {
            // Arrange
            var testClass = new TestClass();

            // Act
            testClass.SetPropertyValue((c) => c.ValueString, "Test");

            // Assert
            Assert.That(testClass.ValueString, Is.EqualTo("Test"));
        }

        [Test]
        public void SetPropertyValue_String_ValToNull_Works()
        {
            // Arrange
            var testClass = new TestClass
            {
                ValueString = "Test"
            };

            // Act
            testClass.SetPropertyValue((c) => c.ValueString, null);

            // Assert
            Assert.That(testClass.ValueString, Is.Null);
        }

        [Test]
        public void SetPropertyValue_Int_NullToVal_Works()
        {
            // Arrange
            var testClass = new TestClass();

            // Act
            testClass.SetPropertyValue((c) => c.ValueInt, 1);

            // Assert
            Assert.That(testClass.ValueInt, Is.EqualTo(1));
        }

        [Test]
        public void SetPropertyValue_Int_ValToNull_Works()
        {
            // Arrange
            var testClass = new TestClass
            {
                ValueInt = 1
            };

            // Act
            testClass.SetPropertyValue((c) => c.ValueInt, null);

            // Assert
            Assert.That(testClass.ValueString, Is.Null);
        }

        [Test]
        public void GetName_Works()
        {
            Expression<Func<TestClass, string?>> strSelector = (t) => t.ValueString;
            Expression<Func<TestClass, int?>> intSelector = (t) => t.ValueInt;

            Assert.That(strSelector.GetName(), Is.EqualTo(nameof(TestClass.ValueString)));
            Assert.That(intSelector.GetName(), Is.EqualTo(nameof(TestClass.ValueInt)));
        }
    }
}