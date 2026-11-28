using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Tests.Projects;

namespace PoeFilterX.Engine.Tests.Compilation;

[Parallelizable(ParallelScope.All)]
public class FilterCommandTests
{
    [TestCase("AlwaysShow")]
    [TestCase("Exceptional")]
    [TestCase("HasVaalUniqueMod")]
    [TestCase("IsVaalUnique")]
    [TestCase("MirageMap")]
    [TestCase("TwiceCorrupted")]
    [TestCase("Vestigial")]
    public void Boolean_conditions_compile_for_the_combined_game_target(string command)
    {
        Assert.That(Compile($"{command} True;", "font-size: 30;"),
            Is.EqualTo($"Show\n\t{command} True\n\tSetFontSize 30\n\n"));
    }

    [TestCase("ArchnemesisMod \"Toxic\";", "ArchnemesisMod = \"Toxic\"")]
    [TestCase("TransfiguredGem \"Leap Slam\";", "TransfiguredGem = \"Leap Slam\"")]
    [TestCase("TransfiguredGem False;", "TransfiguredGem False")]
    [TestCase("HasExplicitMod >=2 \"of Haast\" \"of Tzteosh\";", "HasExplicitMod >= 2 \"of Haast\" \"of Tzteosh\"")]
    [TestCase("HasEnchantment >= 1 \"Damage\";", "HasEnchantment >= 1 \"Damage\"")]
    [TestCase("ItemLevel != 80;", "ItemLevel ! 80")]
    [TestCase("BaseType != \"Wand\";", "BaseType ! \"Wand\"")]
    public void Condition_arguments_survive_parsing_and_emission(string input, string expected)
    {
        Assert.That(Compile(input, "font-size: 30;"), Does.Contain($"\t{expected}\n"));
    }

    [TestCase("show: Minimal;", "Minimal\n")]
    [TestCase("alert-path: \"a.mp3;b.mp3\" 150; alert-optional: enabled;", "\tCustomAlertSoundOptional \"a.mp3;b.mp3\" 150\n")]
    [TestCase("drop-sound-if-alert: disabled;", "\tDisableDropSoundIfAlertSound\n")]
    [TestCase("drop-sound-if-alert: enabled;", "\tEnableDropSoundIfAlertSound\n")]
    [TestCase("alert: disabled;", "\tPlayAlertSound None\n")]
    [TestCase("alert: 1 100 positional; alert: disabled;", "\tPlayAlertSoundPositional None\n")]
    [TestCase("alert-path: disabled;", "\tCustomAlertSound \"None\"\n")]
    [TestCase("alert-path: test.mp3; alert-path-volume: 175;", "\tCustomAlertSound \"test.mp3\" 175\n")]
    [TestCase("icon: disabled;", "\tMinimapIcon -1\n")]
    [TestCase("pillar: disabled;", "\tPlayEffect None\n")]
    public void Styles_emit_official_actions_and_resets(string style, string expected)
    {
        Assert.That(Compile("ItemLevel >= 1;", style), Does.Contain(expected));
    }

    [TestCase("HasExplicitMod >= -1 \"Hale\";")]
    [TestCase("HasExplicitMod >= 2147483648 \"Hale\";")]
    [TestCase("HasEnchantment >= 2;")]
    [TestCase("TransfiguredGem True False;")]
    [TestCase("TransfiguredGem >=;")]
    [TestCase("Continue;")]
    public void Invalid_or_unsupported_commands_return_diagnostics(string command)
    {
        var result = ProjectCompiler.Prepare(Project(command, "font-size: 30;"));
        Assert.That(result.Value, Is.TypeOf<ParseError>());
    }

    [Test]
    public void Counted_conditions_with_the_same_operator_remain_separate()
    {
        const string commands = "HasExplicitMod >= 1 \"Hale\"; HasExplicitMod >= 2 \"of Haast\" \"of Tzteosh\";";
        var project = Project(commands, "font-size: 30;");
        const string item = "Item Class: Wands\nRarity: Rare\nDoom Branch\nDriftwood Wand\n--------\nItem Level: 80\n";
        var result = ItemChecker.Check(project, item, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { ["HasExplicitMod"] = "Hale|of Haast" });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.TypeOf<CheckResult>());
            Assert.That(((CheckResult)result.Value!).Status, Is.EqualTo(CheckStatus.Unmatched));
            Assert.That(Compile(commands, "font-size: 30;"),
                Does.Contain("HasExplicitMod >= 1 \"Hale\"\n\tHasExplicitMod >= 2 \"of Haast\" \"of Tzteosh\""));
        }
    }

    [Test]
    public void Inherited_style_values_can_be_reset_and_visibility_overridden()
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("command-tests/main.filterx");
        source.Add(path, "using main.fss; Style base reset; ItemLevel 80;");
        source.Add(Path.GetFullPath("command-tests/main.fss"),
            "base { show: false; icon: 1 Red Circle; pillar: Red; alert-path: test.mp3; } "
            + "reset { show: Minimal; icon: disabled; pillar: disabled; alert-path: disabled; }");
        var result = ProjectCompiler.Prepare(new ProjectLoader(source).Load(path));
        Assert.That(result.Value, Is.TypeOf<PreparedProject>());
        using var writer = new StringWriter();
        ProjectCompiler.Write((PreparedProject)result.Value!, writer);
        Assert.That(writer.ToString().ReplaceLineEndings("\n"),
            Does.StartWith("Minimal\n").And.Contain("CustomAlertSound \"None\"")
                .And.Contain("MinimapIcon -1").And.Contain("PlayEffect None"));
    }

    [Test]
    public void Optional_custom_sounds_preserve_the_builtin_fallback_and_separate_volumes()
    {
        var output = Compile("ItemLevel 80;",
            "alert: 3 90 positional; alert-path: missing.mp3 150; alert-optional: enabled;");
        Assert.That(output, Does.Contain("PlayAlertSoundPositional 3 90")
            .And.Contain("CustomAlertSoundOptional \"missing.mp3\" 150"));
    }

    [Test]
    public void Optional_custom_sounds_respect_an_explicitly_disabled_builtin_fallback()
    {
        var output = Compile("ItemLevel 80;",
            "alert: 3 90; alert: disabled; alert-path: missing.mp3 150; alert-optional: enabled;");
        Assert.That(output, Does.Contain("PlayAlertSound None")
            .And.Contain("CustomAlertSoundOptional \"missing.mp3\" 150")
            .And.Not.Contain("PlayAlertSound 3"));
    }

    [Test]
    public void Transfigured_boolean_conditions_override_inherited_values()
    {
        var first = (FilterOperations)FilterOperationsCodec.Parse("TransfiguredGem", ["True"]).Value!;
        var second = (FilterOperations)FilterOperationsCodec.Parse("TransfiguredGem", ["False"]).Value!;
        var merged = FilterOperationsCodec.Apply(first, second);
        Assert.That(merged.TransfiguredGem?.Any, Is.False);
    }

    [Test]
    public void Minimal_can_be_used_as_an_unconditional_rule()
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("command-tests/main.filterx");
        source.Add(path, "using main.fss; Style test;");
        source.Add(Path.GetFullPath("command-tests/main.fss"), "test { show: Minimal; }");
        var prepared = ProjectCompiler.Prepare(new ProjectLoader(source).Load(path));
        Assert.That(prepared.Value, Is.TypeOf<PreparedProject>());
        using var writer = new StringWriter();
        ProjectCompiler.Write((PreparedProject)prepared.Value!, writer);
        Assert.That(writer.ToString().ReplaceLineEndings("\n"), Is.EqualTo("Minimal\n\n"));
    }

    [Test]
    public void Disabled_builtin_sounds_stay_disabled_when_styles_are_merged()
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("command-tests/main.filterx");
        source.Add(path, "using main.fss; Style custom silent; ItemLevel 80;");
        source.Add(Path.GetFullPath("command-tests/main.fss"),
            "custom { alert-path: custom.mp3; } silent { alert: 3 90; alert: disabled; }");
        var prepared = ProjectCompiler.Prepare(new ProjectLoader(source).Load(path));
        Assert.That(prepared.Value, Is.TypeOf<PreparedProject>());
        using var writer = new StringWriter();
        ProjectCompiler.Write((PreparedProject)prepared.Value!, writer);
        Assert.That(writer.ToString(), Does.Contain("PlayAlertSound None")
            .And.Contain("CustomAlertSound \"None\"").And.Not.Contain("PlayAlertSound 3"));
    }

    private static string Compile(string commands, string style)
    {
        var prepared = ProjectCompiler.Prepare(Project(commands, style));
        Assert.That(prepared.Value, Is.TypeOf<PreparedProject>());
        using var writer = new StringWriter();
        var result = ProjectCompiler.Write((PreparedProject)prepared.Value!, writer);
        Assert.That(result.Value, Is.TypeOf<int>());

        return writer.ToString().ReplaceLineEndings("\n");
    }

    private static ProjectSnapshot Project(string commands, string style)
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("command-tests/main.filterx");
        source.Add(path, $"using main.fss; {{ Style test; {commands} }}");
        source.Add(Path.GetFullPath("command-tests/main.fss"), $"test {{ {style} }}");

        return new ProjectLoader(source).Load(path);
    }
}
