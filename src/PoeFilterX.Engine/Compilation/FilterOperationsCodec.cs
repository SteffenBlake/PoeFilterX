using System.Collections.Immutable;
using System.Text;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

public static partial class FilterOperationsCodec
{
    private static Result<ImmutableArray<OperatorValue<ImmutableArray<string>>>> ReadModifiers(
        ImmutableArray<OperatorValue<ImmutableArray<string>>> previous,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        var values = args;
        OperationArguments.Operator(ref values);
        if (!values.IsEmpty && values[0].Length > 0
            && (char.IsAsciiDigit(values[0][0]) || values[0][0] == '-'))
        {
            var count = OperationArguments.Integer(values[0], 0, int.MaxValue, "modifier count");
            if (count is ParseError error)
            {
                return error;
            }

            if (values.Length == 1)
            {
                return new ParseError(DiagnosticCodes.InvalidArgumentCount, "Expected modifier names after count.");
            }
        }

        return Read(previous, args, options);
    }

    private static Result<TransfiguredGemValue> ReadTransfiguredGem(
        TransfiguredGemValue? _1,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        var values = args;
        OperationArguments.Operator(ref values);
        if (values.IsEmpty || (bool.TryParse(values[0], out _) && args.Length != 1))
        {
            return new ParseError(DiagnosticCodes.InvalidArgumentCount, "Expected a boolean or gem names.");
        }

        if (args.Length == 1 && bool.TryParse(args[0], out var boolean))
        {
            return new TransfiguredGemValue(boolean, []);
        }

        return Read(default(ImmutableArray<OperatorValue<ImmutableArray<string>>>), args, options)
            .Map(static names => new TransfiguredGemValue(null, names));
    }

    private static TransfiguredGemValue? Merge(TransfiguredGemValue? previous, TransfiguredGemValue? next)
    {
        return next is null ? previous : next.Any.HasValue || previous is null || previous.Any.HasValue
            ? next : new(null, previous.Names.AddRange(next.Names));
    }

    private static void WriteTransfiguredGem(
        StringBuilder output,
        string name,
        TransfiguredGemValue? value
    )
    {
        if (value?.Any is { } boolean)
        {
            Write(output, name, boolean);
        }
        else if (value is not null)
        {
            Write(output, name, value.Names);
        }
    }

    private static void WriteModifiers(
        StringBuilder output,
        string name,
        ImmutableArray<OperatorValue<ImmutableArray<string>>> values
    )
    {
        var uncounted = ImmutableArray.CreateBuilder<OperatorValue<ImmutableArray<string>>>();
        foreach (var value in values)
        {
            if (value.Value.IsEmpty || !int.TryParse(value.Value[0], out var count))
            {
                uncounted.Add(value);
                continue;
            }

            output.Append('\t').Append(name).Append(' ').Append(value.Operator.ToSymbol()).Append(' ').Append(count);
            foreach (var modifier in value.Value.Skip(1))
            {
                output.Append(" \"").Append(modifier).Append('"');
            }

            output.AppendLine();
        }

        Write(output, name, uncounted.ToImmutable());
    }

    private static Result<bool> Read(
        bool? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (args.IsEmpty)
        {
            return new ParseError(
                DiagnosticCodes.InvalidArgumentCount,
                "Expected one boolean argument, got none."
            );
        }

        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Boolean(args[0]);
    }

    private static Result<ImmutableArray<OperatorValue<int>>> Read(
        ImmutableArray<OperatorValue<int>> _,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        var op = OperationArguments.Operator(ref args);
        if (args.IsEmpty)
        {
            return ImmutableArray<OperatorValue<int>>.Empty;
        }

        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], options.Minimum, options.Maximum, options.Name)
            .Map(op, static (value, comparison) =>
                ImmutableArray.Create(new OperatorValue<int>(comparison, value)));
    }

    private static Result<ImmutableArray<OperatorValue<ImmutableArray<string>>>> Read(
        ImmutableArray<OperatorValue<ImmutableArray<string>>> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        var op = OperationArguments.Operator(ref args);

        ImmutableArray<OperatorValue<ImmutableArray<string>>> values = args.IsEmpty ? [] : [new(op, [.. args])];

        return values;
    }

    private static Result<ImmutableArray<OperatorValue<T>>> Read<T>(
        ImmutableArray<OperatorValue<T>> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    ) where T : struct, Enum
    {
        var op = OperationArguments.Operator(ref args);
        if (args.IsEmpty)
        {
            return ImmutableArray<OperatorValue<T>>.Empty;
        }

        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Enumeration<T>(args[0])
            .Map(op, static (value, comparison) =>
                ImmutableArray.Create(new OperatorValue<T>(comparison, value)));
    }

    private static Result<ImmutableArray<T>> Read<T>(
        ImmutableArray<T> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    ) where T : struct, Enum
    {
        var values = ImmutableArray.CreateBuilder<T>(args.Length);
        foreach (var arg in args)
        {
            var parsed = OperationArguments.Enumeration<T>(arg);
            if (parsed is ParseError error)
            {
                return error;
            }

            if (parsed is T value)
            {
                values.Add(value);
            }
        }

        return values.MoveToImmutable();
    }

    private static Result<ImmutableArray<string>> Read(
        ImmutableArray<string> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        return ImmutableArray.Create(args);
    }

    private static Result<ImmutableArray<string>> ReadHasValue(
        ImmutableArray<string> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        return ImmutableArray.Create(string.Join(' ', args));
    }

    private static Result<ImmutableArray<bool>> ReadIsTrue(
        ImmutableArray<bool> _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (args.IsEmpty)
        {
            return ImmutableArray<bool>.Empty;
        }

        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Boolean(args[0]).Map(static value => ImmutableArray.Create(value));
    }

    private static T? Merge<T>(T? previous, T? next) where T : struct => next ?? previous;

    private static ImmutableArray<T> Merge<T>(ImmutableArray<T> previous, ImmutableArray<T> next)
    {
        if (next.IsDefaultOrEmpty)
        {
            return previous;
        }

        return previous.IsDefaultOrEmpty ? next : previous.AddRange(next);
    }

    private static void Write(
        StringBuilder output,
        string name,
        bool? value
    )
    {
        if (value.HasValue)
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Value).AppendLine();
        }
    }

    private static void Write<T>(
        StringBuilder output,
        string name,
        ImmutableArray<OperatorValue<T>> values
    )
    {
        foreach (var value in values)
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Operator.ToSymbol())
                .Append(' ').Append(value.Value).AppendLine();
        }
    }

    private static void Write(
        StringBuilder output,
        string name,
        ImmutableArray<OperatorValue<ImmutableArray<string>>> values
    )
    {
        foreach (var group in values.GroupBy(value => value.Operator))
        {
            output.Append('\t').Append(name).Append(' ').Append(group.Key.ToSymbol());
            foreach (var value in group.SelectMany(value => value.Value))
            {
                output.Append(" \"").Append(value).Append('"');
            }

            output.AppendLine();
        }
    }

    private static void Write<T>(
        StringBuilder output,
        string name,
        ImmutableArray<T> values
    ) where T : struct, Enum
    {
        if (values.IsEmpty)
        {
            return;
        }

        output.Append('\t').Append(name);
        foreach (var value in values.Distinct())
        {
            output.Append(" \"").Append(value).Append('"');
        }

        output.AppendLine();
    }
}
