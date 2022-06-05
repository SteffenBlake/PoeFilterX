using Microsoft.Extensions.Configuration;
using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;
using System.Linq.Expressions;

namespace PoeFilterX.Business.Services
{
    public class FilterCommandParser : IFilterCommandParser
    {
        private IConfiguration Config { get; }
        private ArgParser ArgParser { get; }
        private IDictionary<string, Func<string[], Action<FilterBlock>>> Commands { get; }
        public FilterCommandParser(IConfiguration config, ArgParser argParser)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            ArgParser = argParser ?? throw new ArgumentNullException(nameof(argParser));

            Commands = new Dictionary<string, Func<string[], Action<FilterBlock>>>
            {
                { "AreaLevel", (args) => AddOperatorInt(b => b.AreaLevel, args, 0, 100) }, 
                { "ItemLevel", (args) => AddOperatorInt(b => b.ItemLevel, args, 0, 100) }, 
                { "DropLevel", (args) => AddOperatorInt(b => b.DropLevel, args, 0, 100) }, 
                { "Quality", (args) => AddOperatorInt(b => b.Quality, args, 0, 100) }, 
                { "Rarity", (args) => AddOperatorEnum(b => b.Rarity, args) }, 
                { "Class", (args) => AddStrings(b => b.Class, args) }, 
                { "BaseType", (args) => AddStrings(b => b.BaseType, args) }, 
                { "LinkedSockets", (args) => AddOperatorInt(b => b.LinkedSockets, args, 1, 6) }, 
                { "SocketGroup", (args) => AddOperatorStrings(b => b.SocketGroup, args) }, 
                { "Sockets", (args) => AddOperatorStrings(b => b.Sockets, args) }, 
                { "Height", (args) => AddOperatorInt(b => b.Height, args, 0, 100) }, 
                { "Width", (args) => AddOperatorInt(b => b.Width, args, 0, 100) }, 
                { "HasExplicitMod", (args) => AddStrings(b => b.HasExplicitMod, args) }, 
                { "AnyEnchantment", (args) => SetBool(b => b.AnyEnchantment, args) }, 
                { "EnchantmentPassiveNode", (args) => AddStrings(b => b.EnchantmentPassiveNode, args) },
                { "EnchantmentPassiveNum", (args) => AddOperatorInt(b => b.EnchantmentPassiveNum, args, 0, 100) },
                { "StackSize", (args) => AddOperatorInt(b => b.StackSize, args, 0, 100) },
                { "GemLevel", (args) => AddOperatorInt(b => b.GemLevel, args, 0, 100) },
                { "GemQualityType", (args) => AddEnums(b => b.GemQualityType, args) },
                { "AlternateQuality", (args) => SetBool(b => b.AlternateQuality, args) },
                { "Replica", (args) => SetBool(b => b.Replica, args) },
                { "Identified", (args) => SetBool(b => b.Identified, args) },
                { "Corrupted", (args) => SetBool(b => b.Corrupted, args) },
                { "CorruptedMods", (args) => AddOperatorInt(b => b.CorruptedMods, args, 0, 100) },
                { "Mirrored", (args) => SetBool(b => b.Mirrored, args) },
                { "ElderItem", (args) => SetBool(b => b.ElderItem, args) },
                { "ShaperItem", (args) => SetBool(b => b.ShaperItem, args) },
                { "HasInfluence", (args) => AddEnums(b => b.HasInfluence, args) },
                { "FracturedItem", (args) => SetBool(b => b.FracturedItem, args) },
                { "SynthesisedItem", (args) => SetBool(b => b.SynthesisedItem, args) },
                { "ElderMap", (args) => SetBool(b => b.ElderMap, args) },
                { "ShapedMap", (args) => SetBool(b => b.ShapedMap, args) },
                { "BlightedMap", (args) => SetBool(b => b.BlightedMap, args) },
                { "HasEnchantment", (args) => AddStrings(b => b.HasEnchantment, args) },
                { "MapTier", (args) => AddOperatorInt(b => b.MapTier, args, 0, 100) },
                { "Style", (args) => AddStrings(b => b.Styles, args)},
            };
        }


        public Action<FilterBlock>? Parse(string[] args)
        {
            if (args.Length == 0)
                return null;

            var cmdName = args[0];

            if (!Commands.ContainsKey(cmdName))
                throw new ParserException($"Unrecognized command '{args[0]}'");

            // Inject configuration variables (env vars, etc) via %VARNAME% replace
            var composedArguments = Config.InjectEnvironment(args.Skip(1).ToArray());

            return Commands[cmdName](composedArguments);
        }

        private Action<FilterBlock> SetBool(Expression<Func<FilterBlock, bool?>> selector, string[] args)
        {
            if (args.Length > 1)
                throw ParserException.UnexpectedArgCount(args.Length, 1);

            ArgParser.ThrowIfNotBoolean(args[0], out var value);

            return (b) => b.SetPropertyValue(selector, value);
        }

        private Action<FilterBlock> AddStrings(Expression<Func<FilterBlock, IList<string>?>> selector, string[] args)
        {
            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                    b.SetPropertyValue(selector, new List<string>());

                var items = method(b) ?? throw new Exception("SetPropertyValue failed");
                b.SetPropertyValue(selector, items.Concat(args).ToList());
            };
        }

        private Action<FilterBlock> AddEnums<TEnum>(Expression<Func<FilterBlock, IList<TEnum>?>> selector, string[] args)
            where TEnum : struct, Enum
        {
            var method = selector.Compile();
            var values = args.Select(a =>
            {
                ArgParser.ThrowIfNotEnum<TEnum>(a, out var value);
                return value;
            }).ToList();

            return (b) =>
            {
                if (method(b) == null)
                    b.SetPropertyValue(selector, new List<TEnum>());

                var items = method(b) ?? throw new Exception("SetPropertyValue failed");
                b.SetPropertyValue(selector, items.Concat(values).ToList());
            };
        }

        private Action<FilterBlock> AddOperatorInt(Expression<Func<FilterBlock, IList<OperatorArg<int>>>> selector, string[] args, int min, int max)
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
                args = args.Skip(1).ToArray();

            if (args.Length > 1)
                throw ParserException.UnexpectedArgCount(args.Length, 1);


            ArgParser.ThrowIfIntOutOfRange(args[0], min, max, out var value, selector.GetName());
            var addition = new OperatorArg<int>(value, filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock> AddOperatorStrings(Expression<Func<FilterBlock, IList<OperatorArg<IList<string>>>>> selector, string[] args)
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
                args = args.Skip(1).ToArray();

            var addition = new OperatorArg<IList<string>>(args.ToList(), filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock> AddOperatorEnum<TEnum>(Expression<Func<FilterBlock, IList<OperatorArg<TEnum>>>> selector, string[] args)
            where TEnum : struct, Enum
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
                args = args.Skip(1).ToArray();

            if (args.Length > 1)
                throw ParserException.UnexpectedArgCount(args.Length, 1);

            ArgParser.ThrowIfNotEnum<TEnum>(args[0], out var value);

            var addition = new OperatorArg<TEnum>(value, filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock> AddOperatorEnums<TEnum>(Expression<Func<FilterBlock, IList<OperatorArg<IList<TEnum>>>>> selector, string[] args)
            where TEnum : struct, Enum
        {
            if (ArgParser.TryParseOperator(args[0], out var filterOperator))
                args = args.Skip(1).ToArray();

            var values = args.Select(a =>
            {
                ArgParser.ThrowIfNotEnum<TEnum>(a, out var value);
                return value;
            }).ToList();

            var addition = new OperatorArg<IList<TEnum>>(values, filterOperator);

            return AddOperator(selector, addition);
        }

        private Action<FilterBlock> EnsureOperator<T>(Expression<Func<FilterBlock, IList<OperatorArg<T>>>> selector)
        {
            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                    b.SetPropertyValue(selector, new List<OperatorArg<T>>());
            };
        }

        private Action<FilterBlock> AddOperator<T>(Expression<Func<FilterBlock, IList<OperatorArg<T>>>> selector, OperatorArg<T> addition)
        {
            var method = selector.Compile();

            return 
                EnsureOperator(selector) + 
                ((b) =>
                {
                    var operatorList = method(b);
                    operatorList.Add(addition);
                    // TODO: Check if this line here is even needed...
                    // Maybe safe to leave it as a safeguard in case 
                    // of weird pointer stuff?
                    b.SetPropertyValue(selector, operatorList);
                });
        }

    }

}