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
            Commands = new Dictionary<string, Func<string[], Action<FilterBlock>>>
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
            {
                return null;
            }

            var cmdName = args[0].ToLower();

            if (!Commands.ContainsKey(cmdName))
            {
                throw new ParserException($"Unrecognized style command '{args[0]}'");
            }

            return Commands[cmdName](args.Skip(1).ToArray());
        }

        private Action<FilterBlock> Show(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotBoolean(args[0], out var show);

            return (b) => b.Show = show;
        }

        private Action<FilterBlock> BorderColor(IReadOnlyList<string> args) =>
            SetColor((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorAlpha(IReadOnlyList<string> args) => 
            SetColorAlpha((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorRed(IReadOnlyList<string> args) =>
            SetColorRed((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorGreen(IReadOnlyList<string> args) =>
            SetColorGreen((b) => b.SetBorderColor, args);

        private Action<FilterBlock> BorderColorBlue (IReadOnlyList<string> args) =>
            SetColorBlue((b) => b.SetBorderColor, args);


        private Action<FilterBlock> FontColor(IReadOnlyList<string> args) =>
            SetColor((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorAlpha(IReadOnlyList<string> args) =>
            SetColorAlpha((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorRed(IReadOnlyList<string> args) =>
            SetColorRed((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorGreen(IReadOnlyList<string> args) =>
            SetColorGreen((b) => b.SetTextColor, args);

        private Action<FilterBlock> FontColorBlue(IReadOnlyList<string> args) =>
            SetColorBlue((b) => b.SetTextColor, args);


        private Action<FilterBlock> BackgroundColor(IReadOnlyList<string> args) =>
            SetColor((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorAlpha(IReadOnlyList<string> args) =>
            SetColorAlpha((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorRed(IReadOnlyList<string> args) =>
            SetColorRed((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorGreen(IReadOnlyList<string> args) =>
            SetColorGreen((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> BackgroundColorBlue(IReadOnlyList<string> args) =>
            SetColorBlue((b) => b.SetBackgroundColor, args);

        private Action<FilterBlock> FontSize(IReadOnlyList<string> args) =>
            SetInt((b) => b.SetFontSize, args, 18, 100);

        private Action<FilterBlock> Alert(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);
            return ArgParser.TryParseToggleString(args[0], out var toggle) ? 
                SetSoundToggled((b) => b.AlertSound, toggle) : 
                SetSound((b) => b.AlertSound, args);
        }

        private Action<FilterBlock> AlertId(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureSound((b) => b.AlertSound) + SetSoundId((b) => b.AlertSound, args[0]);
        }

        private Action<FilterBlock> AlertVolume(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureSound((b) => b.AlertSound) + SetSoundVolume((b) => b.AlertSound, args[0]);
        }

        private Action<FilterBlock> AlertStyle(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            ArgParser.ThrowIfNotPositionalString(args[0], out var positional);
            return EnsureSound((b) => b.AlertSound) + SetSoundPositional((b) => b.AlertSound, positional);
        }

        private Action<FilterBlock> DropSound(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return (b) => b.DropSound = toggle;
            }

            throw ParserException.UnrecognizedCommand(args[0]);
        }
        private Action<FilterBlock> AlertPath(IReadOnlyList<string> args)
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
        }

        private Action<FilterBlock> MiniMapIcon(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);

            if (!ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return SetMiniMapIcon(args);
            }

            if (args.Count > 1)
            {
                throw ParserException.UnrecognizedCommand(args[1]);
            }

            return EnsureMiniMapIcon() + SetMiniMapIconToggle(toggle);

        }

        private Action<FilterBlock> MiniMapIconSize(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconSize(args[0]);
        }

        private Action<FilterBlock> MiniMapIconColor(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconColor(args[0]);
        }

        private Action<FilterBlock> MiniMapIconShape(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return EnsureMiniMapIcon() + SetMiniMapIconShape(args[0]);
        }

        private Action<FilterBlock> PlayEffect(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2);

            if (!ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                return SetPlayEffect(args);
            }

            if (args.Count > 1)
            {
                throw ParserException.UnrecognizedCommand(args[1]);
            }

            return EnsurePlayEffect() + SetPlayEffectToggle(toggle);

        }

        private Action<FilterBlock> PlayEffectColor(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return SetPlayEffectColor(args[0]);
        }

        private Action<FilterBlock> PlayEffectDuration(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);
            return SetPlayEffectDuration(args[0]);
        }

        private Action<FilterBlock> SetColor(Expression<Func<FilterBlock, Color?>> selector, IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3, 4);
            var command = EnsureColor(selector);

            // Single Hexcode arg
            var colorRaw = args[0].ToLower();
            if (args.Count == 1 && colorRaw.StartsWith("0x"))
            {
                if (colorRaw.Length is not 8 and not 10)
                {
                    throw new ParserException("Unexpected color code, expected '0xAARRGGBB' or '0xRRGGBB");
                }

                try
                {
                    var colorInt = (int?)new System.ComponentModel.Int32Converter().ConvertFromString(colorRaw);
                    if (colorRaw.Length == 8)
                    {
                        unchecked
                        {
                            colorInt += (int)0xFF000000;
                        }
                    }

                    if (colorInt.HasValue)
                    {
                        command += SetColor(selector, Color.FromArgb(colorInt.Value));
                    }
                } 
                catch
                {
                    throw new ParserException("Unexpected color code, expected '0xAARRGGBB'");
                }
            }
            else
            {
                // 1 to 4 ARGB color arguments
                ArgParser.ThrowIfIntOutOfRange(args[0], 0, 255, out var red);

                var green = 0;
                if (args.Count >= 2)
                {
                    ArgParser.ThrowIfIntOutOfRange(args[1], 0, 255, out green);
                }

                var blue = 0;
                if (args.Count >= 3)
                {
                    ArgParser.ThrowIfIntOutOfRange(args[2], 0, 255, out blue);
                }

                var alpha = 255;
                if (args.Count >= 4)
                {
                    ArgParser.ThrowIfIntOutOfRange(args[3], 0, 255, out alpha);
                }

                command += SetColor(selector, Color.FromArgb(alpha, red, green, blue));
            }

            return command;
        }

        private static Action<FilterBlock> SetColor(Expression<Func<FilterBlock, Color?>> selector, Color color)
        {
            return b => b.SetPropertyValue(selector, color);
        }

        private static Action<FilterBlock> EnsureColor(Expression<Func<FilterBlock, Color?>> selector)
        {
            var method = selector.Compile();
            return (block) =>
            {
                if (method(block) == null)
                {
                    block.SetPropertyValue(selector, Color.FromArgb(0, 0, 0, 0));
                }
            };
        }

        private Action<FilterBlock> SetColorAlpha(Expression<Func<FilterBlock, Color?>> selector, IReadOnlyList<string> args)
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

        private Action<FilterBlock> SetColorRed(Expression<Func<FilterBlock, Color?>> selector, IReadOnlyList<string> args)
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

        private Action<FilterBlock> SetColorGreen(Expression<Func<FilterBlock, Color?>> selector, IReadOnlyList<string> args)
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

        private Action<FilterBlock> SetColorBlue(Expression<Func<FilterBlock, Color?>> selector, IReadOnlyList<string> args)
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

        private Action<FilterBlock> SetInt(Expression<Func<FilterBlock, int?>> selector, IReadOnlyList<string> args, int min, int max)
        {
            ParseInt(args, min, max, out var value);
            return (block) => block.SetPropertyValue(selector, value);
        }

        private Action<FilterBlock> SetSound(Expression<Func<FilterBlock, AlertSound?>> selector, IReadOnlyList<string> args)
        {
            if (ArgParser.TryParseToggleString(args[0], out var toggle))
            {
                if (args.Count > 1)
                {
                    throw ParserException.UnrecognizedCommand(args[1]);
                }

                return
                   SetSoundToggled(selector, toggle) +
                   SetCustomSoundToggled(!toggle);
            }

            var cmd = EnsureSound(selector) + SetSoundId(selector, args[0]);

            if (args.Count >= 2)
            {
                cmd += SetSoundVolume(selector, args[1]);
            }

            if (args.Count >= 3)
            {
                ArgParser.ThrowIfNotPositionalString(args[2], out var positional);
                cmd += SetSoundPositional(selector, positional);
            }

            return cmd;
        }

        private static Action<FilterBlock> EnsureSound(Expression<Func<FilterBlock, AlertSound?>> selector) 
        {
            var method = selector.Compile();
            return (b) =>
            {
                if (method(b) == null)
                {
                    b.SetPropertyValue(selector, new AlertSound());
                }
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

        private static Action<FilterBlock> SetSoundPositional(Expression<Func<FilterBlock, AlertSound?>> selector, bool positional)
        {
            var method = selector.Compile();
            return (block) =>
            {
                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Positional = positional;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private static Action<FilterBlock> SetSoundToggled(Expression<Func<FilterBlock, AlertSound?>> selector, bool toggle)
        {
            var method = selector.Compile();
            return (block) =>
            {
                if (method(block) == null)
                {
                    block.SetPropertyValue(selector, new AlertSound());
                }

                var baseSound = method(block) ?? throw new Exception("SetPropertyValue failed");

                baseSound.Enabled = toggle;

                block.SetPropertyValue(selector, baseSound);
            };
        }

        private static Action<FilterBlock> SetCustomSound(string path)
        {
            return SetCustomSoundToggled(true) + ((b) => b.CustomAlertSound = path);
        }

        private static Action<FilterBlock> SetCustomSoundToggled(bool toggle)
        {
            return (b) => b.CustomAlertSoundEnabled = toggle;
        }

        private void ParseInt(IReadOnlyList<string> args, int min, int max, out int result, [CallerArgumentExpression("result")] string? resultName = null)
        {
            ArgParser.ThrowIfArgsWrong(args, 1);

            ArgParser.ThrowIfIntOutOfRange(args[0], min, max, out result, resultName);
        }

        private Action<FilterBlock> SetMiniMapIcon(IReadOnlyList<string> args)
        {
            ArgParser.ThrowIfArgsWrong(args, 1, 2, 3);

            var command = EnsureMiniMapIcon() + SetMiniMapIconSize(args[0]);

            if (args.Count >= 2)
            {
                command += SetMiniMapIconColor(args[1]);
            }

            if (args.Count >= 3)
            {
                command += SetMiniMapIconShape(args[2]);
            }

            return command;
        }

        private static Action<FilterBlock> EnsureMiniMapIcon() => 
            (b) => b.MinimapIcon ??= new MiniMapIcon();

        private Action<FilterBlock> SetMiniMapIconSize(string arg)
        {
            ArgParser.ThrowIfIntOutOfRange(arg, 0, 2, out var size);

            return (b) => {
                if (b.MinimapIcon == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.MinimapIcon)}");
                }

                b.MinimapIcon.Size = size;
            };
        }

        private Action<FilterBlock> SetMiniMapIconColor(string arg)
        {
            ArgParser.ThrowIfNotEnum<FilterColor>(arg, out var color);

            return (b) => {
                if (b.MinimapIcon == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.MinimapIcon)}");
                }

                b.MinimapIcon.Color = color;
            };
        }

        private Action<FilterBlock> SetMiniMapIconShape(string arg)
        {
            ArgParser.ThrowIfNotEnum<MiniMapIconShape>(arg, out var shape);

            return (b) => {
                if (b.MinimapIcon == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.MinimapIcon)}");
                }

                b.MinimapIcon.Shape = shape;
            };
        }

        private static Action<FilterBlock> SetMiniMapIconToggle(bool toggle)
        {
            return (b) =>
            {
                if (b.MinimapIcon == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.MinimapIcon)}");
                }

                b.MinimapIcon.Enabled = toggle;
            };
        }

        private Action<FilterBlock> SetPlayEffect(IReadOnlyList<string> args)
        {
            var cmd = EnsurePlayEffect() + SetPlayEffectColor(args[0]);

            if (args.Count >= 2)
            {
                cmd += SetPlayEffectDuration(args[1]);
            }

            return cmd;
        }

        private static Action<FilterBlock> EnsurePlayEffect() => (b) => b.PlayEffect ??= new PlayEffect();

        private Action<FilterBlock> SetPlayEffectColor(string arg)
        {
            ArgParser.ThrowIfNotEnum<FilterColor>(arg, out var color);

            return (b) =>
            {
                if (b.PlayEffect == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.PlayEffect)}");
                }

                b.PlayEffect.Color = color;
            };
        }

        private static Action<FilterBlock> SetPlayEffectDuration(string arg)
        {
            var dur = arg.ToLower();
            if (dur == "permanent")
            {
                return (b) =>
                {
                    if (b.PlayEffect == null)
                    {
                        throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.PlayEffect)}");
                    }

                    b.PlayEffect.Temporary = false;
                };
            }
            else if (dur == "temporary")
            {
                return (b) =>
                {
                    if (b.PlayEffect == null)
                    {
                        throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.PlayEffect)}");
                    }

                    b.PlayEffect.Temporary = true;
                };
            }

            throw ParserException.UnrecognizedCommand(arg);
        }

        private static Action<FilterBlock> SetPlayEffectToggle(bool toggle)
        {
            return (b) =>
            {
                if (b.PlayEffect == null)
                {
                    throw new ParserException($"SetPropertyValue failed to instantiate {nameof(b.PlayEffect)}");
                }

                b.PlayEffect.Enabled = toggle;
            };
        }
    }
}