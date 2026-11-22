using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Tests.Results;

[Parallelizable(ParallelScope.All)]
public class ResultTests
{
    [Test]
    public void Map_transforms_success()
    {
        Result<int> result = 21;
        var mapped = result.Map(static value => value * 2);
        Assert.That(mapped.Value, Is.EqualTo(42));
    }

    [Test]
    public void Map_and_FlatMap_pass_errors_through_without_invoking_callbacks()
    {
        var error = new ParseError(DiagnosticCodes.InvalidArgument, "Invalid input");
        Result<int> result = error;
        var mapped = result.Map<int, string>(_ => throw new AssertionException("Must not map an error."));
        var bound = result.FlatMap<int, string>(_ => throw new AssertionException("Must not bind an error."));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(mapped.Value, Is.SameAs(error));
            Assert.That(bound.Value, Is.SameAs(error));
        }
    }

    [Test]
    public void Map_preserves_deprecation_metadata()
    {
        var date = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Result<int> result = new Deprecated<int>(21, date, "Old spelling");
        var mapped = result.Map(static value => value * 2);
        Assert.That(mapped.Value, Is.EqualTo(new Deprecated<int>(42, date, "Old spelling")));
    }

    [Test]
    public void FlatMap_preserves_outer_deprecation_for_a_current_success()
    {
        var date = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Result<int> result = new Deprecated<int>(21, date, "Old spelling");
        var mapped = result.FlatMap<int, string>(static value => value.ToString());
        Assert.That(mapped.Value, Is.EqualTo(new Deprecated<string>("21", date, "Old spelling")));
    }

    [Test]
    public void FlatMap_merges_deprecations_using_the_earliest_date_and_outer_to_inner_messages()
    {
        var outerDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var innerDate = outerDate.AddDays(-1);
        Result<int> result = new Deprecated<int>(21, outerDate, "Outer");
        var mapped = result.FlatMap<int, int>(value => new Deprecated<int>(value * 2, innerDate, "Inner"));
        Assert.That(mapped.Value, Is.EqualTo(new Deprecated<int>(42, innerDate, "Outer Inner")));
    }

    [Test]
    public void A_failure_from_a_deprecated_success_remains_a_failure()
    {
        var error = new ParseError(DiagnosticCodes.InvalidArgument, "Invalid input");
        Result<int> result = new Deprecated<int>(1, DateTime.UtcNow);
        var bound = result.FlatMap<int, int>(_ => error);
        Assert.That(bound.Value, Is.SameAs(error));
    }

    [Test]
    public void Exceptions_from_callbacks_propagate_as_programming_errors()
    {
        var bug = new InvalidOperationException("Programming error");
        Result<int> result = 1;
        var mapped = Assert.Throws<InvalidOperationException>(() => result.Map<int, int>(_ => throw bug));
        var bound = Assert.Throws<InvalidOperationException>(() => result.FlatMap<int, int>(_ => throw bug));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(mapped, Is.SameAs(bug));
            Assert.That(bound, Is.SameAs(bug));
        }
    }

    [Test]
    public void Nullable_successes_can_represent_omitted_optional_arguments()
    {
        Result<int?> result = (int?)null;
        var mapped = result.Map(static value => value ?? 42);
        Assert.That(mapped.Value, Is.EqualTo(42));
    }

    [Test]
    public void Zip_preserves_argument_order_when_both_values_fail()
    {
        var first = new ParseError(DiagnosticCodes.InvalidArgument, "First");
        Result<int> left = first;
        Result<int> right = new ParseError(DiagnosticCodes.InvalidArgument, "Second");
        var zipped = left.Zip(right);
        Assert.That(zipped.Value, Is.SameAs(first));
    }
}
