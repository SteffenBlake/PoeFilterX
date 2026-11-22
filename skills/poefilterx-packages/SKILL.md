---
name: poefilterx-packages
description: "Declare Git packages, import package files, restore/update dependencies, pin commits, manage lockfiles."
---

Read entrypoint package declarations + lockfile. Preserve aliases/pins unless dependency change requested.
Git available on PATH for fetching packages.
Entrypoint root: import theme from "https://github.com/owner/repo.git";
Use file: using @theme/styles/main.fss; Paths relative to repository root; .filterx/.fss/.json follow importing file's allowed types.
Aliases case-sensitive; start letter/underscore, then letters/digits/dot/underscore/hyphen.
Declare all package dependencies at selected entrypoint root. Imported filters consume those aliases.
Package paths stay inside repository; symlink paths rejected.
Pin: import theme from "https://github.com/owner/repo.git" at "<full-commit-id>"; Commit: 40/64 hex chars.
Restore: poefilterx restore [entrypoint]; VS Code: PoeFilterX: Restore Packages. Builds auto-restore.
Lockfile: <entrypoint>.lock.json, e.g. entrypoint.filterx.lock.json. Keep with project for reproducible commits.
Ordinary restore reuses locked commits. Requested refresh: poefilterx restore [entrypoint] --update; unpinned packages advance to remote HEAD; explicit pins stay fixed.
--locked requires matching lockfile. build --no-restore uses existing cache. Cache override: POEFILTERX_PACKAGE_CACHE.
Restore failure: check Git availability/authentication, repository URL, commit, alias/path, lockfile diagnostic; repair cause; retry restore then build.
