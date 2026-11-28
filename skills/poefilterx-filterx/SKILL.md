---
name: poefilterx-filterx
description: "Write/edit .filterx: conditions, nesting, inheritance, imports, variables, guards, rule priority."
---

Read selected entrypoint + relevant imports. Preserve project naming/style. Edit .filterx for matching; .fss for appearance.
Commands end ;. Args whitespace-separated. Quote spaces. Comments # through EOL.
Example: { Class Currency; BaseType == "Divine Orb"; Style valuable; }
Conditions use game class/base names. = partial text match; == exact. Operators: = == ! != < > <= >=.
Later blocks win; children precede parents in output. Put exceptions after broad rules.
{ ... } emits ordinary block; [ ... ] supplies inherited defaults to children.
Children inherit conditions/styles. Booleans override; numeric comparisons accumulate (AND); text lists combine per operator. Style names accumulate, case-sensitive.
Style valuable; selects .fss class. Declaration order determines property precedence. Repeating class in Style applies once.
Output needs applied style. Ordinary blocks need conditions, local/inherited. Entrypoint root can emit fallback with style actions.
using styles.fss; using rules/currency.filterx; using values.json;
using at file root; paths relative to importing file. Quote paths with spaces. .filterx imports .filterx/.fss/.json; .fss imports .fss.
Imported filters inherit caller root defaults; own root defaults stay scoped to imported tree. Repeated imports repeat contribution.
JSON vars: {"Bases":["Iron Ring","Gold Ring"]}. Import before use. Same keys across JSON imports combine/deduplicate values.
BaseType == %Bases%; expands variable args. Missing variable expands empty.
HasValue %Bases%; guards block + descendants. One HasValue passes if any arg contains text; separate HasValue commands all must pass.
IsTrue %Enabled%; False suppresses block + descendants; empty supplies no check. Pair HasValue with IsTrue when value required.
Vars: CLI --Name value > environment POEFILTERX_Name > JSON. Values with multiple words need quoted arguments inside variable value; prefer JSON string arrays.
Commands: [] optional; | alternatives; ... repeats; op comparison (default =); n nonnegative integer; pattern socket count and/or R/G/B/A/D/W letters.
AlternateQuality - AlternateQuality True|False; Matches alternate-quality items.
AlwaysShow - AlwaysShow True|False; Matches always-shown items.
ArchnemesisMod - ArchnemesisMod [op] "name"...; Matches Archnemesis modifier names.
AnyEnchantment - AnyEnchantment True|False; Matches enchanted items.
AreaLevel - AreaLevel [op] n; Compares drop-area level.
BaseArmour - BaseArmour [op] n; Compares base Armour.
BaseDefencePercentile - BaseDefencePercentile [op] n; Compares base defence percentile.
BaseEnergyShield - BaseEnergyShield [op] n; Compares base Energy Shield.
BaseEvasion - BaseEvasion [op] n; Compares base Evasion.
BaseType - BaseType [op] "name"...; Matches base names.
BaseWard - BaseWard [op] n; Compares base Ward.
BlightedMap - BlightedMap True|False; Matches Blighted maps.
Class - Class [op] "name"...; Matches game item-class names.
Corrupted - Corrupted True|False; Matches corrupted items.
CorruptedMods - CorruptedMods [op] n; Compares corrupted modifier count.
DropLevel - DropLevel [op] n; Compares minimum drop level.
ElderItem - ElderItem True|False; Matches Elder-influenced items.
ElderMap - ElderMap True|False; Matches Elder-influenced maps.
EnchantmentPassiveNode - EnchantmentPassiveNode [op] "name"...; Matches cluster-jewel enchantment names.
EnchantmentPassiveNum - EnchantmentPassiveNum [op] n; Compares cluster-jewel added passive count.
Exceptional - Exceptional True|False; Matches Exceptional gems.
Fated - Fated True|False; Matches Fated items.
Foulborn - Foulborn True|False; Matches Foulborn items.
FracturedItem - FracturedItem True|False; Matches fractured items.
GemLevel - GemLevel [op] n; Compares gem level.
HasCruciblePassiveTree - HasCruciblePassiveTree True|False; Matches items with a Crucible passive tree.
HasEaterOfWorldsImplicit - HasEaterOfWorldsImplicit [op] n; Compares Eater implicit tier (1–6).
HasEnchantment - HasEnchantment [op] [n] "name"...; Matches enchantment names, optionally by matching count.
HasExplicitMod - HasExplicitMod [op] [n] "name"...; Matches explicit modifier names, optionally by matching count.
HasImplicitMod - HasImplicitMod True|False; Matches items with implicit modifiers.
HasInfluence - HasInfluence influence...; Matches Shaper/Elder/Crusader/Hunter/Redeemer/Warlord/None influence.
HasSearingExarchImplicit - HasSearingExarchImplicit [op] n; Compares Exarch implicit tier (1–6).
HasVaalUniqueMod - HasVaalUniqueMod True|False; Matches items with a Vaal Unique modifier.
Height - Height [op] n; Compares inventory height.
Identified - Identified True|False; Matches identified items.
Imbued - Imbued True|False; Matches imbued gems.
IsVaalUnique - IsVaalUnique True|False; Matches Vaal Unique items.
ItemLevel - ItemLevel [op] n; Compares item level.
LinkedSockets - LinkedSockets [op] n; Compares largest linked socket group (n≤6).
MapTier - MapTier [op] n; Compares map tier.
MemoryStrands - MemoryStrands [op] n; Compares Memory Strands (n≤100).
MirageMap - MirageMap True|False; Matches Mirage maps.
Mirrored - Mirrored True|False; Matches mirrored items.
Quality - Quality [op] n; Compares quality.
Rarity - Rarity [op] Normal|Magic|Rare|Unique; Compares rarity.
Replica - Replica True|False; Matches Replica uniques.
Scourged - Scourged True|False; Matches Scourged items.
ShapedMap - ShapedMap True|False; Matches Shaped maps.
ShaperItem - ShaperItem True|False; Matches Shaper-influenced items.
SocketGroup - SocketGroup [op] pattern...; Matches linked socket counts/colors, e.g. 5GGG.
Sockets - Sockets [op] pattern...; Matches total socket counts/colors, e.g. 5GGG.
StackSize - StackSize [op] n; Compares stack size.
SynthesisedItem - SynthesisedItem True|False; Matches synthesised items.
TransfiguredGem - TransfiguredGem True|False; or TransfiguredGem [op] "name"...; Matches transfigured status or gem names.
TwiceCorrupted - TwiceCorrupted True|False; Matches twice-corrupted items.
UberBlightedMap - UberBlightedMap True|False; Matches Blight-ravaged maps.
UnidentifiedItemTier - UnidentifiedItemTier [op] n; Compares unidentified item tier.
Vestigial - Vestigial True|False; Matches Vestigial items.
WaystoneTier - WaystoneTier [op] n; Compares Waystone tier.
Width - Width [op] n; Compares inventory width.
ZanaMemory - ZanaMemory True|False; Matches Zana Memory maps.
Style - Style name...; Applies named .fss classes.
HasValue - HasValue value...; Keeps the branch when any argument contains text.
IsTrue - IsTrue [True|False]; Suppresses the branch on False; empty adds no check.
using - using "path"; Imports local/package source or JSON variables.
import - import alias from "git-url" [at "commit"]; Declares an entrypoint Git package alias.
After edit: build selected entrypoint; inspect representative copied items for changed branches.
