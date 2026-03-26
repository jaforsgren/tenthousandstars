using System;
using System.Collections.Generic;

namespace Tts;

public class BriefingGenerator : IBriefingGenerator
{
    private readonly NarrativeDatabase _db;
    private readonly Random _rng;

    public BriefingGenerator(NarrativeDatabase db, Random rng)
    {
        _db = db;
        _rng = rng;
    }

    public string GenerateBriefing(NarrativeConditionConfig condition, ChapterContext chapter, StoryState state)
    {
        var candidates = new List<BriefingTemplate>();
        foreach (var template in _db.BriefingTemplates.Templates)
        {
            if (!HasAnyTag(condition.Tags, template.Tags)) continue;
            if (!StateConditionMatcher.Matches(template.When, state)) continue;
            candidates.Add(template);
        }

        var text = candidates.Count > 0
            ? candidates[_rng.Next(candidates.Count)].Template
            : condition.Description;

        return text
            .Replace("{PlayerFaction}", state.PlayerFactionName)
            .Replace("{EnemyFaction}", state.EnemyFactionName);
    }

    private static bool HasAnyTag(string[] conditionTags, string[] templateTags)
    {
        foreach (var t in templateTags)
            foreach (var ct in conditionTags)
                if (t == ct) return true;
        return false;
    }
}
