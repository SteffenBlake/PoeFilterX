using PoeFilterX.Common;

namespace PoeFilterX.Engine.Compilation;

[OperationModel(nameof(StyleOperationsCodec), OperationLanguage.Style)]
public sealed record StyleOperations
{
    [Operation(Name = "show", Reader = "ReadShow", SkipOutput = true)]
    public FilterVisibility? Show { get; init; }

    [Operation(Name = "border-color", Reader = "ReadColor", OutputName = "SetBorderColor", DefaultAlpha = 255)]
    public ColorValue? BorderColor { get; init; }

    [Operation(Name = "border-color-alpha", Reader = "ReadColorAlpha", Target = "BorderColor", SkipOutput = true)]
    public ColorValue? BorderColorAlpha { get; init; }

    [Operation(Name = "border-color-red", Reader = "ReadColorRed", Target = "BorderColor", SkipOutput = true)]
    public ColorValue? BorderColorRed { get; init; }

    [Operation(Name = "border-color-green", Reader = "ReadColorGreen", Target = "BorderColor", SkipOutput = true)]
    public ColorValue? BorderColorGreen { get; init; }

    [Operation(Name = "border-color-blue", Reader = "ReadColorBlue", Target = "BorderColor", SkipOutput = true)]
    public ColorValue? BorderColorBlue { get; init; }

    [Operation(Name = "font-color", Reader = "ReadColor", OutputName = "SetTextColor", DefaultAlpha = 255)]
    public ColorValue? FontColor { get; init; }

    [Operation(Name = "font-color-alpha", Reader = "ReadColorAlpha", Target = "FontColor", SkipOutput = true)]
    public ColorValue? FontColorAlpha { get; init; }

    [Operation(Name = "font-color-red", Reader = "ReadColorRed", Target = "FontColor", SkipOutput = true)]
    public ColorValue? FontColorRed { get; init; }

    [Operation(Name = "font-color-green", Reader = "ReadColorGreen", Target = "FontColor", SkipOutput = true)]
    public ColorValue? FontColorGreen { get; init; }

    [Operation(Name = "font-color-blue", Reader = "ReadColorBlue", Target = "FontColor", SkipOutput = true)]
    public ColorValue? FontColorBlue { get; init; }

    [Operation(Name = "bg-color", Reader = "ReadColor", OutputName = "SetBackgroundColor", DefaultAlpha = 240)]
    public ColorValue? BackgroundColor { get; init; }

    [Operation(Name = "bg-color-alpha", Reader = "ReadColorAlpha", Target = "BackgroundColor", SkipOutput = true)]
    public ColorValue? BackgroundColorAlpha { get; init; }

    [Operation(Name = "bg-color-red", Reader = "ReadColorRed", Target = "BackgroundColor", SkipOutput = true)]
    public ColorValue? BackgroundColorRed { get; init; }

    [Operation(Name = "bg-color-green", Reader = "ReadColorGreen", Target = "BackgroundColor", SkipOutput = true)]
    public ColorValue? BackgroundColorGreen { get; init; }

    [Operation(Name = "bg-color-blue", Reader = "ReadColorBlue", Target = "BackgroundColor", SkipOutput = true)]
    public ColorValue? BackgroundColorBlue { get; init; }

    [Operation(Name = "font-size", OutputName = "SetFontSize", Minimum = 1, Maximum = 45)]
    public int? FontSize { get; init; }

    [Operation(Name = "alert", Reader = "ReadAlert", Writer = "WriteSound")]
    public SoundValue? Alert { get; init; }

    [Operation(Name = "alert-id", Reader = "ReadAlertId", Target = "Alert", SkipOutput = true)]
    public SoundValue? AlertId { get; init; }

    [Operation(Name = "alert-volume", Reader = "ReadAlertVolume", Target = "Alert", SkipOutput = true)]
    public SoundValue? AlertVolume { get; init; }

    [Operation(Name = "alert-style", Reader = "ReadAlertStyle", Target = "Alert", SkipOutput = true)]
    public SoundValue? AlertStyle { get; init; }

    [Operation(Name = "drop-sound", Reader = "ReadToggle", Writer = "WriteDropSound")]
    public bool? DropSound { get; init; }

    [Operation(Name = "drop-sound-if-alert", Reader = "ReadToggle", Writer = "WriteDropSoundIfAlert")]
    public bool? DropSoundIfAlert { get; init; }

    [Operation(Name = "alert-optional", Reader = "ReadAlertOptional", Target = "Alert", SkipOutput = true)]
    public SoundValue? AlertOptional { get; init; }

    [Operation(Name = "alert-path-volume", Reader = "ReadCustomAlertVolume", Target = "Alert", SkipOutput = true)]
    public SoundValue? AlertPathVolume { get; init; }

    [Operation(Name = "alert-path", Reader = "ReadAlertPath", Target = "Alert", Writer = "WriteCustomSound")]
    public SoundValue? AlertPath { get; init; }

    [Operation(Name = "icon", Reader = "ReadIcon", OutputName = "MinimapIcon")]
    public IconValue? Icon { get; init; }

    [Operation(Name = "icon-size", Reader = "ReadIconSize", Target = "Icon", SkipOutput = true)]
    public IconValue? IconSize { get; init; }

    [Operation(Name = "icon-color", Reader = "ReadIconColor", Target = "Icon", SkipOutput = true)]
    public IconValue? IconColor { get; init; }

    [Operation(Name = "icon-shape", Reader = "ReadIconShape", Target = "Icon", SkipOutput = true)]
    public IconValue? IconShape { get; init; }

    [Operation(Name = "pillar", Reader = "ReadPillar", OutputName = "PlayEffect")]
    public EffectValue? Pillar { get; init; }

    [Operation(Name = "pillar-color", Reader = "ReadPillarColor", Target = "Pillar", SkipOutput = true)]
    public EffectValue? PillarColor { get; init; }

    [Operation(Name = "pillar-duration", Reader = "ReadPillarDuration", Target = "Pillar", SkipOutput = true)]
    public EffectValue? PillarDuration { get; init; }
}
