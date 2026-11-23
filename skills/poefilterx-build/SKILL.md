---
name: poefilterx-build
description: "Install/update PoeFilterX, initialize projects, select entrypoints, build filters, fix build errors, locate game filter folders."
---

Use project's existing CLI/VS Code workflow. Identify target game + entrypoint + output path.
VS Code: install PoeFilterX by SteffenBlake. Commands: PoeFilterX: Initialize Project; Select Entrypoint; Build. Save edited files before build.
CLI install: .NET 11 SDK; dotnet tool install --global PoeFilterX. Verify: poefilterx --version. Update: dotnet tool update --global PoeFilterX.
Standalone: extract platform release archive; invoke included PoeFilterX executable (Windows: PoeFilterX.exe).
New project: poefilterx init --name "My Filter" [--path parent]. Creates my-filter/entrypoint.filterx + my-filter.fss + base/dev JSON + .gitignore ignoring dev JSON. Work inside created folder.
Build: poefilterx [entrypoint-path]; equivalent poefilterx build [entrypoint-path].
Auto-discovery: .poefilterx.json root + configured entrypoint; fallback entrypoint.filterx or *.entrypoint.filterx. Multiple matches: pass explicit path. VS Code: Select Entrypoint/status bar.
Output: configured output; else beside input, extension .filter. CLI override: --output "destination/MyFilter.filter".
Build auto-restores Git packages. --no-restore uses cached packages; --locked requires matching lockfile.
Variables: --Name value; environment POEFILTERX_Name. Help: poefilterx build --help.
Diagnostics: read file(line,column), error code, import trace; fix referenced source; rebuild. Increase detail: --verbosity Debug or Trace. Default Information; -v/--v accept level name.
(When requested, though can offer to user, do not do this without asking first) Install generated .filter into actual game's filter folder. Resolve via game's item-filter folder button; account for redirected Documents/Wine prefixes.
Windows usual: %USERPROFILE%\Documents\My Games\Path of Exile (PoE1); ...\Path of Exile 2 (PoE2).
Proton: Steam library/steamapps/compatdata/<appid>/pfx/drive_c/users/steamuser/My Documents/My Games/<game>. Appids: PoE1 238960; PoE2 2694490. Verify active prefix/folder.
Copy referenced custom sounds alongside filter assets. In game: Options → Game → Item Filter; select/reload output. Verify client accepts filter.
Folder references: https://github.com/NeverSinkDev/NeverSink-Filter#readme https://github.com/NeverSinkDev/NeverSink-Filter-for-PoE2#readme
