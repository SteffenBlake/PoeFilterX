---
name: poefilterx-configuration
description: "Configure PoeFilterX projects, profiles, environment/CLI precedence, local package overrides."
---

Read .poefilterx.json + selected .poefilterx.<profile>.json before edits.
Root: search input-file directory (else cwd) + ancestors for .poefilterx.json; fallback nearest entrypoint.filterx/*.entrypoint.filterx directory.
Precedence low→high: base JSON → selected profile JSON → POEFILTERX_ env → explicit CLI args. CLI defaults don't override configured values.
Profile: --profile name; fallback POEFILTERX_PROFILE. File required when selected. Names: ASCII letters/digits/-/_. Dev not auto-active.
Init: poefilterx init --name "My Filter"; creates base/dev JSON, entrypoint, stylesheet, .gitignore ignoring .poefilterx.dev.json.
Track base JSON; keep machine paths in ignored dev profile. Preserve $schema: https://raw.githubusercontent.com/SteffenBlake/PoeFilterX/main/schemas/poefilterx.schema.json
JSON paths relative project root; explicit CLI input/output paths relative cwd. Output parent must exist.
Build: poefilterx build --profile dev; explicit input overrides entrypoint; --output overrides output; --verbosity overrides verbosity.
Keys: entrypoint/output/verbosity; variables string map; packageCache; packageOverrides URL→directory map; noRestore/locked/update booleans.
Check keys: item or itemFile; game Poe1/Poe2; areaLevel integer; format Text/Json; fact/define arrays of Name=value.
Init keys: name/path. LSP keys: stdio bool/socket port/pipe name; one transport.
Env: POEFILTERX_OUTPUT, POEFILTERX_VERBOSITY; nested POEFILTERX_VARIABLES__League=SSF; arrays POEFILTERX_FACT__0=BaseArmour=100.
Env aliases: POEFILTERX_{NO_RESTORE,PACKAGE_CACHE,ITEM_FILE,AREA_LEVEL,PROJECT_NAME}. Explicit bool override: --locked false.
Local package: dev JSON {"packageOverrides":{"https://github.com/UserName/my-filter-style":"/Projects/my-filter-style"}}.
URL must exactly match import, including .git suffix. Directory must exist. Keep published import URL + release pins; don't replace cache with symlinks.
Override reads working tree, including uncommitted edits; skips package fetch. No fake local commits in lockfile. --locked still requires matching lockfile.
Same map via --package-overrides '<JSON>' or POEFILTERX_PACKAGE_OVERRIDES='<JSON>'.
LSP: language-server --stdio --profile dev, or POEFILTERX_PROFILE=dev in editor launch environment. External saves refresh diagnostics; original checkout URIs supply unsaved buffers. CLI reads saved files.
Release verification: deactivate local overrides + dev profile; build against published pinned packages.
