# Ten Thousand Stars

Mobile space strategy game.

## Concept

Control systems across a star map. Each system produces ships over time based on the planets it contains. Connect systems via routes and move fleets between them to expand your territory.

## Running

Each run generates a new random map (6–12 systems).

## Debugging

### In-editor scene tools

Two scenes have `[Tool]` scripts with inspector toggles for previewing without running the game:

- **`Level.tscn`** — tick `Regenerate` in the inspector to preview a map layout. Set `PreviewSeed` to pin the seed.
- **`scenes/ui/NarrativeScreen.tscn`** — `PreviewInEditor = true` shows the full mission brief layout with sample text and faction data.

### StoryDebugScene

`scenes/debug/StoryDebugScene.tscn` runs a full campaign in-engine. Select an archetype, optionally enter a win/loss pattern (e.g. `W,L,W`), and hit **Generate**. The report shows each chapter's interlude, briefing, barks, and outcome. Interlude buttons open the real `NarrativeScreen` with live narrative data. The **Preview Outro** button unlocks after a full campaign completes.

### NarrativeCli

Headless C# runner — no Godot required. Useful for rapidly iterating on story configs.

```
dotnet run -- [archetype] [win-pattern]
dotnet run -- conquest W,L,W
dotnet run -- scenarios
dotnet run -- scenarios <played> <won>
```

`archetype` — `falling_empire`, `rising_power`, or `conquest` (omit for random).
`win-pattern` — comma-separated `W`/`L` per chapter (omit for all wins).
`scenarios` — lists all scenarios and their unlock criteria instead of running a campaign.
