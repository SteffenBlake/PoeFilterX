using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;
using System.Linq.Expressions;

namespace PoeFilterX.Business.Services
{
    public class FilterCommandParser : IFilterCommandParser
    {
        private IVariableStore VariableStore { get; }
        private ArgParser ArgParser { get; }
        private IDictionary<string, Func<string[], Action<FilterBlock>?>> Commands { get; }
        public FilterCommandParser(IVariableStore variableStore, ArgParser argParser)
        {
            VariableStore = variableStore ?? throw new ArgumentNullException(nameof(variableStore));
            ArgParser = argParser ?? throw new ArgumentNullException(nameof(argParser));

            Commands = new Dictionary<string, Func<string[], Action<FilterBlock>?>>
            {
                { nameof(FilterBlock.HasValue), HasValue },
                { nameof(FilterBlock.IsTrue), IsTrue },

                { nameof(FilterBlock.AlternateQuality), (args) => SetBool(b => b.AlternateQuality, args) },
                { nameof(FilterBlock.AnyEnchantment), (args) => SetBool(b => b.AnyEnchantment, args) },
                { nameof(FilterBlock.AreaLevel), (args) => AddOperatorInt(b => b.AreaLevel, args) },
                { nameof(FilterBlock.BaseArmour), (args) => AddOperatorInt(b => b.BaseArmour, args) },
                { nameof(FilterBlock.BaseDefencePercentile), (args) => AddOperatorInt(b => b.BaseDefencePercentile, args) },
                { nameof(FilterBlock.BaseEnergyShield), (args) => AddOperatorInt(b => b.BaseEnergyShield, args) },
                { nameof(FilterBlock.BaseEvasion), (args) => AddOperatorInt(b => b.BaseEvasion, args) },
                { nameof(FilterBlock.BaseType), (args) => AddOperatorStrings(b => b.BaseType, args) },
                { nameof(FilterBlock.BaseWard), (args) => AddOperatorInt(b => b.BaseWard, args) },
                { nameof(FilterBlock.BlightedMap), (args) => SetBool(b => b.BlightedMap, args) },
                { nameof(FilterBlock.Class), (args) => AddOperatorStrings(b => b.Class, args) },
                { nameof(FilterBlock.Corrupted), (args) => SetBool(b => b.Corrupted, args) },
                { nameof(FilterBlock.CorruptedMods), (args) => AddOperatorInt(b => b.CorruptedMods, args) },
                { nameof(FilterBlock.DropLevel), (args) => AddOperatorInt(b => b.DropLevel, args) },
                { nameof(FilterBlock.ElderItem), (args) => SetBool(b => b.ElderItem, args) },
                { nameof(FilterBlock.ElderMap), (args) => SetBool(b => b.ElderMap, args) },
                { nameof(FilterBlock.EnchantmentPassiveNode), (args) => AddOperatorStrings(b => b.EnchantmentPassiveNode, args) },
                { nameof(FilterBlock.EnchantmentPassiveNum), (args) => AddOperatorInt(b => b.EnchantmentPassiveNum, args) },
                { nameof(FilterBlock.FracturedItem), (args) => SetBool(b => b.FracturedItem, args) },
                { nameof(FilterBlock.GemLevel), (args) => AddOperatorInt(b => b.GemLevel, args) },
                { nameof(FilterBlock.HasCruciblePassiveTree), (args) => SetBool(b => b.HasCruciblePassiveTree, args) },
                { nameof(FilterBlock.HasEaterOfWorldsImplicit), (args) => AddOperatorInt(b => b.HasEaterOfWorldsImplicit, args) },
                { nameof(FilterBlock.HasEnchantment), (args) => AddOperatorStrings(b => b.HasEnchantment, args) },
                { nameof(FilterBlock.HasExplicitMod), (args) => AddOperatorStrings(b => b.HasExplicitMod, args) },
                { nameof(FilterBlock.HasImplicitMod), (args) => SetBool(b => b.HasImplicitMod, args) },
                { nameof(FilterBlock.HasInfluence), (args) => AddEnums(b => b.HasInfluence, args) },
                { nameof(FilterBlock.HasSearingExarchImplicit), (args) => AddOperatorInt(b => b.HasSearingExarchImplicit, args) },
                { nameof(FilterBlock.Height), (args) => AddOperatorInt(b => b.Height, args) },
                { nameof(FilterBlock.Identified), (args) => SetBool(b => b.Identified, args) },
                { nameof(FilterBlock.ItemLevel), (args) => AddOperatorInt(b => b.ItemLevel, args) },
                { nameof(FilterBlock.LinkedSockets), (args) => AddOperatorInt(b => b.LinkedSockets, args, 0, 6) },
                { nameof(FilterBlock.MapTier), (args) => AddOperatorInt(b => b.MapTier, args) },
                { nameof(FilterBlock.WaystoneTier), (args) => AddOperatorInt(b => b.WaystoneTier, args) },
                { nameof(FilterBlock.Mirrored), (args) => SetBool(b => b.Mirrored, args) },
                { nameof(FilterBlock.Quality), (args) => AddOperatorInt(b => b.Quality, args) },
                { nameof(FilterBlock.Rarity), (args) => AddOperatorEnum(b => b.Rarity, args) },
                { nameof(FilterBlock.Replica), (args) => SetBool(b => b.Replica, args) },
                { nameof(FilterBlock.Scourged), (args) => SetBool(b => b.Scourged, args) },
                { nameof(FilterBlock.ShapedMap), (args) => SetBool(b => b.ShapedMap, args) },
                { nameof(FilterBlock.ShaperItem), (args) => SetBool(b => b.ShaperItem, args) },
                { nameof(FilterBlock.SocketGroup), (args) => AddOperatorStrings(b => b.SocketGroup, args) },
                { nameof(FilterBlock.Sockets), (args) => AddOperatorStrings(b => b.Sockets, args) },
                { nameof(FilterBlock.StackSize), (args) => AddOperatorInt(b => b.StackSize, args) },
                { nameof(FilterBlock.SynthesisedItem), (args) => SetBool(b => b.SynthesisedItem, args) },
                { nameof(FilterBlock.TransfiguredGem), (args) => SetBool(b => b.TransfiguredGem, args) },
                { nameof(FilterBlock.UberBlightedMap), (args) => SetBool(b => b.UberBlightedMap, args) },
                { nameof(FilterBlock.Width), (args) => AddOperatorInt(b => b.Width, args) },

                { nameof(FilterBlock.Style), (args) => AddStrings(b => b.Style, args)},
            };
        }
        public Action<FilterBlock>? Parse(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                return null;
            }

            var cmdName = args[0];

            if (!Commands.ContainsKey(cmdName))
            {
                throw new ParserException($"Unrecognized command '{args[0]}'");
            }

            // Inject configuration variables (env vars, .json files, etc) via %VARNAME% replace
            var composedArguments = VariableStore.InjectEnvironment(args.Skip(1).ToArray());

            return Commands[cmdName](composedArguments);
        }

        private Action<FilterBlock>? IsTrue(string[] args)
        {
            if (args.Length == 0)
            {
                return null;
            }

            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotBoolean(args[0], out var isTrue);

            return (b) => b.IsTrue.Add(isTrue);
        }

        private static Action<FilterBlock> HasValue(string[] args)
        {
            return (b) =>
            {
                if (args.Length == 0)
                {
                    b.HasValue.Add(" ");
                }

                foreach (var arg in args)
                {
                    b.HasValue.Add(arg);
                }
            };
        }


        private Action<FilterBlock> SetBool(Expression<Func<FilterBlock, bool?>> selector, IReadOnlyList<string> args)
        {
            if (args.Count > 1)
            {
                throw ParserException.UnexpectedArgCount(args.Count, 1);
            }

            ArgParser.ThrowIfNotBoolean(args[0], out var value);

            return (b) => b.SetPropertyValue(selector, value);
        }

        private static Action<FilterBlock>? AddStrings(Expression<Func<FilterBlock, IList<string>?>> selector, IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                return null;
            }

            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                {
                    b.SetPropertyValue(selector, new List<string>());
                }

                var items = method(b) ?? throw new Exception("SetPropertyValue failed");
                b.SetPropertyValue(selector, items.Concat(args).ToList());
            };
        }

        private Action<FilterBlock>? AddEnums<TEnum>(Expression<Func<FilterBlock, IEnumerable<TEnum>?>> selector, IReadOnlyList<string> args)
            where TEnum : struct, Enum
        {
            var method = selector.Compile();
            if (args.Count == 0)
            {
                return null;
            }

            var values = args.Select(a =>
            {
                ArgParser.ThrowIfNotEnum<TEnum>(a, out var value);
                return value;
            }).ToList();

            return (b) =>
            {
                if (method(b) == null)
                {
                    b.SetPropertyValue(selector, new List<TEnum>());
                }

                var items = method(b) ?? throw new Exception("SetPropertyValue failed");
                b.SetPropertyValue(selector, items.Concat(values).ToList());
            };
        }

        private Action<FilterBlock>? AddOperatorInt(Expression<Func<FilterBlock, IList<OperatorArg<int>>?>> selector, IReadOnlyList<string> args, int min = 0, int? max = null)
        {
            if (args.Count == 0)
            {
                return null;
            }

            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
            {
                args = args.Skip(1).ToArray();
            }

            if (args.Count == 0)
            {
                return null;
            }

            if (args.Count > 1)
            {
                throw ParserException.UnexpectedArgCount(args.Count, 1);
            }

            ArgParser.ThrowIfIntOutOfRange(args[0], min, max, out var value, selector.GetName());
            var addition = new OperatorArg<int>(value, filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock>? AddOperatorStrings(Expression<Func<FilterBlock, IList<OperatorArg<IList<string>>>?>> selector, IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                return null;
            }

            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
            {
                args = args.Skip(1).ToArray();
            }

            if (args.Count == 0)
            {
                return null;
            }

            var addition = new OperatorArg<IList<string>>(args.ToList(), filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock>? AddOperatorEnum<TEnum>(Expression<Func<FilterBlock, IList<OperatorArg<TEnum>>?>> selector, IReadOnlyList<string> args)
            where TEnum : struct, Enum
        {
            if (args.Count == 0)
            {
                return null;
            }

            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
            {
                args = args.Skip(1).ToArray();
            }

            if (args.Count == 0)
            {
                return null;
            }

            if (args.Count > 1)
            {
                throw ParserException.UnexpectedArgCount(args.Count, 1);
            }

            ArgParser.ThrowIfNotEnum<TEnum>(args[0], out var value);

            var addition = new OperatorArg<TEnum>(value, filterOperator);

            return AddOperator(selector, addition);
        }

        private static Action<FilterBlock> EnsureOperator<T>(Expression<Func<FilterBlock, IList<OperatorArg<T>>?>> selector)
        {
            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                {
                    b.SetPropertyValue(selector, new List<OperatorArg<T>>());
                }
            };
        }

        private static Action<FilterBlock> AddOperator<T>(Expression<Func<FilterBlock, IList<OperatorArg<T>>?>> selector, OperatorArg<T> addition)
        {
            var method = selector.Compile();

            return
                EnsureOperator(selector) +
                ((b) =>
                {
                    var operatorList = method(b);
                    operatorList?.Add(addition);
                    b.SetPropertyValue(selector, operatorList);
                });
        }
    }
}