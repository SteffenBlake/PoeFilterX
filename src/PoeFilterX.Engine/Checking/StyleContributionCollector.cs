using System.Collections.Immutable;
using System.Globalization;
using PoeFilterX.Engine.Compilation;

namespace PoeFilterX.Engine.Checking;

public sealed class StyleContributionCollector
{
    private readonly List<StyleContribution> values = [];

    internal ImmutableArray<StyleContribution> Values => [.. values];

    public void Add<T>(string name, T? value) where T : struct
    {
        if (value.HasValue)
        {
            values.Add(new(name, Convert.ToString(value.Value, CultureInfo.InvariantCulture)!, null));
        }
    }

    public void Add(string name, ColorValue? value)
    {
        if (value is null)
        {
            return;
        }

        Add(name + ".Red", value.Red);
        Add(name + ".Green", value.Green);
        Add(name + ".Blue", value.Blue);
        Add(name + ".Alpha", value.Alpha);
    }

    public void Add(string name, SoundValue? value)
    {
        if (value is null)
        {
            return;
        }

        Add(name + ".Id", value.Id);
        Add(name + ".Volume", value.Volume);
        Add(name + ".CustomVolume", value.CustomVolume);
        Add(name + ".Positional", value.Positional);
        Add(name + ".Enabled", value.Enabled);
        Add(name + ".Optional", value.Optional);
        Add(name + ".CustomEnabled", value.UseBuiltIn ? false : value.CustomEnabled);
        if (value.Path is not null)
        {
            values.Add(new(name + ".Path", value.Path, null));
        }
    }

    public void Add(string name, IconValue? value)
    {
        if (value is null)
        {
            return;
        }

        Add(name + ".Size", value.Size);
        Add(name + ".Color", value.Color);
        Add(name + ".Shape", value.Shape);
        Add(name + ".Enabled", value.Enabled);
    }

    public void Add(string name, EffectValue? value)
    {
        if (value is null)
        {
            return;
        }

        Add(name + ".Color", value.Color);
        Add(name + ".Temporary", value.Temporary);
        Add(name + ".Enabled", value.Enabled);
    }
}
