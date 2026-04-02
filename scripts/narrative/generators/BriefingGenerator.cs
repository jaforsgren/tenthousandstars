using System;
using System.Collections.Generic;

namespace Tts;

public class BriefingGenerator : TagMatchingBase, IBriefingGenerator
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
        var candidates = new List<BriefingTemplate>();
        foreach (var template in _db.BriefingTemplates.Templates)
        {
            var tagMatch = allRequired
                ? HasAllTags(condition.Tags, template.Tags)
                : HasAnyTag(condition.Tags, template.Tags);
            if (!tagMatch) continue;
            if (!StateConditionMatcher.Matches(template.When, state)) continue;
            candidates.Add(template);
        }

        var text = candidates.Count > 0
            ? candidates[_rng.Next(candidates.Count)].Template
            : condition.Description;

        return text
            .Replace("{PlayerFaction}", state.Player.FactionName)
            .Replace("{EnemyFaction}", state.Enemy.FactionName);
    }
}
