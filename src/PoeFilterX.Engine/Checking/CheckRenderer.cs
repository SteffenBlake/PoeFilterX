namespace PoeFilterX.Engine.Checking;

public static class CheckRenderer
{
    public static void Write(CheckResult result, TextWriter writer)
    {
        writer.WriteLine($"{result.Item.Name} — {result.Item.Game}, area level {result.AreaLevel}");
        writer.WriteLine($"Base: {result.Item.BaseType} ({result.Item.ItemClass})");
        var matched = result.Blocks.FirstOrDefault(block => block.Status == CheckStatus.Matched);
        if (matched is null)
        {
            writer.WriteLine("No filter block matched. The game's default appearance applies.");
            return;
        }

        writer.WriteLine($"Matched {Location(matched.Location)} [{matched.Id}]");
        foreach (var frame in matched.Trace)
        {
            writer.WriteLine($"  from {Location(frame)}");
        }

        foreach (var condition in matched.Conditions)
        {
            writer.WriteLine($"  {condition.Name} {condition.Expected} (item: {condition.Actual})");
        }

        writer.WriteLine("Styles, in application order:");
        foreach (var style in result.Styles)
        {
            writer.WriteLine($"  {style.Name} [{style.Id}] — {Location(style.Location)}");
            writer.WriteLine($"    {style.Declaration}");
            foreach (var value in style.Values)
            {
                var winner = value.OverriddenBy is null ? null : result.Styles.First(applied => applied.Id == value.OverriddenBy);
                writer.WriteLine(winner is null
                    ? $"    {value.Property}: {value.Value} [active]"
                    : $"    {value.Property}: {value.Value} [overridden by {winner.Name} ({winner.Id}) at {Location(winner.Location)}]");
            }
        }

        if (result.Preview is { } preview)
        {
            writer.WriteLine(preview.Hidden ? "Hidden — item name struck through." : "Shown");
            writer.WriteLine($"Appearance: text {preview.Foreground}, background {preview.Background}, border {preview.Border}, size {preview.FontSize}");
        }
    }

    private static string Location(CheckLocation location) => $"{location.Path}({location.Line},{location.Column})";
}
