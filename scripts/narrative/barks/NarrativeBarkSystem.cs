using System;
using System.Collections.Generic;

namespace Tts.Narrative;

public class NarrativeBarkSystem : INarrativeBarkSystem
{
    private readonly IReadOnlyDictionary<string, string[]> _pools;
    private readonly Random _rng;

    public NarrativeBarkSystem(IReadOnlyDictionary<string, string[]> pools, Random rng)
    {
        _pools = pools;
        _rng = rng;
    }

    public string? TryGetBark(BarkTrigger trigger, StoryState state)
    {
        var tag = TriggerToTag(trigger);
        if (!_pools.TryGetValue(tag, out var pool) || pool.Length == 0) return null;
        return pool[_rng.Next(pool.Length)];
    }

    private static string TriggerToTag(BarkTrigger trigger) => trigger switch
    {
        BarkTrigger.ChapterStart   => "chapter_start",
        BarkTrigger.MissionWon     => "mission_won",
        BarkTrigger.MissionLost    => "mission_lost",
        BarkTrigger.EnemyIsWinning => "enemy_winning",
        BarkTrigger.PlayerLeading  => "player_leading",
        _ => trigger.ToString()
    };
}
