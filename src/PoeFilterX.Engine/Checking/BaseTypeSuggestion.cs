namespace PoeFilterX.Engine.Checking;

public sealed record BaseTypeSuggestion(
    string Name,
    string Tags,
    string Games
)
{
    public string SearchTags => Tags + " " + Games;
}
