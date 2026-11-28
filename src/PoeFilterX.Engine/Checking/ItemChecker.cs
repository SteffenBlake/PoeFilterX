using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using PoeFilterX.Engine.Compilation;
using PoeFilterX.Engine.Diagnostics;
using PoeFilterX.Engine.Projects;
using PoeFilterX.Engine.Results;
using PoeFilterX.Engine.Syntax;

namespace PoeFilterX.Engine.Checking;

public static class ItemChecker
{
    public static Result<CheckResult> Check(
        ProjectSnapshot snapshot,
        string copiedItem,
        GameVersion game,
        int? areaLevel = null,
        IReadOnlyDictionary<string, string>? configuration = null,
        IReadOnlyDictionary<string, string>? factOverrides = null,
        CancellationToken cancellationToken = default
    )
    {
        var level = areaLevel ?? DefaultAreaLevel(game);
        if (level is < 1 or > 100)
        {
            return new ParseError(DiagnosticCodes.InvalidItem, "Area level must be between 1 and 100.");
        }

        var parsed = CopiedItemParser.Parse(copiedItem, game, cancellationToken)
            .FlatMap(item => ItemFactOverrides.Apply(item, factOverrides));
        return parsed.FlatMap(item => ProjectCompiler.Prepare(snapshot, configuration)
            .FlatMap(ProjectCompiler.ResolveRules)
            .FlatMap(rules => Evaluate(snapshot, item, rules, level, cancellationToken)));
    }

    public static int DefaultAreaLevel(GameVersion game) => game == GameVersion.Poe1 ? 83 : 80;

    private static Result<CheckResult> Evaluate(
        ProjectSnapshot snapshot,
        ItemFacts item,
        ImmutableArray<CompiledRule> rules,
        int areaLevel,
        CancellationToken cancellationToken
    )
    {
        var blocks = ImmutableArray.CreateBuilder<CheckedBlock>();
        CompiledRule? matched = null;
        var resolvedFacts = new Dictionary<FilterFact, ImmutableArray<string>?>(2);
        for (var index = 0; index < rules.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rule = rules[index];
            var conditions = ImmutableArray<CheckedCondition>.Empty;
            if (matched is null)
            {
                var evaluator = new ConditionEvaluator(item, areaLevel, resolvedFacts);
                FilterOperationsCodec.Evaluate(rule.Block.Operations, evaluator);
                conditions = evaluator.Conditions;
            }

            var matches = !conditions.Any(condition => condition.Matches == false);
            if (matched is null && matches && conditions.Any(condition => condition.Matches is null))
            {
                var names = conditions.Where(condition => condition.Matches is null).Select(condition => condition.Name);
                return new ParseError(DiagnosticCodes.MissingItemFact,
                    $"The copied item does not supply {string.Join(", ", names)} needed by this block. "
                    + "Copy with Ctrl+Alt+C for advanced modifier information. For unavailable values, supply an explicit fact override.")
                    .At(rule.Block.Path, rule.Block.Span);
            }

            var status = matched is not null ? CheckStatus.Skipped : matches ? CheckStatus.Matched : CheckStatus.Unmatched;
            blocks.Add(new($"block-{index}", Location(snapshot, rule.Block.Path, rule.Block.Span),
                [.. rule.Block.Trace.Select(frame => Location(snapshot, frame.Path, frame.Span))], status, conditions));
            if (status == CheckStatus.Matched)
            {
                matched = rule;
            }
        }

        var style = matched?.Style ?? new StyleOperations();
        return new CheckResult(1, new(item.Game, item.Name, item.BaseType, item.ItemClass, item.Rarity), areaLevel, matched is null ? CheckStatus.Unmatched : CheckStatus.Matched,
            blocks.ToImmutable(), matched is null ? [] : TraceStyles(snapshot, matched), style, Preview(item, style))
        { DefaultPreview = Preview(item, new()), Frames = matched is null ? [] : TraceBlocks(snapshot, matched.Block), DataVersion = item.Game == GameVersion.Poe1 ? GameData.Poe1ItemModifierVersion : GameData.Poe2ItemModifierVersion };
    }

    private static ImmutableArray<CheckedFrame> TraceBlocks(ProjectSnapshot snapshot, PreparedBlock block)
    {
        var frames = ImmutableArray.CreateBuilder<CheckedFrame>();
        var winners = new Dictionary<string, string>(StringComparer.Ordinal);
        var scalarNames = FilterOperationsCodec.Operations.Where(operation => operation.Type == "bool?")
            .Select(operation => operation.Name).ToHashSet(StringComparer.Ordinal);
        var output = new StringBuilder();
        for (PreparedBlock? current = block; current is not null; current = current.Parent)
        {
            var id = $"frame-{frames.Count}";
            output.Clear();
            FilterOperationsCodec.Write(output, current.Local);
            var declarations = ImmutableArray.CreateBuilder<FrameDeclaration>();
            foreach (var line in output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var space = line.IndexOf(' ');
                var name = space < 0 ? line : line[..space];
                var value = space < 0 ? "" : line[(space + 1)..];
                declarations.Add(new(name, value, scalarNames.Contains(name) ? winners.GetValueOrDefault(name) : null));
                winners.TryAdd(name, id);
            }

            foreach (var name in current.Local.Style)
            {
                declarations.Add(new("Style", name, null));
            }

            frames.Add(new(id, Location(snapshot, current.Path, current.Span), declarations.ToImmutable()));
        }

        return frames.ToImmutable();
    }

    private static ImmutableArray<AppliedStyle> TraceStyles(ProjectSnapshot snapshot, CompiledRule rule)
    {
        var applied = new List<AppliedStyle>(rule.Declarations.Length);
        var previous = new StyleOperations();
        for (var index = 0; index < rule.Declarations.Length; index++)
        {
            var declaration = rule.Declarations[index];
            var patch = declaration.Operations;
            var collector = new StyleContributionCollector();
            StyleOperationsCodec.Inspect(StyleOperationsCodec.Apply(new(), patch), collector);
            if (patch.Alert is { UseBuiltIn: true } && previous.Alert is { CustomEnabled: not false, Path: not null })
            {
                collector.Add<bool>("Alert.Enabled", true);
            }

            var location = Location(snapshot, declaration.Path, declaration.Span);
            var values = collector.Values.Select(value => value with { Location = location }).ToImmutableArray();
            var extendsPrevious = index > 0 && declaration.Name == rule.Declarations[index - 1].Name
                && declaration.Path == rule.Declarations[index - 1].Path
                && declaration.StyleSpan == rule.Declarations[index - 1].StyleSpan;
            if (extendsPrevious)
            {
                var last = applied[^1];
                applied[^1] = last with
                {
                    Declaration = last.Declaration + "\n" + declaration.Declaration,
                    Values = last.Values.AddRange(values)
                };
            }
            else
            {
                applied.Add(new($"style-{index}", declaration.Name,
                    Location(snapshot, declaration.Path, declaration.StyleSpan), declaration.Declaration, values));
            }

            previous = StyleOperationsCodec.Apply(previous, patch);
        }

        var winners = new Dictionary<string, (string Id, string Value)>(StringComparer.Ordinal);
        for (var index = applied.Count - 1; index >= 0; index--)
        {
            var style = applied[index];
            var values = style.Values.ToBuilder();
            for (var valueIndex = values.Count - 1; valueIndex >= 0; valueIndex--)
            {
                var value = values[valueIndex];
                values[valueIndex] = value with { OverriddenBy = OverriddenBy(value.Property, winners) };
                winners.TryAdd(value.Property, (style.Id, value.Value));
            }

            applied[index] = style with { Values = values.ToImmutable() };
        }

        return [.. applied];
    }

    private static string? OverriddenBy(
        string property,
        Dictionary<string, (string Id, string Value)> winners
    )
    {
        var control = property switch
        {
            "Alert.Path" => "Alert.CustomEnabled",
            "Alert.Id" or "Alert.Volume" or "Alert.Positional" => "Alert.Enabled",
            "Icon.Size" or "Icon.Color" or "Icon.Shape" => "Icon.Enabled",
            "Pillar.Color" or "Pillar.Temporary" => "Pillar.Enabled",
            _ => null
        };

        if (control is not null && winners.TryGetValue(control, out var toggle) && toggle.Value == bool.FalseString)
        {
            return toggle.Id;
        }

        return winners.TryGetValue(property, out var replacement) ? replacement.Id : null;
    }

    private static CheckLocation Location(
        ProjectSnapshot snapshot,
        string path,
        SourceSpan span
    )
    {
        if (!snapshot.SyntaxTrees.TryGetValue(path, out var tree))
        {
            return new(path, 1, 1);
        }

        var position = tree.Source.GetPosition(Math.Clamp(span.Start, 0, tree.Source.Text.Length));
        return new(path, position.Line + 1, position.Character + 1);
    }

    private static ItemPreview Preview(ItemFacts item, StyleOperations style)
    {
        var defaultText = item.Rarity switch
        {
            FilterRarity.Magic => "#8888ffff",
            FilterRarity.Rare => "#ffff77ff",
            FilterRarity.Unique => "#af6025ff",
            _ => item.ItemClass.Contains("Currency", StringComparison.Ordinal) ? "#aa9e82ff"
                : item.ItemClass.Contains("Gem", StringComparison.Ordinal) ? "#1ba29bff"
                : item.ItemClass.Contains("Quest", StringComparison.Ordinal) ? "#4ae63aff" : "#c8c8c8ff"
        };

        var minimal = style.Show == FilterVisibility.Minimal;

        return new(item.Name, Color(style.FontColor, defaultText), minimal ? "#00000000" : Color(style.BackgroundColor, "#000000f0"),
            Color(style.BorderColor, "#00000000"), minimal ? 1 : style.FontSize ?? 32, style.Show == FilterVisibility.Hide, 1,
            style.Show != FilterVisibility.Hide && style.Icon is { Enabled: not false, Color: not null, Shape: not null, Size: not null } icon
                ? $"{icon.Color} {icon.Shape} ({icon.Size})" : null,
            style.Pillar is { Enabled: not false, Color: not null } pillar
                ? $"{pillar.Color} {(pillar.Temporary == true ? "temporary" : "permanent")}" : null);
    }

    private static string Color(ColorValue? value, string fallback)
    {
        return value is null ? fallback : string.Create(CultureInfo.InvariantCulture,
            $"#{value.Red ?? 0:x2}{value.Green ?? 0:x2}{value.Blue ?? 0:x2}{value.Alpha ?? 0:x2}");
    }
}
