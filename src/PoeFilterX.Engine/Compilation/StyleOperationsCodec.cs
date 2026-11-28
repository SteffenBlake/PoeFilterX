using System.Globalization;
using System.Text;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Compilation;

public static partial class StyleOperationsCodec
{
    private static T? Merge<T>(T? previous, T? next) where T : struct => next ?? previous;

    private static ColorValue? Merge(ColorValue? previous, ColorValue? next)
    {
        if (next is null)
        {
            return previous;
        }

        return new(
            next.Red ?? previous?.Red,
            next.Green ?? previous?.Green,
            next.Blue ?? previous?.Blue,
            next.Alpha ?? previous?.Alpha
        );
    }

    private static SoundValue? Merge(SoundValue? previous, SoundValue? next)
    {
        if (next is null)
        {
            return previous;
        }

        var enabled = next.Enabled ?? previous?.Enabled;
        if (next.UseBuiltIn && next.Enabled is null && previous is { CustomEnabled: not false, Path: not null })
        {
            enabled = true;
        }

        return new(
            next.Id ?? previous?.Id,
            next.Volume ?? previous?.Volume,
            next.Positional ?? previous?.Positional,
            enabled,
            next.Path ?? previous?.Path,
            next.UseBuiltIn ? false : next.CustomEnabled ?? previous?.CustomEnabled,
            UseBuiltIn: next.UseBuiltIn || (next.CustomEnabled is null && previous?.UseBuiltIn == true),
            Optional: next.Optional ?? previous?.Optional,
            CustomVolume: next.CustomVolume ?? previous?.CustomVolume
        );
    }

    private static IconValue? Merge(IconValue? previous, IconValue? next)
    {
        return next is null ? previous : new(
            next.Size ?? previous?.Size,
            next.Color ?? previous?.Color,
            next.Shape ?? previous?.Shape,
            next.Enabled ?? previous?.Enabled
        );
    }

    private static EffectValue? Merge(EffectValue? previous, EffectValue? next)
    {
        return next is null ? previous : new(
            next.Color ?? previous?.Color,
            next.Temporary ?? previous?.Temporary,
            next.Enabled ?? previous?.Enabled
        );
    }

    private static Result<FilterVisibility> ReadShow(
        FilterVisibility? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return bool.TryParse(args[0], out var show)
            ? show ? FilterVisibility.Show : FilterVisibility.Hide
            : OperationArguments.Enumeration<FilterVisibility>(args[0]);
    }

    private static Result<bool> ReadToggle(
        bool? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Choice(args[0], "enabled", "disabled");
    }

    private static Result<int> Read(
        int? _,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        if (OperationArguments.Count(args, 1) is { } error)
        {
            return error;
        }

        return OperationArguments.Integer(args[0], options.Minimum, options.Maximum);
    }

    private static Result<ColorValue> ReadColor(
        ColorValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        if (OperationArguments.Count(args, 1, 2, 3, 4) is { } error)
        {
            return error;
        }
        if (args.Length == 1 && NamedColors.Values.TryGetValue(args[0], out var namedColor))
        {
            return namedColor;
        }

        if (args.Length == 1 && args[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hex = args[0].AsSpan(2);
            if (hex.Length is not (6 or 8)
                || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgba))
            {
                return new ParseError(
                    DiagnosticCodes.InvalidArgument,
                    "Unexpected color code, expected '0xAARRGGBB' or '0xRRGGBB'"
                );
            }

            return new ColorValue(
                (int)(rgba >> 16 & 255),
                (int)(rgba >> 8 & 255),
                (int)(rgba & 255),
                hex.Length == 6 ? 255 : (int)(rgba >> 24)
            );
        }

        var red = OperationArguments.Integer(args[0], 0, 255);
        Result<int> green = args.Length > 1 ? OperationArguments.Integer(args[1], 0, 255) : 0;
        Result<int> blue = args.Length > 2 ? OperationArguments.Integer(args[2], 0, 255) : 0;
        Result<int> alpha = args.Length > 3 ? OperationArguments.Integer(args[3], 0, 255) : options.DefaultAlpha;

        return red.Zip(green).Zip(blue.Zip(alpha))
            .Map(static values => new ColorValue(
                values.Left.Left,
                values.Left.Right,
                values.Right.Left,
                values.Right.Right
            ));
    }

    private static Result<SoundValue> ReadAlert(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1, 2, 3) is { } error)
        {
            return error;
        }
        if (args.Length == 1 && OperationArguments.TryToggle(args[0], out var toggle))
        {
            return new SoundValue(Enabled: toggle, UseBuiltIn: toggle);
        }

        var id = OperationArguments.Integer(args[0], 1, 16);
        Result<int?> volume = args.Length > 1
            ? OperationArguments.Integer(args[1], 0, 300)
                .Map(static value => (int?)value)
            : (int?)null;
        Result<bool?> positional = args.Length > 2
            ? OperationArguments.Choice(args[2], "positional", "global")
                .Map(static value => (bool?)value)
            : (bool?)null;

        return id.Zip(volume).Zip(positional).Map(static values => new SoundValue(
            Id: values.Left.Left, Volume: values.Left.Right, Positional: values.Right, UseBuiltIn: true
        ));
    }

    private static Result<SoundValue> ReadAlertPath(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1, 2) is { } error)
        {
            return error;
        }
        if (args.Length == 1 && OperationArguments.TryToggle(args[0], out var toggle))
        {
            return new SoundValue(CustomEnabled: toggle);
        }

        Result<int?> volume = args.Length == 2
            ? OperationArguments.Integer(args[1], 0, 300).Map(static value => (int?)value)
            : (int?)null;

        return volume.Map(args[0], static (value, path) =>
            new SoundValue(Path: path, CustomEnabled: true, CustomVolume: value));
    }

    private static Result<SoundValue> ReadAlertOptional(
        SoundValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions options
    )
    {
        return ReadToggle(null, args, options).Map(static optional => new SoundValue(Optional: optional));
    }

    private static Result<IconValue> ReadIcon(
        IconValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1, 2, 3) is { } error)
        {
            return error;
        }
        if (args.Length == 1 && OperationArguments.TryToggle(args[0], out var toggle))
        {
            return new IconValue(Enabled: toggle);
        }

        var size = OperationArguments.Integer(args[0], 0, 2);
        Result<FilterColor?> color = args.Length > 1
            ? OperationArguments.Enumeration<FilterColor>(args[1])
                .Map(static value => (FilterColor?)value)
            : (FilterColor?)null;
        Result<MiniMapIconShape?> shape = args.Length > 2
            ? OperationArguments.Enumeration<MiniMapIconShape>(args[2])
                .Map(static value => (MiniMapIconShape?)value)
            : (MiniMapIconShape?)null;

        return size.Zip(color).Zip(shape).Map(static values => new IconValue(
            Size: values.Left.Left, Color: values.Left.Right, Shape: values.Right
        ));
    }

    private static Result<EffectValue> ReadPillar(
        EffectValue? _,
        ReadOnlySpan<string> args,
        OperationReadOptions _1
    )
    {
        if (OperationArguments.Count(args, 1, 2) is { } error)
        {
            return error;
        }
        if (args.Length == 1 && OperationArguments.TryToggle(args[0], out var toggle))
        {
            return new EffectValue(Enabled: toggle);
        }

        var color = OperationArguments.Enumeration<FilterColor>(args[0]);
        Result<bool?> temporary = args.Length > 1
            ? OperationArguments.Choice(args[1], "temporary", "permanent")
                .Map(static value => (bool?)value)
            : (bool?)null;

        return color.Zip(temporary).Map(static values => new EffectValue(Color: values.Left, Temporary: values.Right));
    }

    private static void Write(
        StringBuilder output,
        string name,
        ColorValue? value
    )
    {
        if (value is not null)
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Red ?? 0).Append(' ')
                .Append(value.Green ?? 0).Append(' ').Append(value.Blue ?? 0).Append(' ')
                .Append(value.Alpha ?? 0).AppendLine();
        }
    }

    private static void Write(
        StringBuilder output,
        string name,
        int? value
    )
    {
        if (value.HasValue)
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Value).AppendLine();
        }
    }

    private static void WriteSound(
        StringBuilder output,
        string _,
        SoundValue? value
    )
    {
        if (value is { Enabled: false })
        {
            output.AppendLine(value.Positional == true ? "\tPlayAlertSoundPositional None" : "\tPlayAlertSound None");
            return;
        }

        if (value is { Id: not null, Enabled: not false }
            && (value.CustomEnabled != true || value.Optional == true))
        {
            output.Append(value.Positional == true ? "\tPlayAlertSoundPositional " : "\tPlayAlertSound ")
                .Append(value.Id.Value);
            if (value.Volume.HasValue)
            {
                output.Append(' ').Append(value.Volume.Value);
            }

            output.AppendLine();
        }

    }

    private static void WriteCustomSound(
        StringBuilder output,
        string _,
        SoundValue? value
    )
    {
        if (value is { Path: not null, CustomEnabled: not false })
        {
            output.Append(value.Optional == true ? "\tCustomAlertSoundOptional \"" : "\tCustomAlertSound \"")
                .Append(value.Path).Append('"');
            if (value.CustomVolume.HasValue)
            {
                output.Append(' ').Append(value.CustomVolume.Value);
            }

            output.AppendLine();
        }
        else if (value is { CustomEnabled: false, Path: not null } or { CustomEnabled: false, UseBuiltIn: false })
        {
            output.AppendLine("\tCustomAlertSound \"None\"");
        }
    }

    private static void WriteDropSound(
        StringBuilder output,
        string _,
        bool? value
    )
    {
        if (value.HasValue)
        {
            output.AppendLine(value.Value ? "\tEnableDropSound" : "\tDisableDropSound");
        }
    }

    private static void WriteDropSoundIfAlert(
        StringBuilder output,
        string _,
        bool? value
    )
    {
        if (value.HasValue)
        {
            output.AppendLine(value.Value ? "\tEnableDropSoundIfAlertSound" : "\tDisableDropSoundIfAlertSound");
        }
    }

    private static void Write(
        StringBuilder output,
        string name,
        IconValue? value
    )
    {
        if (value is { Enabled: false })
        {
            output.Append('\t').Append(name).AppendLine(" -1");
            return;
        }

        if (value is { Size: not null, Color: not null, Shape: not null, Enabled: not false })
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Size.Value)
                .Append(' ').Append(value.Color.Value).Append(' ').Append(value.Shape.Value).AppendLine();
        }
    }

    private static void Write(
        StringBuilder output,
        string name,
        EffectValue? value
    )
    {
        if (value is { Enabled: false })
        {
            output.Append('\t').Append(name).AppendLine(" None");
            return;
        }

        if (value is { Color: not null, Enabled: not false })
        {
            output.Append('\t').Append(name).Append(' ').Append(value.Color.Value);
            if (value.Temporary == true)
            {
                output.Append(" Temp");
            }

            output.AppendLine();
        }
    }
}
