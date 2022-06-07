using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace PoeFilterX.Business.Models
{
    public class FilterBlock
    {
        private readonly List<Action<FilterBlock>> _commands = new();
        public IReadOnlyList<Action<FilterBlock>> Commands => 
            Parent == null ? 
            _commands.AsReadOnly() : 
            Parent.Commands.Concat(_commands).ToList().AsReadOnly(); 

        private FilterBlock? Parent { get; }

        public FilterBlock(FilterBlock? parent = null)
        {
            Parent = parent;
        }

        public void AddCommand(Action<FilterBlock> command) => _commands.Add(command);

        public IList<OperatorArg<int>> AreaLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> ItemLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> DropLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> Quality { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<FilterRarity>> Rarity { get; set; } = new List<OperatorArg<FilterRarity>>();

        public IList<string> Class { get; set; } = new List<string>();

        public IList<string> BaseType { get; set; } = new List<string>();

        public IList<OperatorArg<int>> LinkedSockets { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<IList<string>>> SocketGroup { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<IList<string>>> Sockets { get; set; } = new List<OperatorArg<IList<string>>>();

        public IList<OperatorArg<int>> Height { get; set; } = new List<OperatorArg<int>>();

        public IList<OperatorArg<int>> Width { get; set; } = new List<OperatorArg<int>>();

        public IList<string> HasExplicitMod { get; set; } = new List<string>();

        public bool? AnyEnchantment { get; set; }

        public IList<string> HasEnchantment { get; set; } = new List<string>();

        public IList<string> EnchantmentPassiveNode { get; set; } = new List<string>();

        public IList<OperatorArg<int>> EnchantmentPassiveNum { get; set; } = new List<OperatorArg<int>>();
        public IList<OperatorArg<int>> StackSize { get; set; } = new List<OperatorArg<int>>();
        public IList<OperatorArg<int>> GemLevel { get; set; } = new List<OperatorArg<int>>();

        public IList<GemQualityType> GemQualityType { get; set; } = new List<GemQualityType>();

        public bool? AlternateQuality { get; set; }
        public bool? Replica { get; set; }
        public bool? Identified { get; set; }
        public bool? Corrupted { get; set; }

        public IList<OperatorArg<int>> CorruptedMods { get; set; } = new List<OperatorArg<int>>();

        public bool? Mirrored { get; set; }

        public bool? ElderItem { get; set; }

        public bool? ShaperItem { get; set; }

        public IList<InfluenceType> HasInfluence { get; set; } = new List<InfluenceType>();

        public bool? FracturedItem { get; set; }

        public bool? SynthesisedItem { get; set; }

        public bool? ElderMap { get; set; }

        public bool? ShapedMap { get; set; }

        public bool? BlightedMap { get; set; }

        public IList<OperatorArg<int>> MapTier { get; set; } = new List<OperatorArg<int>>();

        public bool Show { get; set; } = true;

        public Color? SetBorderColor { get; set; }
        public Color? SetTextColor { get; set; }
        public Color? SetBackgroundColor { get; set; }

        public int? SetFontSize { get; set; }

        public AlertSound? AlertSound { get; set; }

        public AlertSound? PlayAlertSound => 
            AlertSound is { Positional: false } ? AlertSound : null;
        public AlertSound? PlayAlertSoundPositional => 
            AlertSound is { Positional: true } ? AlertSound : null;

        public bool? DropSound { get; set; }

        public string? CustomAlertSound { get; set; }

        public bool CustomAlertSoundEnabled { get; set; } = true;

        public MiniMapIcon? MinimapIcon { get; set; }

        public PlayEffect? PlayEffect { get; set; }

        public IList<string>? Styles { get; set; }

        public IList<string> CompiledStyles =>
            Parent?.CompiledStyles.Concat(Styles ?? new List<string>()).ToList() ?? (Styles ?? new List<string>());

        public string Compile(IDictionary<string, IList<(int Rank, Action<FilterBlock> Command)>> stylesTable)
        {
            foreach (var command in Commands)
            {
                command(this);
            }

            var styles = CompiledStyles
                .Distinct()
                .SelectMany(s => stylesTable[s])
                .OrderBy(s => s.Rank)
                .ToList();

            if (!styles.Any())
                return "";

            foreach(var style in styles)
            {
                style.Command(this);
            }

            var builder = new StringBuilder();

            builder.AppendLine(Show ? "Show" : "Hide");

            Compile(builder, AreaLevel);
            Compile(builder, ItemLevel);
            Compile(builder, DropLevel);
            Compile(builder, Quality);
            Compile(builder, Rarity);
            Compile(builder, Class);
            Compile(builder, LinkedSockets);
            Compile(builder, SocketGroup);
            Compile(builder, Sockets);
            Compile(builder, Height);
            Compile(builder, Width);
            Compile(builder, HasExplicitMod);
            Compile(builder, AnyEnchantment);
            Compile(builder, HasEnchantment);
            Compile(builder, EnchantmentPassiveNode);
            Compile(builder, EnchantmentPassiveNum);
            Compile(builder, StackSize);
            Compile(builder, GemLevel);
            Compile(builder, GemQualityType);
            Compile(builder, AlternateQuality);
            Compile(builder, Replica);
            Compile(builder, Identified);
            Compile(builder, Corrupted);
            Compile(builder, CorruptedMods);
            Compile(builder, Mirrored);
            Compile(builder, ElderItem);
            Compile(builder, ShaperItem);
            Compile(builder, HasInfluence);
            Compile(builder, FracturedItem);
            Compile(builder, SynthesisedItem);
            Compile(builder, ElderMap);
            Compile(builder, ShapedMap);
            Compile(builder, BlightedMap);
            Compile(builder, MapTier);

            Compile(builder, SetBorderColor);
            Compile(builder, SetTextColor);
            Compile(builder, SetBackgroundColor);
            Compile(builder, SetFontSize);
            if (PlayAlertSound?.Enabled ?? false) 
                Compile(builder, PlayAlertSound);

            if (PlayAlertSoundPositional?.Enabled ?? false) 
                Compile(builder, PlayAlertSoundPositional);

            if (DropSound.HasValue)
                builder.AppendLine(DropSound.Value ? "\tEnableDropSound" : "\tDisableDropSound");

            Compile(builder, CustomAlertSound);

            if (MinimapIcon?.Enabled ?? false)
                Compile(builder, MinimapIcon);

            if (PlayEffect?.Enabled ?? false)
                Compile(builder, PlayEffect);

            return builder.ToString();
        }

        private static void Compile<T>(StringBuilder builder, T? value, [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (value != null)
                builder.AppendLine($"\t{valueName} {value}");
        }

        private static void Compile<T>(StringBuilder builder, IList<OperatorArg<T>> value, [CallerArgumentExpression("value")] string? valueName = null)
        {
            foreach (var operatorArg in value)
            {
                builder.AppendLine($"\t{valueName} {operatorArg.Operator.GetDisplayName()} {operatorArg.Value}");
            }
        }

        private static void Compile<T>(StringBuilder builder, IList<OperatorArg<IList<T>>> value, [CallerArgumentExpression("value")] string? valueName = null)
        {
            // Group multiple operator commands together if they have the same operator
            foreach (var group in value.GroupBy(o => o.Operator))
            {
                var operater = group.Key.GetDisplayName();
                var values = group.SelectMany(g => g.Value);
                var encapsulted = values.Select(v => $"\"{v}\"");
                builder.AppendLine($"\t{valueName} {operater} {string.Join(' ', encapsulted)}");
            }
        }

        private static void Compile<T>(StringBuilder builder, IList<T> value, [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (!value.Any()) 
                return;

            var encapsulted = value.Distinct().Select(v => $"\"{v}\"");
            builder.AppendLine($"\t{valueName} {string.Join(' ', encapsulted)}");
        }

        private static void Compile(StringBuilder builder, Color? value, [CallerArgumentExpression("value")] string? valueName = null)
        {
            if (value != null)
                builder.AppendLine($"\t{valueName} {value.Value.R} {value.Value.G} {value.Value.B} {value.Value.A}");
        }
    }
}