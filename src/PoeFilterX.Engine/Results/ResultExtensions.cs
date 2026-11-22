namespace PoeFilterX.Engine.Results;

public static class ResultExtensions
{
    public static Result<TOut> Map<T, TOut>(this Result<T> result, Func<T, TOut> map)
    {
        return result.Map(map, static (value, callback) => callback(value));
    }

    public static Result<TOut> Map<T, TState, TOut>(
        this Result<T> result,
        TState state,
        Func<T, TState, TOut> map
    )
    {
        return result switch
        {
            Deprecated<T> value => new Deprecated<TOut>(
                map(value.Value, state),
                value.DeprecatedAfterUtc,
                value.Message
            ),
            ParseError error => error,
            T value => map(value, state),
            null when typeof(T).IsValueType && default(T) is null => map(default!, state),
            null => throw new InvalidOperationException("Result has no value.")
        };
    }

    public static Result<TOut> FlatMap<T, TOut>(this Result<T> result, Func<T, Result<TOut>> map)
    {
        return result.FlatMap(map, static (value, callback) => callback(value));
    }

    public static Result<TOut> FlatMap<T, TState, TOut>(
        this Result<T> result,
        TState state,
        Func<T, TState, Result<TOut>> map
    )
    {
        return result switch
        {
            Deprecated<T> value => Deprecate(map(value.Value, state), value.DeprecatedAfterUtc, value.Message),
            ParseError error => error,
            T value => map(value, state),
            null when typeof(T).IsValueType && default(T) is null => map(default!, state),
            null => throw new InvalidOperationException("Result has no value.")
        };
    }

    public static Result<(TLeft Left, TRight Right)> Zip<TLeft, TRight>(
        this Result<TLeft> left,
        Result<TRight> right
    )
    {
        return (left, right) switch
        {
            (ParseError error, _) => error,
            (_, ParseError error) => error,
            (TLeft first, TRight second) => (first, second),
            (TLeft first, null) when typeof(TRight).IsValueType && default(TRight) is null => (first, default!),
            (null, TRight second) when typeof(TLeft).IsValueType && default(TLeft) is null => (default!, second),
            (null, null) when typeof(TLeft).IsValueType && default(TLeft) is null
                && typeof(TRight).IsValueType && default(TRight) is null => (default!, default!),
            _ => left.FlatMap(right, static (first, remaining) =>
                remaining.Map(first, static (second, initial) => (initial, second)))
        };
    }

    private static Result<T> Deprecate<T>(
        Result<T> result,
        DateTime date,
        string? message
    )
    {
        return result switch
        {
            ParseError error => error,
            Deprecated<T> inner => new Deprecated<T>(
                inner.Value,
                date < inner.DeprecatedAfterUtc ? date : inner.DeprecatedAfterUtc,
                string.IsNullOrEmpty(message) ? inner.Message
                    : string.IsNullOrEmpty(inner.Message) ? message : message + " " + inner.Message
            ),
            T value => new Deprecated<T>(value, date, message),
            null when typeof(T).IsValueType && default(T) is null => new Deprecated<T>(default!, date, message),
            null => throw new InvalidOperationException("Result has no value.")
        };
    }
}
