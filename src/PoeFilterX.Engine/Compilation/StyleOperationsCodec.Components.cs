using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

public static partial class StyleOperationsCodec
{
    private static Result<ColorValue> ReadColorAlpha(
        ColorValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 255)
            .Map(static value => new ColorValue(Alpha: value));
    }

    private static Result<ColorValue> ReadColorRed(
        ColorValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 255)
            .Map(static value => new ColorValue(Red: value));
    }

    private static Result<ColorValue> ReadColorGreen(
        ColorValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 255)
            .Map(static value => new ColorValue(Green: value));
    }

    private static Result<ColorValue> ReadColorBlue(
        ColorValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 255)
            .Map(static value => new ColorValue(Blue: value));
    }

    private static Result<SoundValue> ReadAlertId(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 1, 16)
            .Map(static value => new SoundValue(Id: value, UseBuiltIn: true));
    }

    private static Result<SoundValue> ReadAlertVolume(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 300)
            .Map(static value => new SoundValue(Volume: value, UseBuiltIn: true));
    }

    private static Result<SoundValue> ReadCustomAlertVolume(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 300)
            .Map(static value => new SoundValue(CustomVolume: value));
    }

    private static Result<SoundValue> ReadAlertStyle(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Choice(args[0], "positional", "global")
            .Map(static value => new SoundValue(Positional: value, UseBuiltIn: true));
    }

    private static Result<IconValue> ReadIconSize(
        IconValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], 0, 2)
            .Map(static value => new IconValue(Size: value));
    }

    private static Result<IconValue> ReadIconColor(
        IconValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Enumeration<FilterColor>(args[0])
            .Map(static value => new IconValue(Color: value));
    }

    private static Result<IconValue> ReadIconShape(
        IconValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Enumeration<MiniMapIconShape>(args[0])
            .Map(static value => new IconValue(Shape: value));
    }

    private static Result<EffectValue> ReadPillarColor(
        EffectValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Enumeration<FilterColor>(args[0])
            .Map(static value => new EffectValue(Color: value));
    }

    private static Result<EffectValue> ReadPillarDuration(
        EffectValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Choice(args[0], "temporary", "permanent")
            .Map(static value => new EffectValue(Temporary: value));
    }
}
