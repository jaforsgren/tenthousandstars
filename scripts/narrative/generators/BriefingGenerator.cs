using System;
using System.Collections.Generic;

namespace Tts.Narrative;

public class BriefingGenerator : IBriefingGenerator
{
    private readonly NarrativeDatabase _db;
    private readonly Random _rng;

    public BriefingGenerator(NarrativeDatabase db, Random rng)
    {
        _db = db;
        _rng = rng;
    }

    public string GenerateBriefing(NarrativeConditionConfig condition, ChapterContext chapter, StoryState state, bool allRequired = true)
    {
        string[]? pool = null;

        foreach (var tag in condition.Tags)
        {
            if (state.EnemyIsWinning && _db.BriefingPools.TryGetValue($"{tag}_enemy_winning", out var ep))
                { pool = ep; break; }
            if (state.PlayerStrongerThanEnemy && _db.BriefingPools.TryGetValue($"{tag}_player_stronger", out var pp))
                { pool = pp; break; }
            if (_db.BriefingPools.TryGetValue(tag, out var bp))
                { pool = bp; break; }
        }

        var template = pool?.Length > 0 ? pool[_rng.Next(pool.Length)] : condition.Description;
        return template
            .Replace("{PlayerFaction}", state.Player.FactionName)
            .Replace("{EnemyFaction}", state.Enemy.FactionName);
    }
}
