---
name: poefilterx-filterx
description: "Write/edit .filterx: conditions, nesting, inheritance, imports, variables, guards, rule priority."
---

Read selected entrypoint + relevant imports. Preserve project naming/style. Edit .filterx for matching; .fss for appearance.
Commands end ;. Args whitespace-separated. Quote spaces. Comments # through EOL.
Example: { Class Currency; BaseType == "Divine Orb"; Style valuable; }
Conditions use game class/base names. = partial text match; == exact. Operators: = == ! < > <= >=.
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
After edit: build selected entrypoint; inspect representative copied items for changed branches.
