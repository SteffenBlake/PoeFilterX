---
name: poefilterx-fss
description: "Write/edit .fss: style classes, precedence, colors, fonts, icons, sounds, pillars."
---

Read selected entrypoint, imported styles, affected Style references. Match project conventions.
Syntax: valuable { show: true; font-size: 40; } Comments: /* ... */.
using base.fss; at file root; relative paths; quote spaces.
Classes case-sensitive. Apply via Style valuable; in .filterx.
Declarations apply in stylesheet/import order. Later properties win. Reopening class extends/overrides. Style argument order leaves precedence unchanged.
Partial properties preserve other fields. Omitted shorthand args preserve inherited values.
show: true|false; → Show|Hide. font-size: 1–45;
font-color/border-color/bg-color: R G B [A]; channels 0–255. Numeric default alpha: font/border 255, bg 240.
Hex: 0xRRGGBB or 0xAARRGGBB; six-digit alpha 255.
Named colors: all 148 CSS names and aliases, bare and case-insensitive (black, rebeccapurple, grey). Alpha 255, including bg-color; transparent is 0 0 0 0. currentColor/system colors unsupported.
VS Code offers named-color completions and color swatches/picker. Picker uses an exact CSS name when available, otherwise 0xRRGGBB or 0xAARRGGBB; preserves matching name spelling.
Component setters: <color-property>-red/-green/-blue/-alpha: 0–255;
icon: size color shape; size 0–2 (0 largest). Components icon-size/icon-color/icon-shape. Output needs all three.
Icon/pillar colors only: Red Green Blue Brown White Yellow Cyan Grey Orange Pink Purple.
Shapes: Circle Diamond Hexagon Square Star Triangle Cross Moon Raindrop Kite Pentagon UpsideDownHouse.
pillar: color [Permanent|Temporary]; components pillar-color/pillar-duration. Output needs color; initial duration Permanent.
alert: id [volume] [global|positional]; id 1–16, volume 0–300. Components alert-id/alert-volume/alert-style. Output needs id; initial style global.
alert-path: "sound.mp3"; selects custom sound. Path resolved by game; deploy sound with filter assets.
alert-path: disabled; selects stored built-in sound. alert-path: enabled; restores custom path.
Numeric alert, alert-id/volume/style, or alert: enabled; selects built-in sound. alert: disabled; disables built-in sound.
alert/icon/pillar: enabled|disabled; toggles preserve stored properties. drop-sound: enabled|disabled;
After edit: build; inspect representative item; trace later declarations when appearance differs.
