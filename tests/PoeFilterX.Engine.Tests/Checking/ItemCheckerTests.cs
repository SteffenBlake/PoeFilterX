using System.Text.Json;
using PoeFilterX.Engine.Checking;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Tests.Projects;

namespace PoeFilterX.Engine.Tests.Checking;

[Parallelizable(ParallelScope.All)]
public class ItemCheckerTests
{
    private const string Currency = "Item Class: Stackable Currency\nRarity: Currency\nChaos Orb\n--------\nStack Size: 7/20\n--------\nReforges a rare item\n";
    private const string Armour = "Item Class: Body Armours\nRarity: Rare\nDoom Mantle\nVaal Regalia\n--------\nQuality: +20% (augmented)\nEnergy Shield: 500 (augmented)\n--------\nRequirements:\nLevel: 68\nInt: 194\n--------\nSockets: B-B-B R-G-G\n--------\nItem Level: 86\n--------\n{ Prefix Modifier \"Hale\" (Tier: 1) — Life }\n+9 to maximum Life\n--------\nCorrupted\n";

    [TestCase(GameVersion.Poe1)]
    [TestCase(GameVersion.Poe2)]
    public void Parses_currency_without_inventing_hidden_base_metadata(GameVersion game)
    {
        var result = CopiedItemParser.Parse(Currency, game);
        Assert.That(result.Value, Is.TypeOf<ItemFacts>());
        var item = (ItemFacts)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.Name, Is.EqualTo("Chaos Orb"));
            Assert.That(item.Numbers.ContainsKey(FilterFact.Width), Is.False);
            Assert.That(item.Numbers.ContainsKey(FilterFact.Height), Is.False);
            Assert.That(item.Numbers.ContainsKey(FilterFact.DropLevel), Is.False);
            Assert.That(item.Numbers[FilterFact.StackSize], Is.EqualTo(7));
        }
    }

    [Test]
    public void Parses_advanced_modifiers_links_and_requirements_without_confusing_levels()
    {
        var result = CopiedItemParser.Parse(Armour, GameVersion.Poe1);
        Assert.That(result.Value, Is.TypeOf<ItemFacts>());
        var item = (ItemFacts)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.Numbers[FilterFact.ItemLevel], Is.EqualTo(86));
            Assert.That(item.Numbers.ContainsKey(FilterFact.DropLevel), Is.False);
            Assert.That(item.Numbers.ContainsKey(FilterFact.Height), Is.False);
            Assert.That(item.Numbers.ContainsKey(FilterFact.Width), Is.False);
            Assert.That(item.Numbers[FilterFact.LinkedSockets], Is.EqualTo(3));
            Assert.That(item.Texts[FilterFact.HasExplicitMod], Is.EqualTo(["Hale"]));
            Assert.That(item.Flags[FilterFact.Corrupted], Is.True);
            Assert.That(item.Numbers.ContainsKey(FilterFact.BaseEnergyShield), Is.False,
                "Augmented energy shield is not the base defence roll.");
        }
    }

    [TestCase(GameVersion.Poe1)]
    [TestCase(GameVersion.Poe2)]
    public void Selected_game_does_not_restrict_item_names(GameVersion game)
    {
        const string text = "Item Class: Boots\nRarity: Normal\nSteeltoe Boots\n--------\nItem Level: 36\n";
        var result = CopiedItemParser.Parse(text, game);
        Assert.That(result.Value, Is.TypeOf<ItemFacts>());
        var item = (ItemFacts)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.BaseType, Is.EqualTo("Steeltoe Boots"));
            Assert.That(item.ItemClass, Is.EqualTo("Boots"));
            Assert.That(item.Numbers.ContainsKey(FilterFact.DropLevel), Is.False);
        }
    }

    [TestCase("")]
    [TestCase("hello")]
    [TestCase("Item Class: Boots\nRarity: Banana\nSteeltoe Boots\n--------\n")]
    public void Invalid_copied_items_return_errors(string text)
    {
        Assert.That(CopiedItemParser.Parse(text, GameVersion.Poe2).Value, Is.TypeOf<ParseError>());
    }

    [TestCase("Stack Size: bananas")]
    [TestCase("Stack Size: -3")]
    [TestCase("Stack Size: 9999999999999999999999")]
    public void Invalid_numeric_item_properties_return_errors(string property)
    {
        var item = Currency.Replace("Stack Size: 7/20", property, StringComparison.Ordinal);
        Assert.That(CopiedItemParser.Parse(item, GameVersion.Poe1).Value, Is.TypeOf<ParseError>());
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    public void Handles_platform_line_endings_and_a_missing_final_newline(string newline)
    {
        var text = Currency.TrimEnd('\n').Replace("\n", newline, StringComparison.Ordinal);
        Assert.That(CopiedItemParser.Parse(text, GameVersion.Poe1).Value, Is.TypeOf<ItemFacts>());
    }

    [Test]
    public void Follows_compiler_rule_order_and_preserves_partial_color_overrides()
    {
        var project = Project("using styles.fss; Style fallback; { Class Currency; Style base accent; }",
            "fallback { font-size: 18; } base { font-color: 1 2 3; font-size: 20; } accent { font-color-red: 99; font-size: 40; show: false; }");
        var result = ItemChecker.Check(project, Currency, GameVersion.Poe1);
        Assert.That(result.Value, Is.TypeOf<CheckResult>());
        var check = (CheckResult)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Status, Is.EqualTo(CheckStatus.Matched));
            Assert.That(check.Blocks[0].Status, Is.EqualTo(CheckStatus.Matched));
            Assert.That(check.Preview!.Foreground, Is.EqualTo("#630203ff"));
            Assert.That(check.Preview.Hidden, Is.True);
            Assert.That(check.Preview.Opacity, Is.EqualTo(1));
            var red = check.Styles.SelectMany(style => style.Values).First(value => value.Property == "FontColor.Red");
            Assert.That(red.OverriddenBy, Is.Not.Null);
            Assert.That(check.Styles.First(style => style.Id == red.OverriddenBy).Name, Is.EqualTo("accent"));
            var green = check.Styles.SelectMany(style => style.Values).First(value => value.Property == "FontColor.Green");
            Assert.That(green.OverriddenBy, Is.Null);
            Assert.That(check.Blocks[0].Trace, Is.Not.Empty);
            Assert.That(check.Styles.All(style => style.Location.Line > 0), Is.True);
        }
    }

    [Test]
    public void Traces_only_matched_ancestry_bottom_up_and_marks_replaced_scalars()
    {
        var project = Project("""
            using styles.fss;
            Corrupted true;
            Style base;
            {
                Class Currency;
                {
                    Corrupted false;
                    StackSize >= 7;
                    Style accent;
                }
            }
            { Class Gems; Style unrelated; }
            """, "base { font-size: 20; } accent { show: false; } unrelated { font-size: 30; }");
        var result = (CheckResult)ItemChecker.Check(project, Currency, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Matched));
            Assert.That(result.Frames, Has.Length.EqualTo(3));
            Assert.That(result.Frames[0].Declarations.Any(value => value.Name == "StackSize"), Is.True);
            Assert.That(result.Frames[1].Declarations.Any(value => value.Name == "Class"), Is.True);
            Assert.That(result.Frames[2].Declarations.Single(value => value.Name == "Corrupted").OverriddenBy,
                Is.EqualTo(result.Frames[0].Id));
            Assert.That(result.Frames.SelectMany(frame => frame.Declarations).Any(value => value.Value == "unrelated"), Is.False);
            Assert.That(result.DefaultPreview!.Hidden, Is.False);
            Assert.That(result.Preview!.Hidden, Is.True);
            Assert.That(result.Styles.Select(style => style.Name), Is.EqualTo(["base", "accent"]));
        }
    }

    [TestCase("StackSize >= 7;", true)]
    [TestCase("StackSize > 7;", false)]
    [TestCase("AreaLevel == 83;", true)]
    [TestCase("Class == Currency;", false)]
    [TestCase("Class Currency;", true)]
    [TestCase("BaseType != \"Chaos Orb\";", false)]
    [TestCase("BaseType == \"Chaos Orb\";", true)]
    [TestCase("Corrupted false;", true)]
    public void Evaluates_conditions(string condition, bool matches)
    {
        var project = Project($"using styles.fss; {{ {condition} Style test; }}", "test { font-size: 32; }");
        var result = (CheckResult)ItemChecker.Check(project, Currency, GameVersion.Poe1).Value!;
        Assert.That(result.Status == CheckStatus.Matched, Is.EqualTo(matches));
    }

    [TestCase("SocketGroup >= 3BBB;", true)]
    [TestCase("SocketGroup >= 4BBB;", false)]
    [TestCase("Sockets == 6BBBRGG;", true)]
    [TestCase("HasExplicitMod Hale;", true)]
    [TestCase("HasExplicitMod >= 2 Hale;", false)]
    [TestCase("Rarity >= Rare;", true)]
    public void Evaluates_equipment_conditions(string condition, bool matches)
    {
        var project = Project($"using styles.fss; {{ {condition} Style test; }}", "test { font-size: 32; }");
        var result = ItemChecker.Check(project, Armour, GameVersion.Poe1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.TypeOf<CheckResult>());
            Assert.That(((CheckResult)result.Value!).Status == CheckStatus.Matched, Is.EqualTo(matches));
        }
    }

    [Test]
    public void Missing_facts_return_located_errors_and_can_be_supplied_explicitly()
    {
        var project = Project("using styles.fss; { BaseEnergyShield > 200; Style test; }", "test { font-size: 32; }");
        var result = ItemChecker.Check(project, Armour, GameVersion.Poe1);
        Assert.That(result.Value, Is.TypeOf<ParseError>());
        var error = (ParseError)result.Value!;
        Assert.That(error.Diagnostics[0].Path, Is.EqualTo(project.EntryPoint));
        var resolved = ItemChecker.Check(project, Armour, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { ["BaseEnergyShield"] = "230" });
        Assert.That(resolved.Value, Is.TypeOf<CheckResult>());
    }

    [Test]
    public void Unknown_facts_in_rejected_blocks_do_not_prevent_a_definite_match()
    {
        var project = Project("using styles.fss; Style fallback; { Class Boots; BaseEnergyShield > 200; Style test; }",
            "fallback { font-size: 18; } test { font-size: 32; }");
        Assert.That(ItemChecker.Check(project, Armour, GameVersion.Poe1).Value, Is.TypeOf<CheckResult>());
    }

    [Test]
    public void Json_response_uses_generated_metadata_and_named_enum_values()
    {
        var project = Project("using styles.fss; Style test;", "test { font-size: 32; }");
        var result = (CheckResult)ItemChecker.Check(project, Currency, GameVersion.Poe1).Value!;
        var text = JsonSerializer.Serialize(new CheckResponse(1, result, []), CheckJsonContext.Default.CheckResponse);
        using var json = JsonDocument.Parse(text);
        Assert.That(json.RootElement.GetProperty("result").GetProperty("status").GetString(), Is.EqualTo("Matched"));
    }

    [Test]
    public void Rejects_multiple_copied_items_instead_of_combining_their_properties()
    {
        Assert.That(CopiedItemParser.Parse(Currency + Currency, GameVersion.Poe1).Value, Is.TypeOf<ParseError>());
    }

    [Test]
    public void PoE2_unusable_item_message_is_not_treated_as_the_item_name()
    {
        const string item = "Item Class: Quivers\nRarity: Normal\nYou cannot use this item. Its stats will be ignored\n--------\nFire Quiver\n--------\nRequires: Level 8\n--------\nItem Level: 8\n";
        var result = CopiedItemParser.Parse(item, GameVersion.Poe2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.TypeOf<ItemFacts>());
            Assert.That(((ItemFacts)result.Value!).Name, Is.EqualTo("Fire Quiver"));
        }
    }

    [Test]
    public void Repeated_style_classes_preserve_declaration_order_and_include_locations()
    {
        var source = new MemoryProjectSource();
        var entrypoint = Path.GetFullPath("checker-imports/entrypoint.filterx");
        var child = Path.GetFullPath("checker-imports/rules/child.filterx");
        source.Add(entrypoint, "using styles.fss; using rules/child.filterx;");
        source.Add(child, "{ Class Currency; Style item; }");
        source.Add(Path.GetFullPath("checker-imports/styles.fss"),
            "item { font-size: 20; font-color: 1 2 3; }\nitem { font-size: 40; }");
        var snapshot = new ProjectLoader(source).Load(entrypoint);
        var result = (CheckResult)ItemChecker.Check(snapshot, Currency, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Styles, Has.Length.EqualTo(2));
            Assert.That(result.Styles[0].Values.Single(value => value.Property == "FontSize").OverriddenBy,
                Is.EqualTo(result.Styles[1].Id));
            Assert.That(result.Styles[1].Location.Line, Is.EqualTo(2));
            Assert.That(result.Blocks[0].Location.Path, Is.EqualTo(child));
            Assert.That(result.Blocks[0].Trace.Any(frame => frame.Path == entrypoint && frame.Column > 1), Is.True);
        }
    }

    [TestCase("NoSuchFact", "10")]
    [TestCase("Corrupted", "nope")]
    [TestCase("Width", "-10")]
    [TestCase("Rarity", "Currency")]
    [TestCase("HasInfluence", "Unknown")]
    [TestCase("AreaLevel", "20")]
    public void Invalid_fact_overrides_return_parse_errors(string name, string value)
    {
        var project = Project("using styles.fss; Style test;", "test { font-size: 32; }");
        var result = ItemChecker.Check(project, Currency, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { [name] = value });
        Assert.That(result.Value, Is.TypeOf<ParseError>());
    }

    [Test]
    public void Resolves_plain_copied_modifier_names_using_the_baked_game_data()
    {
        const string item = "Item Class: Body Armours\nRarity: Magic\nHale Plate Vest\n--------\nArmour: 19\n--------\nItem Level: 1\n--------\n+9 to maximum Life\n";
        var project = Project("using styles.fss; { HasExplicitMod Hale; Style test; }", "test { font-size: 32; }");
        var result = ItemChecker.Check(project, item, GameVersion.Poe1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.TypeOf<CheckResult>(), result.Value?.ToString());
            Assert.That(((CheckResult)result.Value!).Status, Is.EqualTo(CheckStatus.Matched));
        }
    }

    [TestCase("Blighted", FilterFact.BlightedMap)]
    [TestCase("Blight-ravaged", FilterFact.UberBlightedMap)]
    public void Recognizes_map_prefixes_without_inventing_drop_levels(string prefix, FilterFact fact)
    {
        var item = $"Item Class: Maps\nRarity: Normal\n{prefix} Strand Map\n--------\nMap Tier: 16\n--------\nItem Level: 85\n";
        var parsed = (ItemFacts)CopiedItemParser.Parse(item, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed.Flags[fact], Is.True);
            Assert.That(parsed.Numbers.ContainsKey(FilterFact.DropLevel), Is.False);
            Assert.That(parsed.BaseType, Is.EqualTo("Strand Map"));
            Assert.That(parsed.Numbers[FilterFact.MapTier], Is.EqualTo(16));
        }
    }

    [Test]
    public void Disabling_an_icon_links_its_old_values_to_the_disabling_class()
    {
        var project = Project("using styles.fss; { Class Currency; Style base disabled; }",
            "base { icon: 1 Red Star; } disabled { icon: disabled; font-size: 32; }");
        var result = (CheckResult)ItemChecker.Check(project, Currency, GameVersion.Poe1).Value!;
        var color = result.Styles[0].Values.Single(value => value.Property == "Icon.Color");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(color.OverriddenBy, Is.EqualTo(result.Styles[1].Id));
            Assert.That(result.Preview!.Icon, Is.Null);
        }
    }

    [TestCase("Leap Slam of Groundbreaking")]
    [TestCase("Fireball of Radiation")]
    [TestCase("Fireball")]
    public void Gem_names_and_levels_do_not_require_catalog_metadata(string name)
    {
        var text = $"Item Class: Skill Gems\nRarity: Gem\n{name}\n--------\nLevel: 20\n--------\nRequirements:\nLevel: 70\n";
        var parsed = CopiedItemParser.Parse(text, GameVersion.Poe1);
        Assert.That(parsed.Value, Is.TypeOf<ItemFacts>());
        var item = (ItemFacts)parsed.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.BaseType, Is.EqualTo(name));
            Assert.That(item.Flags.ContainsKey(FilterFact.TransfiguredGem), Is.False);
            Assert.That(item.Numbers[FilterFact.GemLevel], Is.EqualTo(20));
        }
    }

    [Test]
    public void Unknown_gem_variants_do_not_match_a_similarly_named_base()
    {
        const string item = "Item Class: Skill Gems\nRarity: Gem\nFireball of Nonsense\n--------\nLevel: 20\n";
        var parsed = CopiedItemParser.Parse(item, GameVersion.Poe1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed.Value, Is.TypeOf<ItemFacts>());
            Assert.That(((ItemFacts)parsed.Value!).BaseType, Is.EqualTo("Fireball of Nonsense"));
        }
    }

    [Test]
    public void Hidden_weapon_facts_are_not_inferred_from_missing_clipboard_text()
    {
        const string item = "Item Class: One Hand Swords\nRarity: Normal\nRusted Sword\n--------\nItem Level: 80\n";
        var project = Project("using styles.fss; { HasCruciblePassiveTree false; Style test; }", "test { font-size: 32; }");
        Assert.That(ItemChecker.Check(project, item, GameVersion.Poe1).Value, Is.TypeOf<ParseError>());
        var resolved = ItemChecker.Check(project, item, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { ["HasCruciblePassiveTree"] = "false" });
        Assert.That(resolved.Value, Is.TypeOf<CheckResult>());
    }

    [Test]
    public void Preview_uses_the_default_gem_color_when_styles_do_not_set_one()
    {
        const string gem = "Item Class: Skill Gems\nRarity: Gem\nFireball\n--------\nLevel: 20\n";
        var project = Project("using styles.fss; Style test;", "test { font-size: 32; }");
        var result = (CheckResult)ItemChecker.Check(project, gem, GameVersion.Poe1).Value!;
        Assert.That(result.Preview!.Foreground, Is.EqualTo("#1ba29bff"));
    }

    [Test]
    public void Hidden_items_keep_icon_style_data_but_do_not_preview_a_map_icon()
    {
        var project = Project("using styles.fss; Style test;", "test { show: false; icon: 1 Yellow Diamond; }");
        var result = (CheckResult)ItemChecker.Check(project, Currency, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Preview!.Hidden, Is.True);
            Assert.That(result.Preview.Icon, Is.Null);
            Assert.That(result.EffectiveStyle!.Icon!.Shape, Is.EqualTo(MiniMapIconShape.Diamond));
        }
    }

    [TestCase(GameVersion.Poe1, "Future Armours", "Future Vest")]
    [TestCase(GameVersion.Poe2, "Future Armours", "Future Vest")]
    [TestCase(GameVersion.Poe1, "Boots", "Plate Vest")]
    [TestCase(GameVersion.Poe2, "Future Armours", "Plate Vest")]
    public void Unfamiliar_identity_matches_filter_conditions(
        GameVersion game,
        string itemClass,
        string baseType
    )
    {
        var text = $"Item Class: {itemClass}\nRarity: Normal\n{baseType}\n--------\nItem Level: 80\n";
        var project = Project(
            $"using styles.fss; {{ Class == \"{itemClass}\"; BaseType == \"{baseType}\"; Style test; }}",
            "test { font-size: 32; }"
        );
        var checkedItem = ItemChecker.Check(project, text, game);
        Assert.That(checkedItem.Value, Is.TypeOf<CheckResult>());
        var result = (CheckResult)checkedItem.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Matched));
            Assert.That(result.Item.BaseType, Is.EqualTo(baseType));
            Assert.That(result.Item.ItemClass, Is.EqualTo(itemClass));
        }
    }

    [TestCase("Superior ")]
    [TestCase("Blighted ")]
    [TestCase("Blight-ravaged ")]
    [TestCase("Synthesised ")]
    [TestCase("Shaped ")]
    public void Prefixed_unknown_bases_are_not_replaced_with_known_substrings(string prefix)
    {
        var text = $"Item Class: Body Armours\nRarity: Normal\n{prefix}Future Plate Vest\n--------\nItem Level: 80\n";
        var project = Project(
            "using styles.fss; { BaseType == \"Plate Vest\"; Style test; }",
            "test { font-size: 32; }"
        );
        var result = (CheckResult)ItemChecker.Check(project, text, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Item.BaseType, Is.EqualTo("Future Plate Vest"));
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Unmatched));
        }
    }

    [TestCase("Normal", "Future Quiver", "Future Quiver")]
    [TestCase("Rare", "Doom Volley\nFuture Quiver", "Doom Volley")]
    [TestCase("Unique", "Doom Volley\nFuture Quiver", "Doom Volley")]
    public void Unusable_items_recover_identity_from_the_first_body_section(
        string rarity,
        string identity,
        string name
    )
    {
        var text = $"Item Class: Future Quivers\nRarity: {rarity}\nYou cannot use this item. Its stats will be ignored\n--------\n{identity}\n--------\nItem Level: 8\n--------\nFire Quiver\n";
        var result = CopiedItemParser.Parse(text, GameVersion.Poe2);
        Assert.That(result.Value, Is.TypeOf<ItemFacts>());
        var item = (ItemFacts)result.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.BaseType, Is.EqualTo("Future Quiver"));
            Assert.That(item.Name, Is.EqualTo(name));
            Assert.That(item.ItemClass, Is.EqualTo("Future Quivers"));
        }
    }

    [TestCase("Width", "2")]
    [TestCase("Height", "3")]
    [TestCase("DropLevel", "80")]
    [TestCase("BaseDefencePercentile", "90")]
    [TestCase("HasImplicitMod", "false")]
    [TestCase("AlternateQuality", "false")]
    [TestCase("TransfiguredGem", "false")]
    public void Unavailable_metadata_can_be_overridden_for_an_unknown_item(string fact, string value)
    {
        const string text = "Item Class: Future Armours\nRarity: Normal\nFuture Vest\n--------\nItem Level: 80\n";
        var project = Project($"using styles.fss; {{ {fact} {value}; Style test; }}", "test { font-size: 32; }");
        var missing = ItemChecker.Check(project, text, GameVersion.Poe1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing.Value, Is.TypeOf<ParseError>());
            Assert.That(((ParseError)missing.Value!).Diagnostics[0].Code, Is.EqualTo("PFX5002"));
        }

        var supplied = ItemChecker.Check(
            project, text, GameVersion.Poe1, factOverrides: new Dictionary<string, string> { [fact] = value }
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(supplied.Value, Is.TypeOf<CheckResult>());
            Assert.That(((CheckResult)supplied.Value!).Status, Is.EqualTo(CheckStatus.Matched));
        }
    }

    [Test]
    public void Magic_base_identity_stays_unknown_until_supplied_explicitly()
    {
        const string text = "Item Class: Future Armours\nRarity: Magic\nHale Future Plate Vest\n--------\nItem Level: 80\n";
        var parsed = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        Assert.That(parsed.BaseType, Is.Null);
        var project = Project(
            "using styles.fss; { BaseType == \"Future Plate Vest\"; Style test; }", "test { font-size: 32; }"
        );
        var result = ItemChecker.Check(
            project, text, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { ["BaseType"] = "Future Plate Vest" }
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Value, Is.TypeOf<CheckResult>());
            Assert.That(((CheckResult)result.Value!).Status, Is.EqualTo(CheckStatus.Matched));
        }
    }

    [Test]
    public void Identity_overrides_update_both_conditions_and_the_reported_item()
    {
        var project = Project(
            "using styles.fss; { BaseType == \"Future Base\"; Class == \"Future Class\"; Style test; }",
            "test { font-size: 32; }"
        );
        var checkedItem = ItemChecker.Check(
            project, Currency, GameVersion.Poe1,
            factOverrides: new Dictionary<string, string> { ["BaseType"] = "Future Base", ["Class"] = "Future Class" }
        );
        Assert.That(checkedItem.Value, Is.TypeOf<CheckResult>());
        var result = (CheckResult)checkedItem.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Matched));
            Assert.That(result.Item.BaseType, Is.EqualTo("Future Base"));
            Assert.That(result.Item.ItemClass, Is.EqualTo("Future Class"));
        }
    }

    [TestCase("Class", "")]
    [TestCase("Class", "Boots|Future Class")]
    [TestCase("BaseType", "")]
    [TestCase("BaseType", "Plate Vest|Future Base")]
    public void Identity_overrides_describe_one_item(string fact, string value)
    {
        var item = (ItemFacts)CopiedItemParser.Parse(Currency, GameVersion.Poe1).Value!;
        var result = ItemFactOverrides.Apply(item, new Dictionary<string, string> { [fact] = value });
        Assert.That(result.Value, Is.TypeOf<ParseError>());
    }

    [Test]
    public void Gem_level_does_not_depend_on_a_known_class_name()
    {
        const string text = "Item Class: Future Skills\nRarity: Gem\nFuture Skill\n--------\nLevel: 20\n--------\nRequirements:\nLevel: 70\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        Assert.That(item.Numbers[FilterFact.GemLevel], Is.EqualTo(20));
    }

    [TestCase("Armour", FilterFact.BaseArmour)]
    [TestCase("Evasion Rating", FilterFact.BaseEvasion)]
    [TestCase("Energy Shield", FilterFact.BaseEnergyShield)]
    [TestCase("Ward", FilterFact.BaseWard)]
    public void Unmodified_defences_are_read_without_base_metadata(string label, FilterFact fact)
    {
        var text = $"Item Class: Future Armours\nRarity: Normal\nFuture Vest\n--------\n{label}: 100\n--------\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        Assert.That(item.Numbers[fact], Is.EqualTo(100));
    }

    [TestCase("Magic", "Newfound Future Plate Vest of Tomorrow")]
    public void Advanced_magic_copies_supply_affix_boundaries_without_known_base_names(string rarity, string label)
    {
        var text = $"Item Class: Future Armours\nRarity: {rarity}\n{label}\n--------\nItem Level: 80\n--------\n{{ Prefix Modifier \"Newfound\" (Tier: 1) }}\n+9 to maximum Life\n{{ Suffix Modifier \"of Tomorrow\" (Tier: 1) }}\n+10 to Intelligence\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        Assert.That(item.BaseType, Is.EqualTo("Future Plate Vest"));
    }

    [Test]
    public void Unidentified_magic_copies_preserve_unfamiliar_base_names()
    {
        const string text = "Item Class: Future Armours\nRarity: Magic\nFuture Plate Vest\n--------\nUnidentified\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe2).Value!;
        Assert.That(item.BaseType, Is.EqualTo("Future Plate Vest"));
    }

    [Test]
    public void Unidentified_fated_wand_preserves_its_base_and_copied_properties()
    {
        const string text = """
            Item Class: Wands
            Rarity: Rare
            Fated Faun's Horn
            --------
            Wand
            Physical Damage: 21-38
            Critical Strike Chance: 8.00%
            Attacks per Second: 1.40
            --------
            Requirements:
            Intelligence: 116 (unmet)
            --------
            Sockets: W-W W
            --------
            Item Level: 43
            --------
            { Implicit Modifier — Damage, Elemental, Fire, Attack, Caster }
            Adds 7(5-10) to 13(11-13) Fire Damage to Spells and Attacks
            --------
            Unidentified
            """;
        var project = Project(
            "using styles.fss; { BaseType == \"Faun's Horn\"; Class == Wands; Fated true; Identified false; ItemLevel == 43; Style test; }",
            "test { font-size: 32; }"
        );
        var checkedItem = ItemChecker.Check(project, text, GameVersion.Poe1);
        Assert.That(checkedItem.Value, Is.TypeOf<CheckResult>());
        var result = (CheckResult)checkedItem.Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Item.Name, Is.EqualTo("Fated Faun's Horn"));
            Assert.That(result.Item.BaseType, Is.EqualTo("Faun's Horn"));
            Assert.That(result.Status, Is.EqualTo(CheckStatus.Matched));
        }
    }

    [TestCase("Fated Superior ")]
    [TestCase("Superior Fated ")]
    [TestCase("Fractured Superior Fated ")]
    [TestCase("Fated Fractured Superior ")]
    public void Stacked_status_prefixes_are_removed_in_any_order(string prefixes)
    {
        var text = $"Item Class: Future Wands\nRarity: Rare\n{prefixes}Future Faun's Horn\n--------\nUnidentified\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.Name, Is.EqualTo(prefixes + "Future Faun's Horn"));
            Assert.That(item.BaseType, Is.EqualTo("Future Faun's Horn"));
            Assert.That(item.Flags[FilterFact.Fated], Is.True);
            Assert.That(item.Flags[FilterFact.FracturedItem], Is.EqualTo(prefixes.Contains("Fractured", StringComparison.Ordinal)));
        }
    }

    [TestCase("Superior Blighted ", FilterFact.BlightedMap)]
    [TestCase("Superior Blight-ravaged ", FilterFact.UberBlightedMap)]
    [TestCase("Superior Synthesised ", FilterFact.SynthesisedItem)]
    [TestCase("Superior Shaped ", FilterFact.ShapedMap)]
    public void Status_flags_survive_an_outer_quality_prefix(string prefixes, FilterFact fact)
    {
        var text = $"Item Class: Future Maps\nRarity: Normal\n{prefixes}Future Map\n--------\nMap Tier: 16\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.BaseType, Is.EqualTo("Future Map"));
            Assert.That(item.Flags[fact], Is.True);
        }
    }

    [TestCase("Normal", "Wands", "Imbued Wand")]
    [TestCase("Currency", "Stackable Currency", "Fractured Fossil")]
    [TestCase("Normal", "Future Wands", "Anomalous Future Wand")]
    [TestCase("Rare", "Future Wands", "Future Faun's Horn")]
    public void Intrinsic_base_words_are_not_mistaken_for_status_prefixes(
        string rarity,
        string itemClass,
        string label
    )
    {
        var text = $"Item Class: {itemClass}\nRarity: {rarity}\n{label}\n--------\nUnidentified\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        Assert.That(item.BaseType, Is.EqualTo(label));
    }

    [Test]
    public void A_rare_display_name_does_not_supply_the_bases_status_prefixes()
    {
        const string text = "Item Class: Wands\nRarity: Rare\nFated Branch\nFaun's Horn\n--------\nItem Level: 43\n";
        var item = (ItemFacts)CopiedItemParser.Parse(text, GameVersion.Poe1).Value!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(item.Name, Is.EqualTo("Fated Branch"));
            Assert.That(item.BaseType, Is.EqualTo("Faun's Horn"));
            Assert.That(item.Flags[FilterFact.Fated], Is.False);
        }
    }

    private static ProjectSnapshot Project(string filter, string styles)
    {
        var source = new MemoryProjectSource();
        var path = Path.GetFullPath("checker-tests/entrypoint.filterx");
        source.Add(path, filter);
        source.Add(Path.Combine(Path.GetDirectoryName(path)!, "styles.fss"), styles);

        return new ProjectLoader(source).Load(path);
    }
}
