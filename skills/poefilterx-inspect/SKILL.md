---
name: poefilterx-inspect
description: "Inspect copied PoE1/PoE2 items via VS Code or CLI; trace matching rules/styles, supply missing facts, debug unexpected matches."
---

Identify selected entrypoint, game, area level, compiler variables. Obtain actual copied item text.
Game copy: hover item Ctrl+C; Ctrl+Alt+C includes advanced modifier information.
VS Code: PoeFilterX: Inspect Item. Paste text; choose PoE1/PoE2 + area level; Check Item. Item properties supplies fact overrides. Source links open matching/inherited rules and style declarations.
CLI: poefilterx check entrypoint.filterx --item-file item.txt --game Poe1 --area-level 83
Interactive: poefilterx check [entrypoint]; paste item; finish empty line or single . line. Redirected stdin ends at EOF.
Defaults: Poe1; area PoE1 83, PoE2 80. Set actual drop area when testing AreaLevel.
--fact BaseArmour=100 supplies property. Repeat --fact for others. Text alternatives separated |; shell-quote full assignment.
--define Name=value sets compiler variable. Repeat per variable. Match intended build configuration.
--format Json yields structured result/diagnostics. --no-restore/--locked control package restore.
Missing-fact diagnostic: obtain advanced copy or request actual value; supply override from evidence.
Trace first matched rule; later .filterx blocks precede earlier ones. Check inherited conditions, guard variables, exact vs partial text comparisons, entrypoint/import context.
For appearance: inspect styles in application order + overridden properties; later declarations win. Preview shows label/icon; check game for sound/pillar behavior.
After fix: inspect failing item + neighboring intended matches; rebuild selected entrypoint.
