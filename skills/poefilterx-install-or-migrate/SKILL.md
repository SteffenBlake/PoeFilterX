---
name: poefilterx-install-or-migrate
description: "Install PoeFilterX or migrate an existing installation/project to a requested version; confirm environment, IDE, plan, skills, verification, cleanup with user."
---

Read-only discovery first. No installation, removal, project edits, dependency updates before user confirms plan. Ask missing questions; answer user's questions before proceeding. New findings changing plan: explain, reconfirm.
Inspect OS/architecture, shell/PATH, all PoeFilterX executable locations/install sources/versions, npm globals, .NET SDK/tools, Git, Node/npm, project entrypoints/manifests/lockfiles/scripts, installed agent skills + scope/agents. Determine old CLI source before choosing version command; do not accidentally build.
Infer IDE from available process/environment/workspace evidence (TERM_PROGRAM, VSCODE_PID, .vscode, editor executables); remote terminal != confirmed desktop IDE. Report inference; ask user to confirm IDE, install vs migration, source/target versions, project path, preferred extension/CLI/standalone, skill scope/agents. Resolve ambiguity; never silently choose latest.
Read online wiki pages; give user relevant URLs. User projects do not contain this repository's wiki. Current wiki may describe newer release; verify target release assets/prerequisites/syntax. Missing migration path or unavailable target: ask, do not substitute version.
Installation: https://github.com/SteffenBlake/PoeFilterX/wiki/Installation
VS Code: https://github.com/SteffenBlake/PoeFilterX/wiki/VS-Code
Other LSP-capable IDE: https://github.com/SteffenBlake/PoeFilterX/wiki/Language-Server
Migration: https://github.com/SteffenBlake/PoeFilterX/wiki/Migration-help
Packages: https://github.com/SteffenBlake/PoeFilterX/wiki/Packages
Commands: https://github.com/SteffenBlake/PoeFilterX/wiki/Commands
Skills: https://github.com/SteffenBlake/PoeFilterX/wiki/Agent-Skills
Release: https://github.com/SteffenBlake/PoeFilterX/releases/tag/<target-version>
Present detected state, proposed commands/file changes, target versions, IDE setup, verification, all PoeFilterX skills install/update. Ask approval; wait. Unclear preferences/answers: clarify first.

## Install
Follow target-version installation docs. VS Code: publisher SteffenBlake; target-version platform .vsix if pinning. Other IDE: follow its online Language-Server section; confirm editor configuration changes.
LSP setup: read https://github.com/SteffenBlake/PoeFilterX/wiki/Language-Server ; locate confirmed IDE's section, give user its direct section URL, follow installation/configuration steps after plan approval. VS Code bundles LSP; follow VS-Code guide. IDE absent/uncertain: ask user, consult editor's official LSP docs; confirm configuration before changes.
CLI: required SDK; dotnet tool install --global PoeFilterX --version <target-version>; existing tool: dotnet tool update --global PoeFilterX --version <target-version>. Standalone: target release OS/architecture archive; verify invoked executable path. Downgrade: confirm supported procedure, do not assume update handles it.
Install prerequisites only within approved plan. Keep project creation/output installation separate unless approved.

## Migration
Read online Migration-help section for each crossed version, ascending. Preserve unrelated files/tooling and existing user edits. Review project diff + output behavior; don't just update executable.

### 3.30.0 -> 3.30.1
Old global npm CLI: npm uninstall --global poefilterx before replacement; verify PATH no longer selects it. Preserve projects. .NET tool requires .NET 11; extension/standalone follow installation page.
Replace npm filter dependencies with entrypoint-root Git imports + @alias/repository-relative paths. All package declarations in selected entrypoint. Git required. Restore/build; retain generated <entrypoint>.lock.json. Preserve commit pins; no unsolicited dependency refresh. Remove package.json/package-lock.json/node_modules only if solely obsolete PoeFilterX tooling and approved.
Auto-discovery: entrypoint.filterx or *.entrypoint.filterx in current directory; otherwise explicit input path. Confirm output filename when renaming; preserve desired name with --output.
Scripts: help -> --help; help build -> build --help; version/-v -> --version; --verbose true/--v true -> --verbosity Debug. -v/--v now log levels. npm run build -> poefilterx; npm install filter dependencies -> poefilterx restore.
Review font-size 18–100 -> 1–45; numeric bg-color default alpha 255 -> 240 (explicit 255 preserves opacity); selected styles now stylesheet declaration order, later wins; each inherited/local HasValue guard must have value. Preserve intended behavior with user; don't blindly clamp/reorder.

## Skills
Ask confirmation to install/update ALL PoeFilterX skills, including missing ones; confirm project/global scope + target agents. Node prerequisite per online Agent-Skills page. Check installed skills first; preserve local modifications, ask before overwriting.
Follow wiki npx skills guidance. Discover source skills; install/reinstall all via npx skills add SteffenBlake/PoeFilterX --skill '*' with approved scope/agent flags. For target-version skills, verify tag contains skills; use SteffenBlake/PoeFilterX@<target-version> when available. Otherwise ask compatible source choice. Do not run unscoped skills update affecting unrelated skills.
CLI options/removal: https://github.com/vercel-labs/skills/blob/main/docs/commands.md ; verify installed CLI help. No --all targeting every agent without approval.

## Verify / cleanup
Verify intended executable resolution/version or bundled server version, IDE language support, selected project build, package lockfile, relevant migrated behavior. No project: verify installation without creating one unless approved. Ask user to confirm installation works; answer questions, resolve failures before cleanup.
After verified success + user confirmation, explicitly tell user: this skill instructs removal of poefilterx-install-or-migrate as final cleanup. Ask separate confirmation; wait. Declined/pending: retain skill.
Approved: use skills CLI to remove ONLY poefilterx-install-or-migrate from confirmed installed scope/agents; preserve every other PoeFilterX skill. Check CLI removal help + installed paths/lock metadata; avoid deleting source repository skill. Verify removal, report completion.
