using System;
using System.Collections.Generic;

namespace Tts;

public class MissionGenerator : TagMatchingBase, IMissionGenerator
{
    private readonly NarrativeDatabase _db;

    public MissionGenerator(NarrativeDatabase db) => _db = db;

    public NarrativeConditionConfig SelectCondition(string[] requiredTags, StoryState state, Random rng, bool allRequired = true)
    {
        var candidates = new List<NarrativeConditionConfig>();
        foreach (var condition in _db.Conditions)
        {
            if (allRequired && HasAllTags(condition.Tags, requiredTags))
                candidates.Add(condition);
            else if (HasAnyTag(condition.Tags, requiredTags))
                candidates.Add(condition);
        }

        if (candidates.Count == 0)
            candidates.AddRange(_db.Conditions);

        return candidates[rng.Next(candidates.Count)];
    }
}
