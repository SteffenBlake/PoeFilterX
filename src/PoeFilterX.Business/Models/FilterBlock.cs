using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace PoeFilterX.Business.Models
{
    public class FilterBlock
    {
        // TODO: "bubble up" line for each command
        // So we can still log what line caused the failure

        private readonly List<Action<FilterBlock>> _commands = new();

        public FilterBlock(FilterBlock? parent = null, bool abstracted = false)
        {
            Parent = parent;
            Abstracted = abstracted;
        }

        #region "Composition"

        private FilterBlock? Parent { get; }
        private bool Abstracted { get; }

        public IReadOnlyList<Action<FilterBlock>> Commands =>
            Parent == null ? _commands.AsReadOnly() : Parent.Commands.Concat(_commands).ToList().AsReadOnly();

        public IList<string> CompiledStyles =>
            Parent?.CompiledStyles.Concat(Style ?? new List<string>()).ToList() ?? Style ?? new List<string>();

        public IList<string>? Style { get; set; }

        #endregion
        #region "Advanced Conditions"

        public IList<string?> HasValue { get; set; } = new List<string?>();

        #endregion

        #region "Conditions"

        public AlertSound? AlertSound { get; set; }

        public bool? AlternateQuality { get; set; }

        public bool? AnyEnchantment { get; set; }

        public IList<OperatorArg<int>> AreaLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> BaseArmour { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> BaseDefencePercentile { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> BaseEnergyShield { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> BaseEvasion { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<IList<string>>> BaseType { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<int>> BaseWard { get; set; } = new List<OperatorArg<int>>();

        public bool? BlightedMap { get; set; }

        public IList<OperatorArg<IList<string>>> Class { get; set; } = new List<OperatorArg<IList<string>>>();

        public bool? Corrupted { get; set; }

        public IList<OperatorArg<int>> CorruptedMods { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> DropLevel { get; set; } = new List<OperatorArg<int>>();

        public bool? ElderItem { get; set; }

        public bool? ElderMap { get; set; }

        public IList<OperatorArg<IList<string>>> EnchantmentPassiveNode { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<int>> EnchantmentPassiveNum { get; set; } = new List<OperatorArg<int>>();

        public bool? Foulborn { get; set; }

        public bool? FracturedItem { get; set; }

        public IList<OperatorArg<int>> GemLevel { get; set; } = new List<OperatorArg<int>>();

        public bool? HasCruciblePassiveTree { get; set; }

        public IList<OperatorArg<int>> HasEaterOfWorldsImplicit { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<IList<string>>> HasEnchantment { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<IList<string>>> HasExplicitMod { get; set; } = new List<OperatorArg<IList<string>>>();

        public bool? HasImplicitMod { get; set; }

        public IList<InfluenceType> HasInfluence { get; set; } = new List<InfluenceType>();

        public IList<OperatorArg<int>> HasSearingExarchImplicit { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> Height { get; set; } = new List<OperatorArg<int>>();

        public bool? Identified { get; set; }

        public IList<OperatorArg<int>> ItemLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> LinkedSockets { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> MapTier { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> MemoryStrands { get; set; } = new List<OperatorArg<int>>();

        public bool? Mirrored { get; set; }

        public IList<OperatorArg<int>> Quality { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<FilterRarity>> Rarity { get; set; } = new List<OperatorArg<FilterRarity>>();

        public bool? Replica { get; set; }

        public bool? Scourged { get; set; }

        public bool? ShapedMap { get; set; }

        public bool? ShaperItem { get; set; }

        public IList<OperatorArg<IList<string>>> SocketGroup { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<IList<string>>> Sockets { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<int>> StackSize { get; set; } = new List<OperatorArg<int>>();

        public bool? SynthesisedItem { get; set; }

        public bool? TransfiguredGem { get; set; }

        public bool? UberBlightedMap { get; set; }

        public IList<OperatorArg<int>> UnidentifiedItemTier { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> WaystoneTier { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> Width { get; set; } = new List<OperatorArg<int>>();

        public bool? ZanaMemory { get; set; }

        #endregion

        #region "Actions"

        public bool Show { get; set; } = true;

        public AlertSound? PlayAlertSound =>
            AlertSound is { Positional: false } ? AlertSound : null;

        public AlertSound? PlayAlertSoundPositional =>
            AlertSound is { Positional: true } ? AlertSound : null;

        public string? CustomAlertSound { get; set; }

        public bool CustomAlertSoundEnabled { get; set; } = true;

        public bool? DropSound { get; set; }

        public PlayEffect? PlayEffect { get; set; }

        public MiniMapIcon? MinimapIcon { get; set; }

        public Color? SetBackgroundColor { get; set; }

        public Color? SetBorderColor { get; set; }

        public int? SetFontSize { get; set; }

        public Color? SetTextColor { get; set; }

        public IList<bool> IsTrue { get; set; } = new List<bool>();

        #endregion

        public void AddCommand(Action<FilterBlock> command)
        {
            _commands.Add(command);
        }

        public string Compile(IDictionary<string, IList<(int Rank, Action<FilterBlock> Command)>> stylesTable)
        {
            foreach (var command in Commands)
            {
                command(this);
            }

            if (HasValue.Any())
            {
                var compiledHasValue = string.Join(' ', HasValue).Trim();
                if (string.IsNullOrEmpty(compiledHasValue))
                {
                    return "";
                }
            }

            if (IsTrue.Any(b => !b))
            {
                return "";
            }

            var styles = CompiledStyles.Distinct();

            var styleCmds = styles
                .Select(st =>
                {
                    if (!stylesTable.ContainsKey(st))
                    {
                        throw new ParserException($"Filter block referenced undeclared style '{st}'");
                    }

                    return stylesTable[st].OrderBy(s => s.Rank).ToList();
                })
                .ToList();

            if (!styleCmds.Any())
            {
                return "";
            }

            foreach (var style in styleCmds)
            {
                foreach (var (_, command) in style)
                {
                    command(this);
                }
            }

            var builder = new StringBuilder();

            Compile(builder, AlternateQuality);
            Compile(builder, AnyEnchantment);
            Compile(builder, AreaLevel);
            Compile(builder, BaseArmour);
            Compile(builder, BaseDefencePercentile);
            Compile(builder, BaseEnergyShield);
            Compile(builder, BaseEvasion);
            Compile(builder, BaseType);
            Compile(builder, BaseWard);
            Compile(builder, BlightedMap);
            Compile(builder, Class);
            Compile(builder, Corrupted);
            Compile(builder, CorruptedMods);
            Compile(builder, DropLevel);
            Compile(builder, ElderItem);
            Compile(builder, ElderMap);
            Compile(builder, EnchantmentPassiveNode);
            Compile(builder, EnchantmentPassiveNum);
            Compile(builder, Foulborn);
            Compile(builder, FracturedItem);
            Compile(builder, GemLevel);
            Compile(builder, HasCruciblePassiveTree);
            Compile(builder, HasEaterOfWorldsImplicit);
            Compile(builder, HasEnchantment);
            Compile(builder, HasExplicitMod);
            Compile(builder, HasImplicitMod);
            Compile(builder, HasInfluence);
            Compile(builder, HasSearingExarchImplicit);
            Compile(builder, Height);
            Compile(builder, Identified);
            Compile(builder, ItemLevel);
            Compile(builder, LinkedSockets);
            Compile(builder, MapTier);
            Compile(builder, MemoryStrands);
            Compile(builder, Mirrored);
            Compile(builder, Quality);
            Compile(builder, Rarity);
            Compile(builder, Replica);
            Compile(builder, Scourged);
            Compile(builder, ShapedMap);
            Compile(builder, ShaperItem);
            Compile(builder, SocketGroup);
            Compile(builder, Sockets);
            Compile(builder, StackSize);
            Compile(builder, SynthesisedItem);
            Compile(builder, TransfiguredGem);
            Compile(builder, UberBlightedMap);
            Compile(builder, UnidentifiedItemTier);
            Compile(builder, WaystoneTier);
            Compile(builder, Width);
            Compile(builder, ZanaMemory);

            if (Parent != null && string.IsNullOrEmpty(builder.ToString().Trim()))
            {
                return "";
            }

            Compile(builder, SetBorderColor);
            Compile(builder, SetTextColor);
            Compile(builder, SetBackgroundColor);
            Compile(builder, SetFontSize);

            if (PlayAlertSound?.Id != null && (PlayAlertSound?.Enabled ?? false))
            {
                Compile(builder, PlayAlertSound);
            }

            if (PlayAlertSoundPositional?.Id != null && (PlayAlertSoundPositional?.Enabled ?? false))
            {
                Compile(builder, PlayAlertSoundPositional);
            }

            if (DropSound.HasValue)
            {
                _ = builder.AppendLine(DropSound.Value ? "\tEnableDropSound" : "\tDisableDropSound");
            }

            Compile(builder, CustomAlertSound);

            if ((MinimapIcon?.HasValue ?? false) && (MinimapIcon?.Enabled ?? false))
            {
                Compile(builder, MinimapIcon);
            }

            if (PlayEffect?.Color != null && (PlayEffect?.Enabled ?? false))
            {
                Compile(builder, PlayEffect);
            }

            return builder.ToString();
        }

        private static void Compile<T>(StringBuilder builder, T? value,
            [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (value != null)
            {
                _ = builder.AppendLine($"\t{valueName} {value}");
            }
        }

        private static void Compile<T>(StringBuilder builder, IList<OperatorArg<T>> value,
            [CallerArgumentExpression("value")] string? valueName = null)
        {
            foreach (var operatorArg in value)
            {
                _ = builder.AppendLine($"\t{valueName} {operatorArg.Operator.GetDisplayName()} {operatorArg.Value}");
            }
        }

        private static void Compile<T>(StringBuilder builder, IList<OperatorArg<IList<T>>> value,
            [CallerArgumentExpression("value")] string? valueName = null)
        {
            // Group multiple operator commands together if they have the same operator
            foreach (var group in value.GroupBy(o => o.Operator))
            {
                var operater = group.Key.GetDisplayName();
                var values = group.SelectMany(g => g.Value);
                var encapsulted = values.Select(v => $"\"{v}\"");
                _ = builder.AppendLine($"\t{valueName} {operater} {string.Join(' ', encapsulted)}");
            }
        }

        private static void Compile<T>(StringBuilder builder, IList<T> value,
            [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (!value.Any())
            {
                return;
            }

            var encapsulted = value.Distinct().Select(v => $"\"{v}\"");
            _ = builder.AppendLine($"\t{valueName} {string.Join(' ', encapsulted)}");
        }

        private static void Compile(StringBuilder builder, Color? value,
            [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (value != null)
            {
                _ = builder.AppendLine(
                    $"\t{valueName} {value.Value.R} {value.Value.G} {value.Value.B} {value.Value.A}");
            }
        }
    }
}