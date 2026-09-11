---
id: tooling-powershell-json-roundtrip-rewrites-timestamps
date: 2026-08-28
agent: claude
title: Editing a store JSON file through ConvertFrom-Json rewrites every timestamp; JsonNode is case-sensitive
triggers: [ConvertFrom-Json, ConvertTo-Json, JsonNode, clean store, oversight-snapshot.json, response-times.json, instances.json, camelCase, timestamps]
files: [docs/remaining-work.md]
cost: The first store-cleanup attempt matched zero accounts; only a refuse-if-zero guard stopped it wiping every real account from the snapshot
evidence: docs/remaining-work.md §0.7 (test-residue clean, *.pre-clean-*.bak backups)
status: live
---
PowerShell `ConvertFrom-Json` parses ISO-8601 strings into `DateTime` and `ConvertTo-Json` writes them back in a different shape. A round-trip therefore silently rewrites every timestamp in a store where timestamps drive every metric. Use `System.Text.Json` `JsonNode`, which keeps untouched text as it was. `JsonNode` indexing is case-sensitive and the stores are camelCase (`instances`, `id`). Reading `Instances`/`Id` returns nothing, which makes every real account look like junk to delete. Stop the app first so its debounced save cannot race the edit. Take a `.bak`, refuse to run when the registry parses to zero accounts, and re-parse the output before replacing the original.
