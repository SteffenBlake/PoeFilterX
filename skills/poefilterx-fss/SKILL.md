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
Values: [] optional; | alternatives; channel/alpha 0–255; volume 0–300; size 0–2 (0 largest); id 1–16; toggles preserve stored settings.
color = CSS name | 0xRRGGBB | 0xAARRGGBB | R [G [B [A]]]; omitted numeric G/B are 0, alpha defaults font/border 255 or bg 240.
CSS names: all 148 names/aliases, case-insensitive; alpha 255, including bg-color; transparent is 0 0 0 0; currentColor/system colors unsupported.
Hex without alpha uses 255. VS Code offers name completions/swatches/picker; picker prefers exact CSS names, otherwise hex.
Icon/pillar colors: Red Green Blue Brown White Yellow Cyan Grey Orange Pink Purple.
Shapes: Circle Diamond Hexagon Square Star Triangle Cross Moon Raindrop Kite Pentagon UpsideDownHouse.
Commands:
show - show: true|false|Show|Hide|Minimal; Selects visibility; Minimal uses minimum label size and transparent background.
border-color - border-color: color; Sets label border color.
border-color-alpha - border-color-alpha: channel; Sets label border alpha only.
border-color-red - border-color-red: channel; Sets label border red only.
border-color-green - border-color-green: channel; Sets label border green only.
border-color-blue - border-color-blue: channel; Sets label border blue only.
font-color - font-color: color; Sets label text color.
font-color-alpha - font-color-alpha: channel; Sets label text alpha only.
font-color-red - font-color-red: channel; Sets label text red only.
font-color-green - font-color-green: channel; Sets label text green only.
font-color-blue - font-color-blue: channel; Sets label text blue only.
bg-color - bg-color: color; Sets label background color.
bg-color-alpha - bg-color-alpha: channel; Sets label background alpha only.
bg-color-red - bg-color-red: channel; Sets label background red only.
bg-color-green - bg-color-green: channel; Sets label background green only.
bg-color-blue - bg-color-blue: channel; Sets label background blue only.
font-size - font-size: 1–45; Sets label font size.
alert - alert: id [volume] [global|positional] | enabled|disabled; Selects built-in sound; disabling emits None while preserving settings.
alert-id - alert-id: id; Sets built-in sound ID and selects built-in sound.
alert-volume - alert-volume: volume; Sets built-in volume and selects built-in sound.
alert-style - alert-style: global|positional; Sets sound location and selects built-in sound; default global.
drop-sound - drop-sound: enabled|disabled; Toggles the normal item drop sound.
drop-sound-if-alert - drop-sound-if-alert: enabled|disabled; Toggles drop sound when an alert sound is specified.
alert-optional - alert-optional: enabled|disabled; Selects optional custom sound with built-in fallback when the file is missing.
alert-path-volume - alert-path-volume: volume; Sets custom volume independently of built-in volume.
alert-path - alert-path: "path[;path...]" [volume] | enabled|disabled; Selects/restores custom sound or clears its override; game resolves paths, so deploy assets.
icon - icon: size [color [shape]] | enabled|disabled; Sets minimap icon; output needs size/color/shape; disabling emits -1.
icon-size - icon-size: size; Sets minimap icon size.
icon-color - icon-color: color; Sets minimap icon color.
icon-shape - icon-shape: shape; Sets minimap icon shape.
pillar - pillar: color [Permanent|Temporary] | enabled|disabled; Sets light beam; output needs color; disabling emits None.
pillar-color - pillar-color: color; Sets light-beam color.
pillar-duration - pillar-duration: Permanent|Temporary; Sets light-beam duration; default Permanent.
using - using "path.fss"; Imports a stylesheet relative to this file.
After edit: build; inspect representative item; trace later declarations when appearance differs.
