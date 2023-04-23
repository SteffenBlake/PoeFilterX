using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using System.Text.RegularExpressions;

namespace PoeFilterX.Business.Services
{
    public class FilterMatcher {

        private ArgParser ArgParser { get; }
        private IDictionary<string, Func<string, string[], bool>> Library { get; }

        public FilterMatcher() {
            ArgParser = new ArgParser();
            Library = new Dictionary<string, Func<string, string[], bool>> 
            {
                // Actions
                {"PlayAlertSound", Success },
                {"PlayAlertSoundPositional", Success },
                {"CustomAlertSound", Success },
                {"CustomAlertSoundOptional", Success },
                {"EnableDropSound", Success },
                {"DisableDropSoundIfAlertSound", Success },
                {"EnableDropSoundIfAlertSound", Success },
                {"MinimapIcon", Success },
                {"PlayEffect", Success },
                {"SetBackgroundColor", Success },
                {"SetFontSize", Success },
                {"SetTextColor", Success },
                {"SetBorderColor", Success },

                // Conditions
                {"AlternateQuality", AlternateQuality },
                {"AnyEnchantment", AnyEnchantment },
                {"ArchnemesisMod", Failure },
                {"AreaLevel", Success },
                {"BaseArmour", Unsupported },
                {"BaseDefencePercentile", Unsupported },
                {"BaseEnergyShield", Unsupported },
                {"BaseEvasion", Unsupported },
                {"BaseType", BaseType },
                {"BaseWard", Unsupported },
                {"BlightedMap", Unsupported },
                {"Class", ItemClass },
                {"Corrupted", Corrupted },
                {"CorruptedMods", CorruptedMods },
                {"DropLevel", Success },
                {"ElderItem", ElderItem },
                {"ElderMap", Unsupported },
                {"EnchantmentPassiveNode", Unsupported },
                {"EnchantmentPassiveNum", Unsupported },
                {"FracturedItem", FracturedItem },
                {"GemLevel", GemLevel },
                {"GemQualityType", Unsupported },
                {"HasEaterOfWorldsImplicit", HasEaterOfWorldsImplicit },
                {"HasEnchantment", Unsupported },
                {"HasExplicitMod", Unsupported },
                {"HasInfluence", Unsupported },
                {"HasSearingExarchImplicit", HasSearingExarchImplicit },
                {"Height", Unsupported },
                {"Identified", Identified },
                {"ItemLevel", ItemLevel },
                {"LinkedSockets", Unsupported },
                {"MapTier", Unsupported },
                {"Mirrored", Mirrored },
                {"Quality", Quality },
                {"Rarity", Unsupported },
                {"Replica", Replica },
                {"Scourged", Unsupported },
                {"ShapedMap", Unsupported },
                {"ShaperItem", ShaperItem },
                {"SocketGroup", Unsupported },
                {"Sockets", Unsupported },
                {"StackSize", Failure },
                {"SynthesisedItem", SynthesisedItem },
                {"UberBlightedMap", Unsupported },
                {"Width", Unsupported },
            };
        }

        public bool IsMatch(string item, string line) 
        {
            var arguments = line.ToArgs();
            if (arguments.Length < 1) {
                return true;
            }

            var command = arguments[0];
            arguments = arguments.Skip(1).ToArray();

            if (!Library.ContainsKey(command)) {
                throw ParserException.UnrecognizedCommand(command);
            }

            return Library[command](item, arguments);
        }

        private static bool Success(string item, params string[] args) => true;
        private static bool Failure(string item, params string[] args) => false;
        private static bool Unsupported(string item, params string[] args) => false;

        private bool AlternateQuality(string item, params string[] args) 
        {
            return ItemClass(item, "Gems") && (
                item.Contains("Anomalous") ||
                item.Contains("Phantasmal") ||
                item.Contains("Divergent")
            );
        }

        private bool AnyEnchantment(string item, params string[] args)
        {
            var success = item.Contains("(enchant)");
            return BoolCondition(success, args);
        }

        private bool ItemClass(string item, params string[] args) => StringOperator("Item Class: (.+)", item, args);

        private bool SynthesisedItem(string item, string[] args)
        {
            var success = item.Contains("Synthesised ");
            return BoolCondition(success, args);
        }

        private bool Replica(string item, string[] args)
        {
            var success = item.Contains("Replica ");
            return BoolCondition(success, args);
        }

        private bool Quality(string item, string[] args)
        {
            return IntOperator(@"Quality: \+(\d+)%", item, args);
        }

        private bool Mirrored(string item, string[] args)
        {
            var success = item.Contains("Mirrored");
            return BoolCondition(success, args);
        }

        private bool ItemLevel(string item, string[] args)
        {
            return IntOperator(@"Item Level: (\d+)", item, args);
        }

        private bool Identified(string item, string[] args)
        {
            var success = !item.Contains("Unidentified");
            return BoolCondition(success, args);
        }

        private bool HasEaterOfWorldsImplicit(string item, string[] args)
        {
            var success = item.Contains("Eater of Worlds Item");
            return BoolCondition(success, args);
        }

        private bool HasSearingExarchImplicit(string item, string[] args)
        {
            var success = item.Contains("Searing Exarch Item");
            return BoolCondition(success, args);
        }

        private bool GemLevel(string item, string[] args)
        {
            return ItemClass(item, "Gems") && IntOperator(@"Level: (\d+)", item, args);
        }

        private bool FracturedItem(string item, string[] args)
        {
            var success = item.Contains("Fractured");
            return BoolCondition(success, args);
        }

        private bool ElderItem(string item, string[] args)
        {
            var success = item.Contains("Elder Item");
            return BoolCondition(success, args);
        }

        private bool ShaperItem(string item, string[] args)
        {
            var success = item.Contains("Shaper Item");
            return BoolCondition(success, args);
        }

        private bool CorruptedMods(string item, string[] args)
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
            {
                args = args.Skip(1).ToArray();
            }

            var regex = new Regex("Corruption Implicit Modifier");
            var corruptImplicitCount = regex.Matches(item).Count();
            return CompareInt(filterOperator, corruptImplicitCount, args);
        }

        private bool Corrupted(string item, string[] args)
        {
            var success = item.Contains("Corruption Implicit Modifier");
            return BoolCondition(success, args);
        }

        private bool BaseType(string item, string[] args)
        {
            var regex = @"([^\n\d]+)\r\n---";
            return StringOperator(regex, item, args);
        }

        private bool StringOperator(string regex, string item, params string[] args) {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator)) {
                args = args.Skip(1).ToArray();
            }

            var regexMatch = new Regex(regex);
            if (!regexMatch.IsMatch(item)) {
                return false;
            }

            var match = regexMatch.Match(item).Groups[1].Value;

            if (
                filterOperator == FilterOperator.NotEquals ||
                filterOperator == FilterOperator.GreaterThan ||
                filterOperator == FilterOperator.GreaterThanOrEqual ||
                filterOperator == FilterOperator.LessThan ||
                filterOperator == FilterOperator.LessThanOrEqual
            ) {
                throw new ParserException($"Unsupported operator for string match, '{filterOperator.GetDisplayName()}'");
            }

            foreach (var arg in args) {
                if (filterOperator == FilterOperator.ExactMatch && arg == match) {
                    return true;
                }
                if (filterOperator == FilterOperator.Equals && match.Contains(arg)) {
                    return true;
                }
            }
            return false;
        }

        private bool IntOperator(string regex, string item, params string[] args)
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
            {
                args = args.Skip(1).ToArray();
            }

            var regexMatch = new Regex(regex);
            if (!regexMatch.IsMatch(item))
            {
                return 
                    filterOperator == FilterOperator.LessThan ||
                    filterOperator == FilterOperator.LessThanOrEqual ||
                    filterOperator == FilterOperator.NotEquals;
            }

            var match = regexMatch.Match(item);
            if (!int.TryParse(match.Groups[1].Value, out var matchInt))
            {
                throw new Exception($"Unrecognized value '{match}', expected an integer");
            }

            return CompareInt(filterOperator, matchInt, args);
        }

        bool BoolCondition(bool success, params string[] args) 
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotBoolean(args[0], out var result);
            return success == result;
        }

        private bool CompareInt(FilterOperator filterOperator, int matchInt, string[] args)
        {
            if (args.Length > 1)
            {
                throw ParserException.UnexpectedArgCount(args.Length, 1);
            }

            ArgParser.ThrowIfIntOutOfRange(args[0], 0, null, out var intValue);

            return filterOperator switch
            {
                FilterOperator.Equals => matchInt == intValue,
                FilterOperator.ExactMatch => matchInt == intValue,
                FilterOperator.NotEquals => matchInt != intValue,
                FilterOperator.GreaterThan => matchInt > intValue,
                FilterOperator.GreaterThanOrEqual => matchInt >= intValue,
                FilterOperator.LessThan => matchInt < intValue,
                FilterOperator.LessThanOrEqual => matchInt <= intValue,
                _ => throw new ArgumentException(nameof(filterOperator))
            };
        }
    }
}