using System;
using System.Collections.Generic;

namespace Tts;

public class NarrativeBarkSystem : TagMatchingBase, INarrativeBarkSystem
{
    private readonly NarrativeDatabase _db;
    private readonly Random _rng;

    public NarrativeBarkSystem(NarrativeDatabase db, Random rng)
    {
        _db = db;
        _rng = rng;
    }

    public string? TryGetBark(BarkTrigger trigger, StoryState state)
    {
        var tag = TriggerToTag(trigger);
        var candidates = new List<Bark>();

        foreach (var bark in _db.Barks.Barks)
        {
            if (!HasTag(bark.Tags, tag)) continue;
            if (!StateConditionMatcher.Matches(bark.When, state)) continue;
            candidates.Add(bark);
        }

        if (candidates.Count == 0) return null;
        var chosen = candidates[_rng.Next(candidates.Count)];
        return $"[{chosen.Npc}]: {chosen.Message}";
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
