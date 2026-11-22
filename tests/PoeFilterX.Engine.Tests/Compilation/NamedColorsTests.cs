using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Results;

namespace PoeFilterX.Engine.Tests.Compilation;

[Parallelizable(ParallelScope.All)]
public class NamedColorsTests
{
    [TestCase("black", 0, 0, 0, 255)]
    [TestCase("ReBeccAPurple", 102, 51, 153, 255)]
    [TestCase("green", 0, 128, 0, 255)]
    [TestCase("gray", 128, 128, 128, 255)]
    [TestCase("grey", 128, 128, 128, 255)]
    [TestCase("transparent", 0, 0, 0, 0)]
    public void Names_have_css_channel_values(
        string name,
        int red,
        int green,
        int blue,
        int alpha
    )
    {
        Assert.That(NamedColors.Values[name], Is.EqualTo(new ColorValue(red, green, blue, alpha)));
    }

    [TestCase("font-color")]
    [TestCase("border-color")]
    [TestCase("bg-color")]
    public void Every_name_parses_identically_to_its_hex_value(string operation)
    {
        Assert.That(NamedColors.Values, Has.Count.EqualTo(149));
        foreach (var (name, color) in NamedColors.Values)
        {
            var hex = $"0x{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            var expected = StyleOperationsCodec.Parse(operation, [hex]);
            var actual = StyleOperationsCodec.Parse(operation, [name.ToUpperInvariant()]);
            Assert.That(actual.Value, Is.TypeOf<StyleOperations>().And.EqualTo(expected.Value), name);
        }
    }

    [TestCase("currentColor")]
    [TestCase("notacolor")]
    [TestCase("Canvas")]
    public void Unsupported_names_return_parse_errors(string name)
    {
        Assert.That(StyleOperationsCodec.Parse("font-color", [name]).Value, Is.TypeOf<ParseError>());
    }
}
