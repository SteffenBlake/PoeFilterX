using PoeFilterX.Business.Enums;
using PoeFilterX.Business.Extensions;
using PoeFilterX.Business.Models;
using PoeFilterX.Business.Services.Abstractions;
using System.Drawing;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace PoeFilterX.Business.Services
{
    public class StyleCommandParser : IStyleCommandParser
    {
        private ArgParser ArgParser { get; } = new();
        private IDictionary<string, Func<string[], Action<FilterBlock>>> Commands { get; }
        public StyleCommandParser()
        {
            Commands = new Dictionary<string, Func<string[], Action<FilterBlock>>>()
            {
                { "show:",                  Show },
                { "border-color:",          BorderColor },
                { "border-color-alpha:",    BorderColorAlpha },
                { "border-color-red:",      BorderColorRed },
                { "border-color-green:",    BorderColorGreen },
                { "border-color-blue:",     BorderColorBlue },
                { "font-color:",            FontColor },
                { "font-color-alpha:",      FontColorAlpha },
                { "font-color-red:",        FontColorRed },
                { "font-color-green:",      FontColorGreen },
                { "font-color-blue:",       FontColorBlue },
                { "bg-color:",              BackgroundColor },
                { "bg-color-alpha:",        BackgroundColorAlpha },
                { "bg-color-red:",          BackgroundColorRed },
                { "bg-color-green:",        BackgroundColorGreen },
                { "bg-color-blue:",         BackgroundColorBlue },
                { "font-size:",             FontSize },
                { "alert:",                 Alert },
                { "alert-id:",              AlertId },
                { "alert-volume:",          AlertVolume },
                { "alert-style:",           AlertStyle },
                { "drop-sound:",            DropSound },
                { "alert-path:",            AlertPath },
                { "icon:",                  MiniMapIcon },
                { "icon-size:",             MiniMapIconSize },
                { "icon-color:",            MiniMapIconColor },
                { "icon-shape:",            MiniMapIconShape },
                { "pillar:",                PlayEffect },
                { "pillar-color:",          PlayEffectColor },
                { "pillar-duration:",       PlayEffectDuration },
            };
        }
        public Action<FilterBlock>? Parse(string runningArgs)
        {
            var args = runningArgs.Trim().ToArgs();
            if (args.Length == 0)
                return null;

            var cmdName = args[0].ToLower();

            if (!Commands.ContainsKey(cmdName))
                throw new ParserException($"Unrecognized style command '{args[0]}'");

            return Commands[cmdName](args.Skip(1).ToArray());
        }

        private Action<FilterBlock> Show(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotBoolean(args[0], out var show);

            return (b) => b.Show = show;
        }

        private Action<FilterBlock> BorderColor(string[] args) =>
            SetColor((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorAlpha(string[] args) => 
            SetColorAlpha((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorRed(string[] args) =>
            SetColorRed((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorGreen(string[] args) =>
            SetColorGreen((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorBlue (string[] args) =>
            SetColorBlue((b) => b.SetBorderColor, args);


        private Action<FilterBlock> FontColor(string[] args) =>
            SetColor((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorAlpha(string[] args) =>
            SetColorAlpha((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorRed(string[] args) =>
            SetColorRed((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorGreen(string[] args) =>
            SetColorGreen((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorBlue(string[] args) =>
            SetColorBlue((b) => b.SetTextColor, args);


        private Action<FilterBlock> BackgroundColor(string[] args) =>
            SetColor((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorAlpha(string[] args) =>
            SetColorAlpha((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorRed(string[] args) =>
            SetColorRed((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorGreen(string[] args) =>
            SetColorGreen((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorBlue(string[] args) =>
            SetColorBlue((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> FontSize(string[] args) =>
            SetInt((b) => b.SetFontSize, args, 18, 100);

        private Action<FilterBlock> Alert(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return SetSoundToggled((b) => b.AlertSound, toggle);
            }

            return SetSound((b) => b.AlertSound, args);
        }

        private Action<FilterBlock> AlertId(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureSound((b) => b.AlertSound) + SetSoundId((b) => b.AlertSound, args[0]);
        }

        private Action<FilterBlock> AlertVolume(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureSound((b) => b.AlertSound) + SetSoundVolume((b) => b.AlertSound, args[0]);
        }

        private Action<FilterBlock> AlertStyle(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotPositionalString(args[0], out var positional);
            return EnsureSound((b) => b.AlertSound) + SetSoundPositional((b) => b.AlertSound, positional);
        }

        private Action<FilterBlock> DropSound(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return (b) => b.DropSound = toggle;
            }

            throw ParserException.UnrecognizedCommand(args[0]);
        }
        private Action<FilterBlock> AlertPath(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return 
                    SetCustomSoundToggled(toggle) +
                    SetSoundToggled(b => b.AlertSound, !toggle);
            } 
            else
            {
                return
                    SetCustomSoundToggled(true) +
                    SetSoundToggled(b => b.AlertSound, false) +
                    SetCustomSound(args[0]);
            }
            throw ParserException.UnrecognizedCommand(args[0]);
        }

        private Action<FilterBlock> MiniMapIcon(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);

            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                if (args.Length > 1)
                    throw ParserException.UnrecognizedCommand(args[1]);

                return EnsureMiniMapIcon() + SetMiniMapIconToggle(toggle);
            }

            return SetMiniMapIcon(args);
        }

        private Action<FilterBlock> MiniMapIconSize(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconSize(args[0]);
        }

        private Action<FilterBlock> MiniMapIconColor(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconColor(args[0]);
        }

        private Action<FilterBlock> MiniMapIconShape(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconShape(args[0]);
        }

        private Action<FilterBlock> PlayEffect(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2);

            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                if (args.Length > 1)
                    throw ParserException.UnrecognizedCommand(args[1]);

                return EnsurePlayEffect() + SetPlayEffectToggle(toggle);
            }

            return SetPlayEffect(args);
        }

        private Action<FilterBlock> PlayEffectColor(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return SetPlayEffectColor(args[0]);
        }

        private Action<FilterBlock> PlayEffectDuration(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return SetPlayEffectDuration(args[0]);
        }

        private Action<FilterBlock> SetColor(Expression<Func<FilterBlock, Color?>> selector, string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3, 4);
            var command = EnsureColor(selector);

            // Single Hexcode arg
            var colorRaw = args[0].ToLower();
            if (args.Length == 1 && colorRaw.StartsWith("0x"))
            {
                if ((colorRaw.Length != 8 && colorRaw.Length != 10))
                    throw new ParserException("Unexpected color code, expected '0xAARRGGBB' or '0xRRGGBB");

                try
                {
                    var colorInt = (int?)new System.ComponentModel.Int32Converter().ConvertFromString(colorRaw);
                    if (colorRaw.Length == 8)
                    unchecked
                    {
                        colorInt += (int)0xFF000000;
                    }

                    if (colorInt.HasValue)
                    {
                        return SetColor(selector, Color.FromArgb(colorInt.Value));
                    }
                } 
                catch
                {
                    throw new ParserException("Unexpected color code, expected '0xAARRGGBB'");
                }

            }

            // 1 to 4 ARGB color arguments
            ArgParser.ThrowIfIntOutOfRange(args[0], 0, 255, out var red);

            var green = 0;
            if (args.Length >=2)
                ArgParser.ThrowIfIntOutOfRange(args[1], 0, 255, out green);

            var blue = 0;
            if (args.Length >=3)
                ArgParser.ThrowIfIntOutOfRange(args[2], 0, 255, out blue);

            var alpha = 255;
            if (args.Length >= 4)
                ArgParser.ThrowIfIntOutOfRange(args[3], 0, 255, out alpha);

            return SetColor(selector, Color.FromArgb(alpha, red, green, blue));
        }

        private Action<FilterBlock> SetColor(Expression<Func<FilterBlock, Color?>> selector, Color color)
        {
            return (b => b.SetPropertyValue(selector, color));
        }

        private Action<FilterBlock> EnsureColor(Expression<Func<FilterBlock, Color?>> selector)
        {
            var method = selector.Compile();
            return (block) =>
            {
                if (method(block) == null)
                    block.SetPropertyValue(selector, Color.FromArgb(0, 0, 0, 0));
            };
        }

        private Action<FilterBlock> SetColorAlpha(Expression<Func<FilterBlock, Color?>> selector, string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureColor(selector) + SetColorAlpha(selector, args[0]);
        }

        private Action<FilterBlock> SetColorAlpha(Expression<Func<FilterBlock, Color?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 255, out var alpha);
            var method = selector.Compile();

            return (block) =>
            {
                var baseColor = method(block) ?? throw new Exception("SetPropertyValue failed");

                block.SetPropertyValue(selector, Color.FromArgb(
                    alpha,
                    baseColor.R,
                    baseColor.G,
                    baseColor.B
                ));
            };
        }

        private Action<FilterBlock> SetColorRed(Expression<Func<FilterBlock, Color?>> selector, string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureColor(selector) + SetColorRed(selector, args[0]);
        }

        private Action<FilterBlock> SetColorRed(Expression<Func<FilterBlock, Color?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 255, out var red);
            var method = selector.Compile();

            return (block) =>
            {
                var baseColor = method(block) ?? throw new Exception("SetPropertyValue failed");

                block.SetPropertyValue(selector, Color.FromArgb(
                    baseColor.A,
                    red,
                    baseColor.G,
                    baseColor.B
                ));
            };
        }

        private Action<FilterBlock> SetColorGreen(Expression<Func<FilterBlock, Color?>> selector, string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureColor(selector) + SetColorGreen(selector, args[0]);
        }

        private Action<FilterBlock> SetColorGreen(Expression<Func<FilterBlock, Color?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 255, out var green);
            var method = selector.Compile();

            return (block) =>
            {
                var baseColor = method(block) ?? throw new Exception("SetPropertyValue failed");

                block.SetPropertyValue(selector, Color.FromArgb(
                    baseColor.A,
                    baseColor.R,
                    green,
                    baseColor.B
                ));
            };
        }

        private Action<FilterBlock> SetColorBlue(Expression<Func<FilterBlock, Color?>> selector, string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureColor(selector) + SetColorBlue(selector, args[0]);
        }

        private Action<FilterBlock> SetColorBlue(Expression<Func<FilterBlock, Color?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 255, out var blue);
            var method = selector.Compile();

            return (block) =>
            {
                var baseColor = method(block) ?? throw new Exception("SetPropertyValue failed");

                block.SetPropertyValue(selector, Color.FromArgb(
                    baseColor.A,
                    baseColor.R,
                    baseColor.G,
                    blue
                ));
            };
        }

        private Action<FilterBlock> SetInt(Expression<Func<FilterBlock, int?>> selector, string[] args, int min, int max)
        {
            ParseInt(args, min, max, out int value);
            return (block) => block.SetPropertyValue(selector, value);
        }

        private Action<FilterBlock> SetSound(Expression<Func<FilterBlock, AlertSound?>> selector, string[] args)
        {
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                if (args.Length > 1)
                    throw ParserException.UnrecognizedCommand(args[1]);

                return
                   SetSoundToggled(selector, toggle) +
                   SetCustomSoundToggled(!toggle);
            }

            var cmd = EnsureSound(selector) + SetSoundId(selector, args[0]);

            if (args.Length >= 2)
                cmd += SetSoundVolume(selector, args[1]);

            if (args.Length >= 3)
            {
                ArgParser.ThrowIfNotPositionalString(args[2], out var positional);
                cmd += SetSoundPositional(selector, positional);
            }

            return cmd;
        }

        private Action<FilterBlock> EnsureSound(Expression<Func<FilterBlock, AlertSound?>> selector) 
        {
            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                    b.SetPropertyValue(selector, new AlertSound());
            };
        }

        private Action<FilterBlock> SetSoundId(Expression<Func<FilterBlock, AlertSound?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 1, 16, out var id);

            var method = selector.Compile();

            return (block) =>
            {
                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Id = id;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private Action<FilterBlock> SetSoundVolume(Expression<Func<FilterBlock, AlertSound?>> selector, string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 300, out var volume);
            var method = selector.Compile();
            return (block) =>
            {
                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Volume = volume;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private Action<FilterBlock> SetSoundPositional(Expression<Func<FilterBlock, AlertSound?>> selector, bool positional)
        {
            var method = selector.Compile();
            return (block) =>
            {
                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Positional = positional;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private Action<FilterBlock> SetSoundToggled(Expression<Func<FilterBlock, AlertSound?>> selector, bool toggle)
        {
            var method = selector.Compile();
            return (block) =>
            {
                if (method(block) == null)
                    block.SetPropertyValue(selector, new AlertSound());

                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Enabled = toggle;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private Action<FilterBlock> SetCustomSound(string path)
        {
            return SetCustomSoundToggled(true) + ((b) => b.CustomAlertSound = path);
        }

        private Action<FilterBlock> SetCustomSoundToggled(bool toggle)
        {
            return (b) => b.CustomAlertSoundEnabled = toggle;
        }

        private void ParseInt(string[] args, int min, int max, out int result, [CallerArgumentExpression("result")] string? resultName = null)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);

            ArgParser.ThrowIfIntOutOfRange(args[0], min, max, out result, resultName);
        }

        private Action<FilterBlock> SetMiniMapIcon(string[] args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);

            var command = EnsureMiniMapIcon() + SetMiniMapIconSize(args[0]);

            if (args.Length >= 2)
                command += SetMiniMapIconColor(args[1]);

            if (args.Length >= 3)
                command += SetMiniMapIconShape(args[2]);


            return command;
        }

        private Action<FilterBlock> EnsureMiniMapIcon() => (b) => b.MinimapIcon ??= new MiniMapIcon();

        private Action<FilterBlock> SetMiniMapIconSize(string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 2, out var size);

            return ((b) => b.MinimapIcon.Size = size);
        }

        private Action<FilterBlock> SetMiniMapIconColor(string arg)
        {
            ArgParser.ThrowIfNotEnum<FilterColor>(arg, out var color);

            return ((b) => b.MinimapIcon.Color = color);
        }

        private Action<FilterBlock> SetMiniMapIconShape(string arg)
        {
            ArgParser.ThrowIfNotEnum<MiniMapIconShape>(arg, out var shape);

            return ((b) => b.MinimapIcon.Shape = shape);
        }

        private Action<FilterBlock> SetMiniMapIconToggle(bool toggle)
        {
            return ((b) => b.MinimapIcon.Enabled = toggle);
        }

        private Action<FilterBlock> SetPlayEffect(string[] args)
        {
            var cmd = EnsurePlayEffect() + SetPlayEffectColor(args[0]);

            if (args.Length >= 2)
                cmd += SetPlayEffectDuration(args[1]);

            return cmd;
        }

        private Action<FilterBlock> EnsurePlayEffect() => (b) => b.PlayEffect ??= new PlayEffect();

        private Action<FilterBlock> SetPlayEffectColor(string arg)
        {
            ArgParser.ThrowIfNotEnum<FilterColor>(arg, out var color);

            return ((b) => b.PlayEffect.Color = color);
        }

        private Action<FilterBlock> SetPlayEffectDuration(string arg)
        {
            var dur = arg.ToLower();
            if (dur == "permanent")
            {
                return ((b) => b.PlayEffect.Temporary = false);
            }
            else if (dur == "temporary")
            {
                return ((b) => b.PlayEffect.Temporary = true);
            }

            throw ParserException.UnrecognizedCommand(arg);
        }

        private Action<FilterBlock> SetPlayEffectToggle(bool toggle)
        {
            return ((b) => b.PlayEffect.Enabled = toggle);
        }

    }
}